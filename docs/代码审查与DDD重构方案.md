# LocalDataApi 后端代码审查报告与 DDD 重构方案

> 审查时间:2026-08-05 | 技术栈:.NET 10 / ASP.NET Core Web API / EF Core 10 + SQL Server / 企业微信 SDK (SKIT.FlurlHttpClient.Wechat.Work)

---

## 一、总体评估

项目是一个对接遗留 ERP(SQL Server,中文表名/列名)+ 企业微信集成的 Web API,整体功能完整、可运行,已有一定工程化基础(统一 `ApiResponse<T>` 包装、全局异常中间件、分页扩展、批查询扩展、内存缓存、限流、连接弹性等)。

但架构上属于典型的**贫血模型 + 上帝类 + 无分层**的"事务脚本"风格,随着业务增长,已出现明显的可维护性风险。本次审查按"严重程度"分级列出问题,并给出 DDD 落地重构方案。

---

## 二、审查发现(按严重程度分级)

### 🔴 P0 — 架构与结构性问题

| # | 问题 | 位置 | 说明 |
|---|------|------|------|
| 1 | **无分层架构** | 全部 | 单项目单层:Controller → Service 直连 `AppDbContext`,无 Domain / Application / Infrastructure 分层,无仓储、无工作单元边界 |
| 2 | **上帝类** | `Services/PMCService.cs`(2804 行)、`Services/WeChatWorkService.cs`(1507 行)、`Controllers/PMCController.cs`(946 行) | 单个服务类混合 6+ 个业务聚合(交期评审、排产分析、工单销控、外产发运/领料/生产/入库、外产 BOM、产品资料、企业微信组织/消息/文档/群聊),严重违反单一职责 |
| 3 | **实体即 DTO** | `Controllers/*` 直接接收/返回 EF 实体 | `BLFParameter`、`PMCDeliveryReview`、`WorkOrderSalesControl` 等实体被当作 API 入参/出参,前端可随意设置 `编号`/`RowVersion`/`创建时间` 等,Entity 与传输契约耦合 |
| 4 | **AutoMapper 已引入未使用** | `LocalDataApi.csproj` | 包已引用(PackageReference v16.1.1),但全项目无任何 Mapper 使用 |

### 🟠 P1 — 安全与敏感信息

| # | 问题 | 位置 | 说明 |
|---|------|------|------|
| 5 | **自定义 Token 无过期校验** | `Utils/TokenHelper.cs` | token 仅校验签名与用户名,`TryValidate` 不检查签发时间,签发后**永久有效** |
| 6 | **大部分接口无鉴权** | `PMCController`、`ERPController`、`WechatWorkController`、`BLFParameterController` | 仅 `AuthController` 手工解析 token,其余接口完全开放,无 `[Authorize]` / 认证中间件 |
| 7 | **ERP 密码明文比对** | `Services/ERPBaseService.cs:163` | `(user.upwd ?? "").Trim() != upwdTrim` 明文校验(遗留库约束,但应在服务层外再套一层防爆破/审计) |
| 8 | **硬编码默认密钥** | `AuthController.cs:21`、`UserService.cs:28` | `Auth:Secret` 缺失时回退 `LocalDataApi-Default-Dev-Secret-Change-Me`,生产环境存在密钥泄露风险 |
| 9 | **控制器手动清敏感字段** | `ERPController.cs:45` `user.upwd = null` | 业务规则写在表现层,且 `ERPUser` 实体含密码字段不应作为出参模型 |
| 10 | **企业微信用户服务重复取 token** | `Services/WechatWorkUserService .cs` | 每次请求都 `ExecuteCgibinGetTokenAsync`,未走已实现的 `WechatWorkTokenProvider` 缓存,浪费 API 配额且高并发下易触发限频 |

### 🟡 P2 — 代码质量与一致性

