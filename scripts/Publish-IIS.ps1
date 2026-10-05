#requires -Version 7.4
param(
    # An app directory in a NEW release package, not a running IIS directory.
    [string]$DestinationPath,
    [switch]$IncludeLocalConfig,
    [ValidateSet('Production','Staging')][string]$Environment = 'Production',
    [switch]$AllowUntrustedSql,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$taskPublish = Join-Path $taskRoot 'artifacts/publish'
if (!(Test-Path -LiteralPath (Join-Path $taskPublish 'AiNexus.Api.dll'))) { throw 'Run Build.ps1 first.' }
if (!$DestinationPath) { $DestinationPath = Join-Path $taskRoot ('artifacts/iis/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '/app') }
$taskApp = [IO.Path]::GetFullPath($DestinationPath)
if ((Test-Path -LiteralPath $taskApp) -and @(Get-ChildItem -LiteralPath $taskApp -Force).Count) {
    throw 'DestinationPath must be an empty release-package app directory. Follow deploy/iis/README.md to replace a running application safely.'
}
$taskPackage = Split-Path $taskApp
foreach ($taskPath in @($taskApp, (Join-Path $taskPackage 'config'), (Join-Path $taskPackage 'keys'), (Join-Path $taskPackage 'logs'))) {
    New-Item -ItemType Directory -Path $taskPath -Force | Out-Null
}
Get-ChildItem -LiteralPath $taskPublish | Where-Object { $_.Name -notin @('logs','App_Data') } | Copy-Item -Destination $taskApp -Recurse
$taskSettings = Join-Path $taskPackage "config/appsettings.$Environment.json"
$taskSecrets = Join-Path $taskPackage 'config/appsettings.Secrets.json'
if ($IncludeLocalConfig) {
    Initialize-NexusLocalSettings
    $taskLocal = Get-NexusLocalPaths
    if (!(Test-Path -LiteralPath $taskSettings)) { Copy-Item -LiteralPath $taskLocal.Settings -Destination $taskSettings }
    if (!(Test-Path -LiteralPath $taskSecrets)) { Copy-Item -LiteralPath $taskLocal.Secrets -Destination $taskSecrets; Protect-NexusSecrets $taskSecrets }
} else {
    if (!(Test-Path -LiteralPath $taskSettings)) { Copy-Item -LiteralPath (Join-Path $taskRoot 'backend/src/AiNexus.Api/appsettings.Production.example.json') -Destination $taskSettings }
    if (!(Test-Path -LiteralPath $taskSecrets)) { Copy-Item -LiteralPath (Join-Path $taskRoot 'backend/src/AiNexus.Api/appsettings.Secrets.example.json') -Destination $taskSecrets; Protect-NexusSecrets $taskSecrets }
}
$taskValue = [IO.File]::ReadAllText($taskSettings) | ConvertFrom-Json -AsHashtable
if ([string]::IsNullOrWhiteSpace([string]$taskValue.Attachments.StoragePath)) {
    Set-NexusSetting $taskValue 'Attachments.StoragePath' 'D:\CoreProject\AiNexus\data\attachments'
}
if ($AllowUntrustedSql) { Set-NexusSetting $taskValue 'Database.TrustServerCertificate' $true }
Save-NexusJson $taskSettings $taskValue
Save-NexusJson $taskSecrets ([IO.File]::ReadAllText($taskSecrets) | ConvertFrom-Json -AsHashtable)
$taskValue = $null
[xml]$taskWeb = [IO.File]::ReadAllText((Join-Path $taskApp 'web.config'))
$taskWeb.SelectSingleNode("//environmentVariable[@name='ASPNETCORE_ENVIRONMENT']").SetAttribute('value', $Environment)
$taskWeb.SelectSingleNode("//environmentVariable[@name='LocalConfigPath']").SetAttribute('value', "..\config\appsettings.$Environment.json")
$taskWeb.Save((Join-Path $taskApp 'web.config'))
Copy-Item -LiteralPath (Join-Path $taskRoot 'scripts/Verify-IIS.ps1') -Destination $taskPackage
Write-Output "IIS release package: $taskPackage"
Write-Output 'Only app/ is the IIS physical path. Preserve live config/, keys/ and data/attachments during updates. Create the external attachment directory and set its application-pool Modify ACL before first start; see deploy/iis/README.md.'
