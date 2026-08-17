# 企业微信前端 80 端口入口配置（18 服务器）

## 目标

前端 IIS 站点 `yclt36-curve-viewer` 保留现有 `:1001` 入口，并新增企业微信使用的 `www.ycdcf.com:80` 入口：

```text
*:1001:
*:80:www.ycdcf.com
```

企业微信应用主页保持不变：

```text
http://www.ycdcf.com/#/wechat-login
```

> 本文命令均在 `192.168.1.18` 的“以管理员身份运行”的 PowerShell 执行。

## 1. 前置检查

```powershell
Import-Module WebAdministration

Get-Website | Select-Object Name,State,PhysicalPath
Get-WebBinding | Select-Object protocol,bindingInformation
```

确认：

- `yclt36-curve-viewer` 站点状态为 `Started`。
- 前端物理路径为 `F:\YCDataSystem\dist`。
- 已存在 `*:1001:` 绑定。
- 没有其他站点使用 `*:80:www.ycdcf.com`。

`Default Web Site` 即使有 `*:80:` 绑定也可以保留；企业微信请求携带 `Host: www.ycdcf.com`，会匹配前端的带主机名绑定。

## 2. 添加 IIS 80 端口域名绑定

```powershell
$siteName = 'yclt36-curve-viewer'
$hostName = 'www.ycdcf.com'

$site = Get-Website -Name $siteName -ErrorAction Stop
$existing = Get-WebBinding -Name $siteName -Protocol 'http' |
  Where-Object { $_.bindingInformation -eq "*:80:$hostName" }

if (-not $existing) {
  New-WebBinding -Name $siteName -Protocol 'http' -Port 80 -HostHeader $hostName
}

Get-WebBinding -Name $siteName | Select-Object protocol,bindingInformation
```

预期结果包含：

```text
http  *:1001:
http  *:80:www.ycdcf.com
```

## 3. 放行 Windows 防火墙 TCP 80

```powershell
$ruleName = 'YCDataVue Frontend TCP 80'
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

if ($rule) {
  Set-NetFirewallRule -DisplayName $ruleName -Enabled True -Direction Inbound -Action Allow -Profile Domain,Private,Public
  Set-NetFirewallAddressFilter -AssociatedNetFirewallRule $rule -RemoteAddress LocalSubnet
} else {
  New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 80 `
    -Action Allow `
    -Profile Domain,Private,Public `
    -RemoteAddress LocalSubnet
}

Get-NetFirewallRule -DisplayName $ruleName | Get-NetFirewallPortFilter
Get-NetFirewallRule -DisplayName $ruleName | Get-NetFirewallAddressFilter
```

## 4. 增加后端 CORS 来源

`localDataApi` 需要允许企业微信页面的无端口来源：

```text
http://www.ycdcf.com
```

以下命令使用数组索引环境变量，保留 `:1001` 访问来源并新增无端口域名：

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
  'Cors__AllowedOrigins__2' = 'http://www.ycdcf.com'
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

## 5. 服务器验证

验证 IIS 根据域名请求返回前端首页：

```powershell
curl.exe -I -H "Host: www.ycdcf.com" http://127.0.0.1/
```

预期返回 `HTTP/1.1 200 OK`。

验证后端 CORS：

```powershell
$headers = @{
  Origin = 'http://www.ycdcf.com'
  'Access-Control-Request-Method' = 'POST'
  'Access-Control-Request-Headers' = 'content-type'
}

$response = Invoke-WebRequest `
  'http://192.168.1.18:90/api/Auth/login' `
  -Method Options `
  -Headers $headers `
  -UseBasicParsing

$response.StatusCode
$response.Headers['Access-Control-Allow-Origin']
```

预期输出：

```text
204
http://www.ycdcf.com
```

## 6. 客户端切换与企业微信验证

仅在第 5 步全部通过后，在每台需要访问系统的客户端 hosts 文件中设置：

```text
192.168.1.18 www.ycdcf.com
```

执行：

```powershell
ipconfig /flushdns
```

重新打开企业微信工作台中的应用，验证能进入：

```text
http://www.ycdcf.com/#/wechat-login
```

企业微信后台应用主页无需修改，也不要添加 `:1001`。

## 回滚

企业微信访问异常时，在 18 上删除仅新增的 80 域名绑定：

```powershell
Import-Module WebAdministration
Remove-WebBinding -Name 'yclt36-curve-viewer' -Protocol 'http' -Port 80 -HostHeader 'www.ycdcf.com'
```

将客户端 hosts 恢复为：

```text
192.168.1.110 www.ycdcf.com
```

现有 `:1001` 绑定和后端 `:90` 不受该回滚影响。