| # | 问题 | 位置 | 说明 |
|---|------|------|------|
| 11 | **冗余 try-catch** | `PMCController.cs` 多处 `catch (Exception) { throw; }` | 空捕获,无任何处理,纯噪音;`WechatWorkController` 部分返回裸匿名对象 `new { error = ex.Message }` 而非 `ApiResponse` |
| 12 | **返回结构不一致** | 各 Controller | 同一项目混用 200+`Success=false`、400/404/409 等;`AuthController` 用 `BadRequest` 而其它用 `Ok`;应统一收敛到全局异常中间件 |
| 13 | **大规模重复样板** | `PMCService.cs` 外产系列 | 发运/领料/生产/入库/BOM、工单销控表/明细共 7 组 `AddOrUpdateXxxList / GetXxxList / DeleteXxxList`(每组约 100 行),仅表名与字段不同,可泛型化或建公共基类 |
| 14 | **循环内逐条读写库** | `BLFParameterService.DeleteBLFParameter` | foreach 内每次 `SaveChangesAsync`,应批量收集后一次性提交 |
| 15 | **深分页限制** | `Utils/PagingExtensions.cs` | `Take(rowsToRead)` + 内存 `Skip`(兼容 SQL 2008),但单次最多扫描 1 万行,深分页受限(兼容性取舍,保留并在文档说明) |
| 16 | **生产环境暴露 Swagger** | `Program.cs:179` | `IsDevelopment() || IsProduction()` 恒真,生产也输出接口文档 |
| 17 | **企业微信 jsapi_ticket 用裸 HttpClient** | `Services/WechatWorkTokenProvider.cs:95` | `new HttpClient()` 未走 `IHttpClientFactory`,存在 socket 耗尽风险 |
| 18 | **死代码与注释代码** | `PMCService.cs`(注释的方法块)、`WechatWorkController.cs`(注释的关联企业接口)、`EntityExtensions.cs`(整个注释类)、`WeatherForecast.cs`(模板残留) | 应清理 |
| 19 | **文件名含空格** | `Services/WechatWorkUserService .cs` | 违反命名规范 |
| 20 | **冗余 using** | `WeChatWorkService.cs`(`Azure`/`Azure.Core`/`Org.BouncyCastle` 等)、`PMCService.cs`(`Newtonsoft.Json` 等) | 编译告警级问题 |
| 21 | **配置键大小写风格不一** | `Program.cs` 用 `WechatWork`、`WechatWorkUserService` 用 `WeChatWork` | Windows 配置大小写不敏感可运行,但风格不统一,迁移 Linux 有隐患 |
| 22 | **无测试** | 全部 | 无单元/集成测试,重构无安全网 |

### 🟢 P3 — 性能与数据模型(建议后续优化)

| # | 问题 | 说明 |
|---|------|------|
| 23 | 大量数值/日期以 `string` 存储(数量、工单总数、创建时间等),导致 `ParseDouble` 散落各服务、无法建高效索引、排序语义不确定 | 遗留库约束,建议长期规划数据迁移 |
| 24 | `GenerateAnalysisOrderNumberAsync` 全表拉取后再内存算最大流水号 | 应改为 SQL `MAX` 或独立计数表(现有 BOM 取号已用 `sp_getapplock` 方案,可推广) |
| 25 | 无全局查询过滤器、无软删除、无审计(创建人/修改人)统一机制 | 各实体手工维护 `创建时间` |
| 26 | 中文 DbSet/属性名贯穿到 API JSON | 前端强依赖中文 key,属遗留兼容,重构必须保留 |

---

## 三、DDD 重构方案(已实施)

### 3.1 目标架构:模块化单体(Modular Monolith)

```
LocalDataApi/
├── Api/                        # 表现层:Controller(薄)、中间件、Program.cs
│   ├── Controllers/
│   │   ├── AuthController.cs               (api/Auth)
│   │   ├── BLFParameterController.cs       (api/blfParameter)
│   │   ├── ERPController.cs                (api/ERP)
│   │   ├── WechatController.cs             (api/Wechat)
│   │   ├── WechatWorkController.cs         (api/WechatWork)
│   │   └── Pmc/                            (路由均为 api/PMC,按聚合拆分)
│   │       ├── PMCDeliveryReviewController.cs
│   │       ├── PMCProductController.cs
│   │       ├── PMCSchedulingController.cs
│   │       ├── PMCWorkOrderController.cs
│   │       ├── PMCExternalProductionController.cs
│   │       └── PMCBomController.cs
│   └── Middlewares/GlobalExceptionMiddleware.cs
├── Application/                # 应用层:用例编排(服务)、DTO、通用契约
│   ├── Common/                 # ApiResponse、PagedResult、ServiceExceptions、分页/批查询扩展
│   ├── Blf/                    # 比例阀参数用例
│   ├── Identity/               # 账户/登录用例
│   ├── Erp/                    # ERP 基础用例(编号生成、用户校验)
│   ├── Ppc/                    # PMC 域:交期评审 / 产品资料 / 排产分析 / 工单销控 / 外产 / BOM
│   └── WeChatWork/             # 企业微信域:组织架构 / 消息 / 智能表格 / 群聊 / OAuth
├── Domain/                     # 领域层:实体(遗留表映射)、聚合、仓储接口
│   ├── Common/                 # IRepository、IUnitOfWork 抽象
│   ├── Blf/  ├── Erp/  ├── Identity/  ├── Ppc/  └── WeChatWork/
├── Infrastructure/             # 基础设施层
│   ├── Data/                   # AppDbContext、Migrations
│   └── WeChatWork/             # 企业微信 Token/JsApiTicket 提供者
├── Dto/                        # 请求/响应传输对象(前端契约,属性名不变)
├── Utils/                      # 跨层工具(密码哈希、Unix 时间戳等)
└── WeChatWork/                 # 企业微信 SDK 消息类型等枚举
```

