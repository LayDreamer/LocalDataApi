# Sprint3 `Sys_User` 数值主键切换上下文交接

日期：2026-08-15  
项目：`LocalDataApi/LocalDataApi`  
状态：账号体系数值主键切换已完成、已应用至当前数据库，并完成核心闭环验证。  
适用对象：后续在新窗口继续开发、排障、发布或设计 Sprint4 的人员。

> 本文以当前代码与已执行数据库迁移为准。旧文档 `Sprint3_人员身份模型上下文交接_20260814.md` 记录的是切换前的过渡模型，不能再作为现行身份模型依据。

## 1. 一句话结论

平台账号的唯一运行时主键已经是 `dbo.Sys_User.Id bigint IDENTITY`。认证、JWT、角色、会话、员工绑定、部门负责人和新增日志关联均应使用 `long UserId`；旧表 `dbo.[用户管理]` 不再被应用读写，仅作为只读归档与历史追溯来源。

```text
Sys_User.Id (bigint，唯一平台账号主键)
 ├─ UserRole.UserId (bigint)
 ├─ AuthSession.UserId (bigint)
 ├─ Employee.UserId (bigint，可空、唯一过滤索引)
 ├─ Department.LeaderUserId (bigint，可空)
 ├─ Sys_UserExternalIdentity.UserId (bigint)
 └─ 各类日志.PlatformUserId (bigint，可空、无外键)

Sys_UserLegacyMap
 └─ LegacyUserId (原 [用户管理].Id 字符串) -> UserId (Sys_User.Id)
```

## 2. 当前冻结的身份与账号模型

### 2.1 `dbo.Sys_User`

`Sys_User` 是账号域的唯一主表，实体 `Domain/Identity/User.cs` 已映射至该表。

- `Id bigint IDENTITY`：唯一平台用户 ID，迁移时保留原 `用户管理.IdentityId` 数值；新建账号由数据库生成。
- `UserName` / `NormalizedUserName`：用户名与其 `ToUpperInvariant()` 规范化值；后者有唯一索引。
- `DisplayName`、`Email`、`PhoneNumber`：账号基础展示与联系方式。
- `Status tinyint`：`1=正常`、`2=禁用`、`3=归档`。
- 密码安全字段：`PasswordHash`、`PasswordSalt`、`PasswordAlgorithm`、`PasswordUpdatedAtUtc`、`MustChangePassword`。
- 登录安全字段：`LoginFailCount`、`LockoutEndUtc`、`LastLoginAtUtc`、`LastLoginIp`、`PermissionVersion`。
- 审计与并发字段：`CreatedAtUtc`、`UpdatedAtUtc`、`RowVersion`。

固定索引：`PK_Sys_User(Id)`、`UX_Sys_User_NormalizedUserName`、`IX_Sys_User_Status`；邮箱与手机号目前只用于检索，不设置唯一约束。

### 2.2 外部身份与历史映射

- `Sys_UserExternalIdentity`：`(Provider, ExternalSubject)` 唯一，企业微信使用 `Provider='WeChatWork'`。后续 LDAP、OIDC、钉钉等只能以该模型扩展，不能再向 `Sys_User` 增加供应商专用字段。
- `Sys_UserLegacyMap`：`LegacyUserId nvarchar(450)`（原 `用户管理.Id`）映射至 `UserId bigint`。仅供历史日志回填、追溯和维护窗口回滚使用；认证、授权、会话等运行时逻辑禁止依赖它。

### 2.3 人员与组织关系

- `Employee.UserId bigint NULL` 外键指向 `Sys_User.Id`；`UX_Employee_UserId` 为 `UserId IS NOT NULL` 的过滤唯一索引。因此一个账号最多绑定一个员工，一个员工可暂不绑定账号。
- `Department.LeaderUserId bigint NULL` 指向 `Sys_User.Id`。原企业微信负责人字符串仍保留为同步来源/归档字段，不能作为新的业务关联键。
- 绑定、解绑由 `IEmployeeAccountService` / `EmployeeAccountService` 完成；控制器为 `EmployeeAccountController`：
  - `POST /api/identity/employees/{id}/bind-user`
  - `POST /api/identity/employees/{id}/unbind-user`
  - `GET /api/identity/employees/{id}/account`
- 绑定/解绑权限：`Identity.Employee.BindUser`；查询权限复用 `Identity.User.View`。唯一键竞争需转换为 HTTP 409，审计失败不得回滚已经成功的绑定或解绑。

## 3. 已完成的数据库迁移与实际基线

以下迁移已应用到当前目标数据库：

| Migration | 作用 |
| --- | --- |
| `20260815014530_SysUserNumericPrimaryKeyCutover` | 创建并接管 `Sys_User`、外部身份表与映射表；复制账号数据；切换角色、会话、员工、部门负责人和日志用户关联为数值键。 |
| `20260815024044_DropLegacyUserIdentityIdCounter` | 删除已不再使用的 `UserIdentityIdCounter`。禁止重新引入计数器分配逻辑。 |
| `20260815024709_MarkLegacyUserTableArchived` | 为旧 `用户管理` 建立归档标识和归档读取角色策略。 |

当前数据库已核验的关键结果：

