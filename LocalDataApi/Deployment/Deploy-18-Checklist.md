# LocalDataApi 服务器端一键部署清单

> 在 **192.168.1.18 本机**（已装 WorkBuddy）执行。发布产物位于 `F:\YCDataSystem\publish`。

## 前置

- 已安装 .NET 10 ASP.NET Core Hosting Bundle（缺则脚本会警告，需先装）。
- `F:\YCDataSystem\publish` 已含 `LocalDataApi.dll` + `web.config`。

## 一键部署（零提示）

1. `deploy.settings.json` 已从现有服务器（110）的 IIS 应用池回填 SQL 连接串与企微 AgentSecret，无需再填；如需变更可直接改文件。保存到 `F:\YCDataSystem\publish\deploy.settings.json`。
2. 管理员 PowerShell 运行（脚本在 `Deployment\` 目录，配置文件会自动被同一目录或 `F:\YCDataSystem\publish` 发现）：

```powershell
cd <项目目录>\Deployment
.\Configure-IisAppPoolEnvironment.ps1 -SitePath F:\YCDataSystem\publish
```

脚本读取 `deploy.settings.json` 回填所有值，**全程不提示**；自动完成：建应用池（无托管代码/集成）→ 建站点（:90）→ 注入环境变量 → 回收池 → 自检登录接口。

> 也可不依赖配置文件，直接带参数跳过提示：`-ConnectionString / -CorpId / -AgentSecret / -AgentId / -RedirectUri / -AdminPassword / -CorsOrigins / -AuthSecret`。

## 验证

```powershell
Invoke-WebRequest http://localhost:90/api/identity/permissions/all -UseBasicParsing
```

返回 200 + 46 个权限码即成功。

## 回滚

```powershell
Stop-Website localDataApi; Stop-WebAppPool localDataApi
```

## 检查清单

- [ ] Hosting Bundle 已装
- [ ] `F:\YCDataSystem\publish` 含发布产物
- [ ] `deploy.settings.json` 已填好并放置（无占位值）
- [ ] 脚本运行无提示、自检通过
- [ ] 验证返回 200 + 46 个权限码