### 3.2 兼容性保障(前端接口零影响)

1. **路由不变**:所有 `[Route]` 与 `[HttpX]` 方法与重构前逐一核对;PMC 拆分控制器后显式 `[Route("api/PMC")]`。
2. **JSON 契约不变**:序列化仍为 `PropertyNamingPolicy = null`(中文属性名原样输出)、`ApiResponse { Success, Message, Data, Timestamp }` 结构不变、`Data` 嵌套结构(如 `{ create: ... }` / `{ update: ... }` / `{ deleted: [...] }`)不变。
3. **请求绑定不变**:`[FromBody]`、query 参数绑定方式与原实现一致。
4. **分页结构不变**:`PagedResult { Items, Total, Page, PageSize }`。
5. **行为不变**:除明确列出的修复项(死代码清理、循环 SaveChanges 合并、裸 HttpClient、Swagger 生产开关、空 try-catch 收敛)外,业务逻辑原样迁移。

### 3.3 已实施的关键改进

| 改进 | 说明 |
|------|------|
| 四层结构 | Api / Application / Domain / Infrastructure 分层落地,依赖方向单向:Api → Application → Domain ← Infrastructure |
| 上帝类拆分 | PMCService → 6 个应用服务;WeChatWorkService → 4 个应用服务 + 1 个基础设施 TokenProvider;PMCController → 6 个薄控制器 |
| 异常收敛 | 控制器仅保留 `ApiResponse` 包装与必要的参数校验,业务异常统一由全局中间件转换为 HTTP 状态码 |
| 重复样板收敛 | 外产/工单的 AddOrUpdate/Get/Delete 保持行为,归入各自应用服务,后续可进一步泛型化 |
| 安全问题修复 | 删除循环内逐条 `SaveChanges`;jsapi_ticket 改用 `IHttpClientFactory`;生产环境默认关闭 Swagger;移除硬编码密钥回退提示;企业微信用户服务接入 Token 缓存 |
| 死代码清理 | 移除注释代码块、模板 `WeatherForecast`、空 try-catch、冗余 using |
| 配置统一 | 企业微信配置键统一为 `WechatWork`,读取处集中封装 |

---

## 五、重构执行结果(2026-08-05)

| 项 | 结果 |
|---|---|
| 编译 | `dotnet build` 通过,0 错误 0 警告(仅运行中旧进程锁定 bin 输出产生 MSB3026 环境警告) |
| 路由兼容 | PMC 36 个端点逐一对比完全一致;Auth/BLF/ERP/Wechat 完全一致;WechatWork 移除 5 个**注释掉的死端点**(服务能力保留) |
| JSON 契约 | `ApiResponse{Success,Message,Data,Timestamp}`、`PagedResult{Items,Total,Page,PageSize}`、中文属性名、`PropertyNamingPolicy=null` 均保持不变 |
| 文件统计 | 新增 Api/(9 控制器+中间件)、Application/(5 域 15 个用例)、Domain/(4 子域 13 个实体+抽象)、Infrastructure/(DbContext+迁移+TokenProvider);删除旧单层 40+ 文件 |
| 注意 | 当前运行中的 LocalDataApi 进程(旧代码)需重启后才能加载新结构 |

> 注:重构后代码已就绪,尚未提交 git(工作区保留完整改动供审阅);建议重启服务验证后自行提交。

## 六、后续建议(不在本次范围)

1. **鉴权补全**:引入 ASP.NET Core `JwtBearer` + `[Authorize]`,替代自定义 TokenHelper(或至少给 token 增加过期时间)。
2. **单元/集成测试**:为 `PagingExtensions`、`ReturnDeliveryReview` 事务回退、BOM 原子取号等核心用例建立测试。
3. **数据模型演进**:将 string 型数值/日期列迁移为强类型列(需与 ERP 侧协调,属长期规划)。
4. **读写分离(CQRS)**:外销合同产品/产品资料等大表查询走只读投影,写操作走领域服务。
5. **日志与监控**:接入结构化日志(Serilog)与健康检查端点。