- 原 `用户管理`、`Sys_User` 和 `Sys_UserLegacyMap` 各有 3 条账号数据，映射一一对应；迁移前的 `IdentityId` 与 `Sys_User.Id` 一致。
- 角色、会话、员工绑定、部门负责人均未发现无法映射的孤儿关系。
- 原存在的一条无法映射的 `UserRole` 关联已按确认策略撤销，并写入审计 `UserRole.RevokedForUserIdCutover`。
- 原有未撤销会话在切换时均以原因 `user-id-cutover` 撤销；旧 JWT 与旧会话不得被视为有效。
- `Employee.UserId` 唯一过滤索引、各数值外键、外部身份唯一索引均已存在。

## 4. 已完成的应用闭环验证

已使用有效测试账号完成以下实际回归，测试过程中临时改密后已恢复原密码：

- 密码登录成功。
- JWT 的 `sub` 与 `NameIdentifier` 为可严格解析的十进制 `long` 用户 ID。
- 已认证的 `/api/identity/me` 调用成功。
- 改密后旧密码失效，改密前令牌被撤销；恢复原密码后登录正常。
- 登出后令牌失效。
- 新增单元测试 `LocalDataApi.Tests/Identity/NumericUserIdentityTests.cs`，4 项全部通过，覆盖数值身份声明解析与关键模型类型约束。

尚未完成真实企业微信 OAuth 的端到端回归：该验证必须使用有效企业微信授权码、已配置的回调地址和实际测试企业账号。不能用本地模拟结果替代此验收。

## 5. 旧表归档与权限边界

`dbo.[用户管理]` 当前保留用于历史追溯，不应立即物理删除。删除前至少应完成一个完整发布周期的数据对账、企业微信登录回归和可执行回滚演练。

迁移已建立数据库角色 `db_SysUserLegacyArchiveReader`，该角色拥有旧表查询权限、被拒绝写入权限，并为旧表写入 `ArchivePolicy` 扩展属性。

**重要限制：应用当前仍以 `sa` 连接数据库。** `sa` 可绕过角色拒绝规则，因此“归档只读”尚不能由该角色对现有应用进程强制执行。后续应创建最小权限应用登录账号、切换连接字符串并验证后，才可认为归档写保护真正生效。部署脚本说明见：

`DatabaseScripts/20260815_SysUserLegacyArchivePermissions.sql`

## 6. 后续开发红线

1. 任何新实体、DTO、API 参数、缓存键、会话键、角色关联和权限上下文，一律使用 `long UserId`；不得新增字符串用户 ID 运行时接口。
2. JWT 只写入 `Sys_User.Id` 的十进制字符串；验证时严格解析为 `long`。解析失败、零值或负值必须拒绝。
3. 禁止恢复或新增 `IdentityId`、`UserIdentityIdCounter`，也不得把 `用户管理.Id` 重新作为认证/授权主键。
4. 外部平台身份统一使用 `Sys_UserExternalIdentity`；不得新增微信、LDAP、OIDC 等专用账号主表字段。
5. 新日志写 `PlatformUserId` 与展示名；不为日志字段建立用户外键，保留历史日志的旧字符串操作人字段。
6. 员工账号绑定必须经服务层并依赖数据库唯一过滤索引保证并发安全；不得直接绕过服务层写 `Employee.UserId`。
7. 离职员工处理和 `Sys_User.Status` / `IsActive` 联动尚不在本任务范围；未经过独立设计与迁移，不得自行添加自动禁用或自动解绑逻辑。
8. 不在 PMC、ERP 等历史业务表上新增本次账号体系相关 DDL；这些表不属于本次切换范围。

## 7. 发布、迁移与回滚规则

- 生产环境禁止应用启动自动执行 EF Migration；使用经过审核的幂等 SQL 和明确维护窗口发布。
- 数据迁移必须在写入冻结期间执行：登录、注册、改密、企业微信同步、角色分配、员工绑定均需暂停。
- 迁移 SQL 使用显式事务和 `XACT_ABORT ON`；发现数据异常必须阻断并人工修复，禁止自动补号、改名或丢弃账号。
- 开放流量前失败：可回退应用并恢复原关系；开放流量后回滚仅允许在维护窗口执行，必须先反向同步新增账号变更并完成映射校验。
- 每次相关变更至少执行：构建、迁移状态检查、模型待变更检查、数据对账、测试与 `git diff --check`。

## 8. 推荐的下一步工作

按优先级建议继续推进：

1. **切换数据库应用账号**：停止使用 `sa`，建立最小权限登录账号并验证旧 `用户管理` 表对应用端不可写。
2. **企业微信真实回归**：用测试企业账号完成登录、外部身份匹配/创建、人员绑定与部门负责人查询验证。
3. **旧表退役准备**：运行一个完整发布周期后再次对账；确定历史报表、外部集成和人工查询均已改用 `Sys_User` 或 `Sys_UserLegacyMap`，再拟定物理删除的独立变更单。
4. **Sprint4 业务开发**：可直接基于 `long UserId` 开展人员权限、组织职责、离职流程等功能；离职与账号状态联动需先单独评审。

## 9. 新窗口开始工作的最小检查清单

1. 阅读本文及平台规范：`永创制造数字化平台_平台基础能力冻结与开发规范V3.2.md`（文档内版本已更新为 V1.3）。
2. 查看 `Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` 与最近三条 Sys_User 迁移，确认模型基线。
3. 不假设旧表可删除，也不假设 `sa` 已被替换。
4. 若修改身份相关代码，先搜索 `UserId`、`Sys_User`、`Sys_UserExternalIdentity` 与 `Sys_UserLegacyMap`，再检查是否意外引用 `用户管理`、`IdentityId` 或旧字符串 `User.Id`。
5. 完成变更后运行身份单元测试，并在具备企业微信测试条件时补齐 OAuth 回归。

