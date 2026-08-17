# LocalDataApi 项目长期备忘

## 项目约束(重构/开发时必须遵守)
- **前端接口契约零影响**:JSON 序列化 `PropertyNamingPolicy=null`(中文属性名原样输出)、`ApiResponse{Success,Message,Data,Timestamp}`、`PagedResult{Items,Total,Page,PageSize}` 均不可变
- **遗留 ERP 库**:SQL Server 中文表名/列名,实体为贫血数据载体;实体 `编号` 主键为 string;数量/日期多为 string 存储(需 ParseDouble 兼容)
- **PMC 控制器路由**:拆分后多个控制器均显式 `[Route("api/PMC")]`,端点路径不得与旧版(见 git HEAD)冲突
- **业务异常**:统一抛 `LocalDataApi.Application.Common` 的 ValidationException/NotFoundException/ConflictException,由全局中间件转 HTTP 状态码,Controller 不再各自 try-catch
- **依赖方向**:Api → Application → Domain ← Infrastructure,禁止反向引用

## 架构(2026-08-05 重构后)
- `Api/` 控制器 + GlobalExceptionMiddleware + Program.cs
- `Application/`:Blf、Identity、Erp、Pmc(6 个 Pmc*Service,接口在 Contracts/)、WeChatWork(5 个服务 + WechatWorkServiceBase)
- `Domain/`:实体按 Blf/Erp/Identity/Pmc/WeChatWork 子域;Common 下有 IRepository/IUnitOfWork 抽象(暂未接入实现)
- `Infrastructure/`:Data(AppDbContext、Migrations)、WeChatWork(WechatWorkTokenProvider,单例)
- 保留:`Dto/`(前端契约)、`Utils/`(PagingExtensions、QueryBatchExtensions、TokenHelper 等)、`WeChatWork/WechatWorkMessageType.cs`
- 说明:PmcBomService 内含排产分析单号生成逻辑(SaveSchedulingAnalysisAsync),因 Scheduling 服务反向依赖其 GetBomByItemNo,合置避免循环依赖

## 已知遗留(后续优化候选)
- 鉴权未覆盖:大部分业务接口无 [Authorize];自定义 TokenHelper 无过期校验
- ERP 密码明文比对(遗留库约束);生产部署需配置 Auth:Secret 与 WechatWork 配置
- WeChatWorkLoginService 已移除(未接入任何接口的死代码);企业微信 OAuth 能力由 WechatWorkUserService 承担

## RBAC 权限中心(2026-08-08 已实现,commit b26db3d + 172970c)
- **落地方式**:EF Migration(用户选定)——`dotnet ef database update` 已执行,6 表+用户表 5 列已建;**迁移边界**:仅 BLF 3 表 + RBAC 6 表由 EF 管理,其余 DB-First 表(中文表/视图/用户表)在 OnModelCreating 里 `SetIsTableExcludedFromMigrations(true)` 排除;视图实体跳过(GetViewName 判断);用户表列以手写 AddColumn 进迁移
- **表**:Department/Role/Permission/UserRole/RolePermission/AuditLog(英文表名);`用户管理` 加 5 列(PrimaryDepartmentId/Name、Position、IsLeader、PermissionVersion)
- **权限链路**:User → UserRole(IsActive) → Role(Enabled) → RolePermission → Permission(Enabled) → Code;缓存 key `rbac:permissions:{userId}`(IMemoryCache 单例),变更时 PermissionVersion+1 + Remove 缓存
- **TokenHelper**:新格式 `userId|userName|issued|expiry|version|sig`(6段),旧 4 段兼容;`TryValidateFull` 返回 TokenPayload;**expiry 索引:6段取索引3、4段取索引2**(曾误取倒数第2段=version 导致所有新令牌 401,已修复)
- **配置密钥取值约定(重要坑)**:读取 `Auth:Secret`/`Rbac:*` 等"可能为空串"的配置,回退默认值时**必须用 `string.IsNullOrWhiteSpace` 判断**,不能写 `?? 回退值`——`IConfiguration[index]` 在值为空串时返回 `""`(非 null),`??` 不触发回退,导致签名/校验密钥不一致(2026-08-10 修复 `AuthController`/`CurrentUserService`,与 `UserService` 取值逻辑须一致)
- **权限过滤器**:`[HasPermission(PermissionCodes.Xxx)]`(IAsyncActionFilter,Api/Attributes),401=AUTH_TOKEN_INVALID/403=AUTH_PERMISSION_DENIED;`Rbac:PermissionCheckEnabled` 仅开发环境可关闭(生产强制校验,2026-08-10 整改);空权限声明 Fail Close 拒绝
- **缓存失效**:`IPermissionCacheService`(scoped,2026-08-10 新增)统一封装"权限版本+1+清缓存",提供 ClearUserPermissionCacheAsync/ClearRolePermissionCacheAsync/ClearPermissionCacheAsync;Role/UserRole/Permission 服务均经它失效,版本修改随调用方 SaveChanges 同批提交
- **权限码同步**:`GET /api/identity/permissions/all` 免鉴权返回全部权限码(46个),供前端/CI 校验
- **权限字典变更**:`PUT /api/identity/permissions/{id}`(Identity.Permission.Update)启用/停用权限点+审计+清缓存
- **覆盖检查**:启动时扫描 Controller,无权限声明的接口输出 Warning(业务接口未接入阶段会大量告警属预期)
- **权限常量**:Application/Common/PermissionCodes.cs(47 个,与权限编码字典文档五方一致);新权限必须同步:字典文档+PermissionCodes+PermissionSeeder+前端
- **内置角色**:ADMIN/PMC_ADMIN/SCHEDULER/REVIEWER/OPERATOR/VIEWER(全大写 Code);RbacSeeder 启动时幂等初始化 + 历史 Role="Admin" 用户自动绑 ADMIN(防锁死)
- **登录默认角色兜底(重要行为)**:`UserService.LoginAsync`/`LoginByWeChatWorkAsync` 仅对**零角色**(UserRoles 无任何 `IsActive` 记录)用户调用 `EnsureUserHasRoleAsync` 绑定 `Rbac:DefaultLoginRole`(默认 VIEWER);**已有权限用户(如 ADMIN)解绑 VIEWER 后不会被自动加回**(2026-08-10 下午改,之前无条件兜底导致 admin 冗余带 VIEWER 且解绑后被重新塞回)。`EnsureUserHasRoleAsync` 内部须**复用已撤销 UserRole 记录**(查所有记录而非只查 `IsActive=true`),否则解绑后重新绑定会触发 `(UserId,RoleId)` 唯一索引 2601/2627 → 409
- **保护规则**:系统角色(IsSystem)禁删/禁禁用/禁清空权限;最后一个 ADMIN 用户不可被移除;部门软删除(IsActive=false)禁止物理删
- **DatabaseScripts/20260808_RbacTables.sql**:与 EF Migration 并存的幂等 SQL 兜底方案(无 EF 环境部署用),两者重复执行均无害
- **未接入**:PMC/ERP 业务接口权限(Task-012/013 后续阶段);前端权限(见 RBAC Vue3 规范文档)

