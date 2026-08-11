# LocalDataApi 前端（yclt36-curve-viewer）部署到 192.168.1.18

## 目标与访问地址

- 前端：`192.168.1.18:1001`，IIS 站点名为 `yclt36-curve-viewer`。
- 后端：`192.168.1.18:90`，IIS 站点/应用池名为 `localDataApi`。
- 企业微信与用户统一访问：`http://www.ycdcf.com:1001/#/wechat-login`。
- `www.ycdcf.com` 是内网域名；所有客户端必须解析到 `192.168.1.18`。

> 本文中的服务器配置命令均在 **192.168.1.18 的管理员 PowerShell** 中执行。请先完成后端部署，并确认 `http://192.168.1.18:90` 可访问。

## 0. 切换前检查

在构建机（110）确认前端生产 API 指向 18：

```powershell
Get-Content .\yclt36-curve-viewer\.env.production
```

必须包含：

```text
VITE_API_BASE_URL=http://192.168.1.18:90
```

当前配置已是该值。不要将 API 指回 110。

## 1. 在 110 构建前端

在 `YCDataVue\yclt36-curve-viewer` 目录执行：

```powershell
npm ci
npm run build
```

> `VITE_API_BASE_URL=... npm run build` 是 Bash 写法，不能直接用于 Windows PowerShell。若临时覆盖 API 地址，请使用：

```powershell
$env:VITE_API_BASE_URL = 'http://192.168.1.18:90'
npm run build
Remove-Item Env:VITE_API_BASE_URL
```

构建完成后，确认产物不再包含旧后端地址：

```powershell
rg -n -a 'http://192\.168\.1\.110:90' .\dist
```

没有输出即为通过。

## 2. 将构建产物复制到 18

在 **18** 上执行；`/MIR` 会删除 18 目标目录中不属于当前构建的旧哈希文件：

```powershell
New-Item -ItemType Directory -Path 'F:\YCDataSystem\frontend' -Force
robocopy '\\192.168.1.110\D$\work2026\AICode\AI测试专用\YCDataVue\yclt36-curve-viewer\dist' 'F:\YCDataSystem\frontend' /MIR /R:2 /W:1
if ($LASTEXITCODE -gt 7) { throw "前端复制失败，Robocopy exit code: $LASTEXITCODE" }
Test-Path 'F:\YCDataSystem\frontend\index.html'
```

最后一条必须返回 `True`。若 18 无法访问 110 的 `D$` 管理员共享，请通过受控共享目录或离线介质复制 **dist 的全部内容**，不要把 `dist` 目录本身再嵌套一层。

## 3. 在 18 创建 IIS 前端站点

将 `Deploy-Frontend-Iis.ps1` 放到 18 后，以管理员身份执行：

```powershell
Set-Location '<脚本所在目录>'
.\Deploy-Frontend-Iis.ps1 -SiteName 'yclt36-curve-viewer' -AppPoolName 'yclt36-curve-viewer' -SitePort 1001 -SitePath 'F:\YCDataSystem\frontend'
```

脚本不会自动修正已有站点的错误绑定。无论脚本是否提示成功，都执行：

```powershell
Import-Module WebAdministration
Get-Website -Name 'yclt36-curve-viewer' | Select-Object Name,State,PhysicalPath
Get-WebBinding -Name 'yclt36-curve-viewer' | Select-Object protocol,bindingInformation
Invoke-WebRequest 'http://localhost:1001/' -UseBasicParsing
```

预期：站点状态为 `Started`，物理路径为 `F:\YCDataSystem\frontend`，HTTP 绑定为 `*:1001:`，首页返回 `200`。

## 4. 放行 18 的 Windows 防火墙

前端脚本只创建 IIS 站点，不会创建防火墙规则。在 18 上执行：

```powershell
$ruleName = 'YCDataVue Frontend TCP 1001'
if (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue) {
    Set-NetFirewallRule -DisplayName $ruleName -Enabled True -Direction Inbound -Action Allow -Profile Domain,Private,Public
    Set-NetFirewallAddressFilter -AssociatedNetFirewallRule (Get-NetFirewallRule -DisplayName $ruleName) -RemoteAddress LocalSubnet
} else {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 1001 -Action Allow -Profile Domain,Private,Public -RemoteAddress LocalSubnet
}
Get-NetFirewallRule -DisplayName $ruleName | Get-NetFirewallPortFilter
Get-NetFirewallRule -DisplayName $ruleName | Get-NetFirewallAddressFilter
```

> 规则限制为 `LocalSubnet`，不对外网开放。若服务器网络配置文件为 Public，上述规则仍会生效。

