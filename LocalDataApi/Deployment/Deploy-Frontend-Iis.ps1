[CmdletBinding()]
param(
    [string]$SiteName = 'yclt36-curve-viewer',
    [string]$AppPoolName = 'yclt36-curve-viewer',
    [int]$SitePort = 1001,
    [string]$SitePath = 'F:\YCDataSystem\frontend',
    [switch]$NoCreate,
    [switch]$NoVerify
)

$ErrorActionPreference = 'Stop'

# 必须以管理员运行
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator.'
}

Import-Module WebAdministration -ErrorAction Stop

# 注:前端为纯静态文件(hash 路由 + base:'./'),无需 web.config 重写规则,
# 也无需注入任何机密环境变量,故本脚本只做"建应用池 + 建站点 + 启动 + 自检"。

# ===== 1. 物理目录 =====
if (-not (Test-Path $SitePath)) {
    if ($NoCreate) { throw "站点目录不存在且指定了 -NoCreate: $SitePath" }
    New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
    Write-Host "已创建站点目录: $SitePath"
}

# ===== 2. 应用池(无托管代码 / 集成模式) =====
$pool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue
if (-not $pool) {
    if ($NoCreate) { throw "应用池不存在且指定了 -NoCreate: $AppPoolName" }
    New-WebAppPool -Name $AppPoolName | Out-Null
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value Integrated
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
    $binding = $site.Bindings.Collection | Where-Object { $_.Protocol -eq 'http' } | Select-Object -First 1
    $expectedPortBinding = ':' + $SitePort + ':'
    if ($binding -and ($binding.BindingInformation -notmatch $expectedPortBinding)) {
        Write-Warning "现有站点绑定为 '$($binding.BindingInformation)',与期望端口 $SitePort 不一致,请人工核对。"
    }
}

# ===== 4. 启动站点 + 回收池 =====
try { Start-Website -Name $SiteName -ErrorAction SilentlyContinue } catch { }
Restart-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Write-Host "已启动站点 '$SiteName' 并回收应用池。"

# ===== 5. 检查前端入口文件(index.html) =====
$indexFile = Join-Path $SitePath 'index.html'
if (-not (Test-Path $indexFile)) {
    Write-Warning "未找到 $indexFile —— 站点根目录缺少 index.html，请先把 dist 全部内容拷贝到 $SitePath，否则访问会 403/404。"
}

# ===== 6. 自检 =====
if (-not $NoVerify) {
    Start-Sleep -Seconds 2
    $url = "http://localhost:$SitePort/"
    try {
        $resp = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        Write-Host "自检成功: $url 返回 HTTP $($resp.StatusCode) (前端首页可访问)"
    }
    catch {
        Write-Warning "自检未能连接到 $url ($($_.Exception.Message))。请检查:端口 $SitePort 是否被占用、物理路径 $SitePath 是否含 index.html、18 本机防火墙是否放通 $SitePort。"
    }
}

Write-Host "`n完成。请确认后端 CORS 已放通本前端来源(见 Deploy-18-Frontend-Checklist.md 步骤三),再从浏览器访问 http://192.168.1.18:$SitePort 验证登录与接口。"
