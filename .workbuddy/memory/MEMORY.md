# LocalDataApi 项目长期备忘

## 项目约束(重构/开发时必须遵守)
- **前端接口契约零影响**:JSON 序列化 `PropertyNamingPolicy=null`(中文属性名原样输出)、`ApiResponse{Success,Message,Data,Timestamp}`、`PagedResult{Items,Total,Page,PageSize}` 均不可变
- **遗留 ERP 库**:SQL Server 中文表名/列名,实体为贫血数据载体;实体 `编号` 主键为 string;数量/日期多为 string 存储(需 ParseDouble 兼容)
- **PMC 控制器路由**:拆分后多个控制器均显式 `[Route("api/PMC")]`,端点路径不得与旧版(见 git HEAD)冲突
- **业务异常**:统一抛 `LocalDataApi.Application.Common` 的 ValidationException/NotFoundException/ConflictException,由全局中间件转 HTTP 状态码,Controller 不再各自 try-catch
- **依赖方向**:Api → Application → Domain ← Infrastructure,禁止反向引用

## 架构(2026-08-05 重构后)
- `Api/` 控制器 + GlobalExceptionMiddleware + Program.cs
- `Application/`:Blf、Identity、Erp、Ppc(6 个 Pmc*Service,接口在 Contracts/)、WeChatWork(5 个服务 + WechatWorkServiceBase)
- `Domain/`:实体按 Blf/Erp/Identity/Ppc/WeChatWork 子域;Common 下有 IRepository/IUnitOfWork 抽象(暂未接入实现)
- `Infrastructure/`:Data(AppDbContext、Migrations)、WeChatWork(WechatWorkTokenProvider,单例)
- 保留:`Dto/`(前端契约)、`Utils/`(PagingExtensions、QueryBatchExtensions、TokenHelper 等)、`WeChatWork/WechatWorkMessageType.cs`
- 说明:PmcBomService 内含排产分析单号生成逻辑(SaveSchedulingAnalysisAsync),因 Scheduling 服务反向依赖其 GetBomByItemNo,合置避免循环依赖

## 已知遗留(后续优化候选)
- 鉴权未覆盖:大部分接口无 [Authorize];自定义 TokenHelper 无过期校验
- ERP 密码明文比对(遗留库约束);生产部署需配置 Auth:Secret 与 WechatWork 配置
- WeChatWorkLoginService 已移除(未接入任何接口的死代码);企业微信 OAuth 能力由 WechatWorkUserService 承担