从另一台内网机器验证：

```powershell
Test-NetConnection 192.168.1.18 -Port 1001
Invoke-WebRequest 'http://192.168.1.18:1001/' -UseBasicParsing
```

`TcpTestSucceeded` 必须为 `True`，首页必须返回 `200`。

## 5. 后端 CORS

后端必须允许以下两个前端来源：

```text
http://www.ycdcf.com:1001
http://192.168.1.18:1001
```

`LocalDataApi` 将 `Cors:AllowedOrigins` 绑定为数组。因此不要把多个来源写入单个 `Cors__AllowedOrigins` 环境变量（例如用分号拼接）；应使用数组索引环境变量。以下命令会删除旧的标量变量、写入两个生产来源并回收应用池：

```powershell
Import-Module WebAdministration
$serverManager = New-Object Microsoft.Web.Administration.ServerManager
$pool = $serverManager.ApplicationPools['localDataApi']
if ($null -eq $pool) { throw '找不到 IIS 应用池 localDataApi' }

$variables = $pool.EnvironmentVariables
$oldValue = $variables['Cors__AllowedOrigins']
if ($null -ne $oldValue) { [void]$variables.Remove($oldValue) }

$required = @{
  'Cors__AllowedOrigins__0' = 'http://www.ycdcf.com:1001'
  'Cors__AllowedOrigins__1' = 'http://192.168.1.18:1001'
}
foreach ($item in $required.GetEnumerator()) {
  $variable = $variables[$item.Key]
  if ($null -eq $variable) {
    $variable = $variables.CreateElement('add')
    $variable['name'] = $item.Key
    [void]$variables.Add($variable)
  }
  $variable['value'] = $item.Value
}
$serverManager.CommitChanges()
Restart-WebAppPool -Name 'localDataApi'
```

从任意内网机器验证预检响应：

```powershell
$headers = @{
  Origin = 'http://www.ycdcf.com:1001'
  'Access-Control-Request-Method' = 'POST'
  'Access-Control-Request-Headers' = 'content-type'
}
$response = Invoke-WebRequest 'http://192.168.1.18:90/api/Auth/login' -Method Options -Headers $headers -UseBasicParsing
$response.StatusCode
$response.Headers['Access-Control-Allow-Origin']
```

预期输出：`204` 与 `http://www.ycdcf.com:1001`。

## 6. 域名、企业微信和切流

企业微信应用主页保持为：

```text
http://www.ycdcf.com:1001/#/wechat-login
```

企业微信“网页授权及 JS-SDK”的可信域名填写 `www.ycdcf.com`，不要填写协议、端口或路径。

验证 18 的前端和登录正常后，再将 **所有客户端**（包括运行企业微信的电脑）的 hosts 从：

```text
192.168.1.110 www.ycdcf.com
```

改为：

```text
192.168.1.18 www.ycdcf.com
```

若存在网络边缘端口转发，也改为：

```text
外部 :1001 → 192.168.1.18:1001
```

> 内网客户端通过 hosts/DNS 直连 18，不经过端口转发。企业微信 OAuth 使用域名回跳，因此不能只改成 IP 访问。

## 7. 最终验收

在客户端依次验证：

```text
http://192.168.1.18:1001/
http://www.ycdcf.com:1001/
```

- 两个地址均能打开登录页。
- 使用账号登录，浏览器 Network 中 API 请求均指向 `http://192.168.1.18:90`。
- 不出现 CORS 错误。
- 从企业微信工作台进入应用，能进入 `#/wechat-login` 并完成免登录。
- 刷新 `#/dashboard` 等 hash 路由，页面仍正常。

## 回滚

若 18 验证失败，先恢复网络入口，再停止 18 前端：

1. 将客户端 hosts 恢复为 `192.168.1.110 www.ycdcf.com`。
2. 若使用端口转发，恢复为旧规则（通常是 `:1001 → 192.168.1.110:80`；以网络设备当前备份为准）。
3. 企业微信应用主页保持域名地址不变；恢复 hosts/转发后会重新到达 110。
4. 在 18 上执行：

```powershell
Import-Module WebAdministration
Stop-Website -Name 'yclt36-curve-viewer'
Stop-WebAppPool -Name 'yclt36-curve-viewer'
```

## 安全注意事项

- 不要将数据库密码、企业微信密钥或管理员初始密码写入本文档。
- `deploy.settings.json` 如包含敏感信息，不应提交到 Git 或复制给客户端；应限制读取权限，并优先使用 IIS 环境变量或受控机密存储。
