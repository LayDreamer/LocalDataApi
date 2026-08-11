[CmdletBinding()]
param(
    [string]$AppPoolName = 'localDataApi',
    [string]$SiteName = 'localDataApi',
    [int]$SitePort = 90,
    [string]$SitePath = 'D:\IISWebSitefiles',
    [switch]$NoCreate,
    [switch]$NoVerify,
    [string]$ConnectionString,
    [string]$CorpId,
    [string]$AgentSecret,
    [string]$AgentId,
    [string]$RedirectUri,
    [string]$AdminPassword,
    [string]$CorsOrigins,
    [string]$AuthSecret,
    [string]$SettingsFile
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator.'
}

function Read-RequiredText {
    param([Parameter(Mandatory)][string]$Prompt)

    $value = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Prompt is required."
    }

    return $value
}

function ConvertFrom-SecureValue {
    param([Parameter(Mandatory)][Security.SecureString]$SecureValue)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function ConvertTo-Hashtable {
    param($obj)
    $ht = @{}
    if ($obj) { $obj.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value } }
    return $ht
}

# 校验/自动添加 18 本机 hosts 条目,供服务端自解析 www.ycdcf.com(前端域名)。
# 若已存在且指向本机 IP 则跳过;若指向其他 IP 则告警(不改,避免误删旧映射);若不存在则追加。
function Ensure-LocalHostsEntry {
    param([string]$HostName, [string]$Ip)
    $hostsPath = Join-Path $env:windir 'System32\drivers\etc\hosts'
    if (-not (Test-Path $hostsPath)) { Write-Warning "未找到 hosts 文件: $hostsPath"; return }
    $content = Get-Content $hostsPath -Raw
    $existing = [regex]::Matches($content, '(?m)^\s*(\S+)\s+' + [regex]::Escape($HostName) + '\b')
    if ($existing.Count -gt 0) {
        $mappedIp = $existing[0].Groups[1].Value
        if ($mappedIp -eq $Ip) {
            Write-Host "hosts 已含 '$Ip $HostName' (跳过)"
        }
        else {
            Write-Warning "hosts 中 '$HostName' 当前指向 '$mappedIp'(非 $Ip)。18 本机自解析将走旧地址;如需改请手动编辑 $hostsPath 删除旧行后重跑,或修改该行为 '$Ip $HostName'。"
        }
        return
    }
    try {
        Add-Content -Path $hostsPath -Value "`n$Ip $HostName   # LocalDataApi 部署脚本自动添加" -Encoding ascii -Force
        Write-Host "已向本机 hosts 追加 '$Ip $HostName'"
    }
    catch {
        Write-Warning "无法写入 hosts($hostsPath): $($_.Exception.Message)。请手动添加 '$Ip $HostName' 以便 18 本机解析域名。"
    }
}

# 读取本地配置(deploy.settings.json),回填现有数据,避免重复输入。优先级:参数 > 配置文件 > 提示
$settings = @{}
$candidates = @(
    $SettingsFile,
    (Join-Path $PSScriptRoot 'deploy.settings.json'),
    (Join-Path $SitePath 'deploy.settings.json')
)
foreach ($c in $candidates) {
    if ($c -and (Test-Path $c)) {
        try { $settings = ConvertTo-Hashtable (Get-Content $c -Raw | ConvertFrom-Json) }
        catch { Write-Warning "读取配置失败: $c ($($_.Exception.Message))" }
        Write-Host "已载入配置: $c"
        break
    }
}

function Get-Secret {
    param([string]$Param, [string]$Key, [string]$Prompt)
    if ($Param) { return $Param }
    if ($settings.ContainsKey($Key) -and $settings[$Key]) { return $settings[$Key] }
    return ConvertFrom-SecureValue (Read-Host $Prompt -AsSecureString)
}
function Get-Text {
    param([string]$Param, [string]$Key, [string]$Prompt)
    if ($Param) { return $Param }
    if ($settings.ContainsKey($Key) -and $settings[$Key]) { return $settings[$Key] }
    return Read-RequiredText $Prompt
}

$connectionString = Get-Secret -Param $ConnectionString -Key 'ConnectionString' -Prompt 'Database connection string'
$corpId = Get-Text -Param $CorpId -Key 'CorpId' -Prompt 'WeChat Work CorpId'
$agentSecret = Get-Secret -Param $AgentSecret -Key 'AgentSecret' -Prompt 'WeChat Work AgentSecret'
$agentId = Get-Text -Param $AgentId -Key 'AgentId' -Prompt 'WeChat Work AgentId'
$redirectUri = Get-Text -Param $RedirectUri -Key 'RedirectUri' -Prompt 'WeChat Work RedirectUri'