## 部署目标(2026-08-10 用户确认)
- **目标服务器 OS**:Windows Server 2016/2019/2022(现代系统,**原生支持 .NET 10 Hosting Bundle**,本项目 net10.0 可直接部署,无需降级 TFM)。注:早期个人背景备忘笼统写的"Server 2008 + IIS 7"与此不符,**以此处确认为准**。
- **数据库位置**:SQL Server 与目标 Web 服务器**同机**,后端用 `localhost` 连接,无需开放 1433 远程端口;防火墙只需放行 IIS 站点端口(当前 90,或改 80/443)。
- **迁移方式**:FileSystem 发布到本地文件夹 → 拷贝到服务器 IIS 物理路径(`web.config` in-process 托管、应用池 .NET CLR=无托管代码);配置靠环境变量注入(`ConnectionStrings__DefaultConnection`/`Auth__Secret`/`Rbac__DefaultAdminPassword`/`WeChatWork__*`),`Cors__AllowedOrigins` 与 `WeChatWork__AllowedRedirectHosts` 须含服务器真实域名/IP。

## 前端部署(2026-08-11 新增)
- **前端项目**:`YCDataVue\yclt36-curve-viewer\`(Vue3+Vite5+TS,`npm run build`→`dist/`,`base:'./'`,**hash 路由,无需 web.config**)。
- **致命坑**:生产后端地址编译时写死在 `.env.production` 的 `VITE_API_BASE_URL`(原 `http://192.168.1.110:90`)。部署到 18 必须改成 `http://192.168.1.18:90`,推荐**构建时内联覆盖**(`VITE_API_BASE_URL=http://192.168.1.18:90 npm run build`,不改文件)。
- **18 部署布局**:后端 `F:\YCDataSystem\publish`(站点 `localDataApi` :90);前端 `F:\YCDataSystem\frontend`(站点 `yclt36-curve-viewer` :1001,独立静态站点)。
- **CORS**:后端 `CorsOrigins` 须含前端来源——域名 `http://www.ycdcf.com:1001`(已含)与直连 `http://192.168.1.18:1001`(需新增),改后 `Restart-WebAppPool localDataApi` 生效。
- **交付物**:`LocalDataApi\LocalDataApi\Deployment\Deploy-18-Frontend-Checklist.md`(部署清单) + `Deploy-Frontend-Iis.ps1`(镜像后端的建池/建站脚本,前端无机密注入故省略环境变量段)。
- **构建策略(用户定)**:在 110 构建后拷贝 dist 到 18,18 不装 Node。
