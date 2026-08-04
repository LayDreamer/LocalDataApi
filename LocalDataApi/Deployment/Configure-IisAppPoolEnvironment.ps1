[CmdletBinding()]
param(
    [string]$AppPoolName = 'localDataApi',
    [switch]$Recycle
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

$connectionString = ConvertFrom-SecureValue (Read-Host 'Database connection string' -AsSecureString)
$corpId = Read-RequiredText 'WeChat Work CorpId'
$agentSecret = ConvertFrom-SecureValue (Read-Host 'WeChat Work AgentSecret' -AsSecureString)
$agentId = Read-RequiredText 'WeChat Work AgentId'
$redirectUri = Read-RequiredText 'WeChat Work RedirectUri'

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
    'Performance__DatabaseConcurrency'      = '64'
    'Performance__DatabaseQueue'            = '256'
}

$administrationAssembly = Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
Add-Type -Path $administrationAssembly

$serverManager = New-Object Microsoft.Web.Administration.ServerManager
try {
    $configuration = $serverManager.GetApplicationHostConfiguration()
    $section = $configuration.GetSection('system.applicationHost/applicationPools')
    $pools = $section.GetCollection()
    $pool = $pools | Where-Object { $_.GetAttributeValue('name') -eq $AppPoolName } | Select-Object -First 1
    if (-not $pool) {
        throw "IIS application pool not found: $AppPoolName"
    }

    $environmentVariables = $pool.GetCollection('environmentVariables')
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

Write-Host "Configured $($values.Count) settings for IIS application pool '$AppPoolName'."

if ($Recycle) {
    Import-Module WebAdministration
    Restart-WebAppPool -Name $AppPoolName
    Write-Host "Recycled IIS application pool '$AppPoolName'."
}
else {
    Write-Host "The application pool was not recycled. Settings take effect after its next start or recycle."
}