# ===== 2026-08-10 部署计划新增的机密注入 =====
$adminPassword = Get-Secret -Param $AdminPassword -Key 'AdminPassword' -Prompt 'Default admin password (Rbac)'
$corsOrigins = Get-Text -Param $CorsOrigins -Key 'CorsOrigins' -Prompt 'CORS allowed origins (semicolon-separated, e.g. http://www.ycdcf.com:1001;http://192.168.1.18:90)'

# 自动补齐前端来源(保证前后端跨域顺畅):前端部署在 :1001(见 Deploy-Frontend-Iis.ps1),
# 浏览器访问来源即 http://www.ycdcf.com:1001 或 http://192.168.1.18:1001,缺失则自动补入 CORS。
$requiredCors = @('http://www.ycdcf.com:1001', 'http://192.168.1.18:1001')
$corsList = if ($corsOrigins) { $corsOrigins -split ';' | Where-Object { $_.Trim() } } else { @() }
$added = @()
foreach ($o in $requiredCors) {
    if ($corsList -notcontains $o) { $corsList += $o; $added += $o }
}
$corsOrigins = $corsList -join ';'
if ($added.Count) { Write-Host "已为 CORS 自动补齐前端来源: $($added -join ', ')" }
# Auth__Secret:配置优先,否则自动生成强随机值(迁移时建议从旧站回填以保持 Token 有效)
if ($AuthSecret) {
    $authSecret = $AuthSecret
} elseif ($settings.ContainsKey('AuthSecret') -and $settings['AuthSecret']) {
    $authSecret = $settings['AuthSecret']
} else {
    $authSecret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'Database connection string is required.'
}
if ([string]::IsNullOrWhiteSpace($agentSecret)) {
    throw 'WeChat Work AgentSecret is required.'
}
if ($agentId -notmatch '^\d+$') {
    throw 'WeChat Work AgentId must be an integer.'
}

$values = [ordered]@{
    'ConnectionStrings__DefaultConnection' = $connectionString
    'WeChatWork__CorpId'                    = $corpId
    'WeChatWork__AgentSecret'               = $agentSecret
    'WeChatWork__AgentId'                   = $agentId
    'WeChatWork__RedirectUri'               = $redirectUri
    'Auth__Secret'                          = $authSecret
    'Rbac__DefaultAdminPassword'            = $adminPassword
    'Cors__AllowedOrigins'                  = $corsOrigins
    'ASPNETCORE_ENVIRONMENT'                = 'Production'
    'Performance__DatabaseConcurrency'      = '64'
    'Performance__DatabaseQueue'            = '256'
}

Import-Module WebAdministration -ErrorAction Stop

# ===== 0. 前置检查:ASP.NET Core Hosting Bundle (ANCM) =====
$ancm = Join-Path $env:windir 'System32\inetsrv\aspnetcore.dll'
if (-not (Test-Path $ancm)) {
    Write-Warning '未检测到 ASP.NET Core Module (aspnetcore.dll)。请先在服务器安装 .NET 10 ASP.NET Core Hosting Bundle,否则 in-process 托管会 500.19/500.21。'
    Write-Warning '下载地址: https://dotnet.microsoft.com/download/dotnet/10.0 (选择 ASP.NET Core Runtime -> Hosting Bundle)'
}
else {
    $ver = (Get-Item $ancm).VersionInfo.FileVersion
    Write-Host "检测到 ASP.NET Core Module 版本: $ver"
}

# ===== 1. 物理目录 + 日志目录 =====
if (-not (Test-Path $SitePath)) {
    if ($NoCreate) { throw "站点目录不存在且指定了 -NoCreate: $SitePath" }
    New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
    Write-Host "已创建站点目录: $SitePath"
}
$logDir = Join-Path $SitePath 'logs'
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    Write-Host "已创建日志目录: $logDir"
}

# ===== 2. 应用池(无托管代码 / 集成模式) =====
$pool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue
if (-not $pool) {
    if ($NoCreate) { throw "应用池不存在且指定了 -NoCreate: $AppPoolName" }
    New-WebAppPool -Name $AppPoolName | Out-Null
    # 无托管代码
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ''
    # 集成管道
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value Integrated
    # 自动启动 + 常驻
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name autoStart -Value $true
    Write-Host "已创建应用池 '$AppPoolName' (No Managed Code / Integrated / AlwaysRunning)"
}
else {
    Write-Host "应用池已存在: $AppPoolName (跳过创建)"
}

# ===== 3. 站点(:SitePort) =====
$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if (-not $site) {
    if ($NoCreate) { throw "站点不存在且指定了 -NoCreate: $SiteName" }
    New-Website -Name $SiteName -Port $SitePort -PhysicalPath $SitePath -ApplicationPool $AppPoolName | Out-Null
    Write-Host "已创建站点 '$SiteName' 绑定 *:$SitePort -> $SitePath"
}
else {
    Write-Host "站点已存在: $SiteName (跳过创建)"
    # 确保绑定端口正确
    $binding = $site.Bindings.Collection | Where-Object { $_.Protocol -eq 'http' } | Select-Object -First 1
    $expectedPortBinding = ':' + $SitePort + ':'
    if ($binding -and ($binding.BindingInformation -notmatch $expectedPortBinding)) {
        Write-Warning "现有站点绑定为 '$($binding.BindingInformation)',与期望端口 $SitePort 不一致,请人工核对。"
    }
}

# ===== 4. 注入应用池环境变量(机密) =====
$administrationAssembly = Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
Add-Type -Path $administrationAssembly

$serverManager = New-Object Microsoft.Web.Administration.ServerManager
try {
    $configuration = $serverManager.GetApplicationHostConfiguration()
    $section = $configuration.GetSection('system.applicationHost/applicationPools')
    $pools = $section.GetCollection()
    $poolElement = $pools | Where-Object { $_.GetAttributeValue('name') -eq $AppPoolName } | Select-Object -First 1
    if (-not $poolElement) {
        throw "IIS application pool not found: $AppPoolName"
    }

    $environmentVariables = $poolElement.GetCollection('environmentVariables')
    foreach ($entry in $values.GetEnumerator()) {
        $element = $environmentVariables | Where-Object { $_.GetAttributeValue('name') -eq $entry.Key } | Select-Object -First 1
        if (-not $element) {
            $element = $environmentVariables.CreateElement('add')
            $element.SetAttributeValue('name', $entry.Key)
            $element.SetAttributeValue('value', $entry.Value)
            [void]$environmentVariables.Add($element)
        }
        else {
            $element.SetAttributeValue('value', $entry.Value)
        }
    }

    $serverManager.CommitChanges()
}
finally {
    $connectionString = $null
    $agentSecret = $null
    $serverManager.Dispose()
}

Write-Host "已注入 $($values.Count) 项应用池环境变量到 '$AppPoolName'。"

# ===== 5. 启动站点 + 回收池(使环境变量生效) =====
try { Start-Website -Name $SiteName -ErrorAction SilentlyContinue } catch { }
Restart-WebAppPool -Name $AppPoolName
Write-Host "已回收应用池 '$AppPoolName' 并使站点上线。"

# ===== 5.5 校验 18 本机 hosts(供服务端自解析 www.ycdcf.com) =====
Ensure-LocalHostsEntry -HostName 'www.ycdcf.com' -Ip '192.168.1.18'

# ===== 6. 自检 =====
if (-not $NoVerify) {
    Start-Sleep -Seconds 4
    $url = "http://localhost:$SitePort/api/Auth/login"
    try {
        $resp = Invoke-WebRequest -Uri $url -Method Post -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        Write-Host "自检成功: $url 返回 HTTP $($resp.StatusCode)"
        Write-Host "响应前 200 字符: $($resp.Content.Substring(0, [Math]::Min(200, $resp.Content.Length)))"
    }
    catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status) {
            Write-Host "自检: 站点已响应 (HTTP $status)。请确认返回体是否符合预期 ApiResponse 结构。"
        }
        else {
            Write-Warning "自检未能连接到 $url ($($_.Exception.Message))。请检查:应用池是否运行、端口 $SitePort 是否被占用、web.config 是否正确、以及 18 本机防火墙是否放通 $SitePort。"
        }
    }
}

Write-Host "`n完成。下一步:用浏览器从客户端访问 http://192.168.1.18:$SitePort/api/Auth/login 验证;验证通过后停用 110 旧站点(localDataApi 应用池/站点)。"
