#requires -Version 7.4
param(
    # An app directory in a NEW release package, not a running IIS directory.
    [string]$DestinationPath,
    [switch]$IncludeLocalConfig,
    [ValidateSet('Production','Staging')][string]$Environment = 'Production',
    [switch]$AllowUntrustedSql,
    [switch]$SkipBuild,
    [string]$PublishDirectory = 'artifacts/publish'
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
$taskPublish = [IO.Path]::GetFullPath($PublishDirectory, $taskRoot)
if (!$taskPublish.StartsWith((Join-Path $taskRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'PublishDirectory must be inside workspace artifacts/.' }
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $taskPublish }
if (!(Test-Path -LiteralPath (Join-Path $taskPublish 'AiNexus.Host.dll'))) { throw 'Run Build.ps1 first.' }
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
    if (!(Test-Path -LiteralPath $taskSettings)) { Copy-Item -LiteralPath (Join-Path $taskRoot 'backend/src/AiNexus.Host/appsettings.Production.example.json') -Destination $taskSettings }
    if (!(Test-Path -LiteralPath $taskSecrets)) { Copy-Item -LiteralPath (Join-Path $taskRoot 'backend/src/AiNexus.Host/appsettings.Secrets.example.json') -Destination $taskSecrets; Protect-NexusSecrets $taskSecrets }
}
$taskValue = [IO.File]::ReadAllText($taskSettings) | ConvertFrom-Json -AsHashtable
if ([string]::IsNullOrWhiteSpace([string]$taskValue.Attachments.StoragePath)) {
    Set-NexusSetting $taskValue 'Attachments.StoragePath' 'D:\CoreProject\AiNexus\data\attachments'
}
if ([string]::IsNullOrWhiteSpace([string]$taskValue.Diagnostics.Directory)) {
    Set-NexusSetting $taskValue 'Diagnostics.Directory' 'D:\CoreProject\AiNexus\data\diagnostics'
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
foreach ($taskDirectory in @('db', 'docs', 'deploy/iis', 'contracts')) {
    $taskTarget = Join-Path $taskPackage $taskDirectory
    New-Item -ItemType Directory -Path $taskTarget -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskDirectory) -File | Copy-Item -Destination $taskTarget
}
$taskTools = Join-Path $taskPackage 'scripts'
New-Item -ItemType Directory -Path $taskTools -Force | Out-Null
foreach ($taskTool in @('Migrate-Settings.ps1', 'Local-Settings.ps1', 'Settings-Schema.ps1', 'settings-layout.json', 'Verify-IIS.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskTool) -Destination $taskTools
}
# Copy bounded verification evidence only, never machine logs, credentials or test databases.
$taskEvidence = Join-Path $taskPackage 'artifacts'
New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
foreach ($taskReport in @('diagnostics-performance.json', 'browser-results.json')) {
    $taskReportPath = Join-Path $taskRoot ('artifacts/' + $taskReport)
    if (Test-Path -LiteralPath $taskReportPath -PathType Leaf) { Copy-Item -LiteralPath $taskReportPath -Destination $taskEvidence }
}
$taskEvidenceFiles = @{
    'diagnostic-acceptance' = @('result.json', 'safe-export.csv', 'chat-safe-error.png', 'admin-masked-detail.png', 'detail-and-timeline.png', 'admin-dark.png', 'admin-mobile.png', 'query-failure-and-degraded.png')
    'test-results' = @('backend.trx', 'diagnostics.trx', 'diagnostic-browser.trx', 'diagnostic-performance.trx')
}
foreach ($taskEvidenceDirectory in $taskEvidenceFiles.Keys) {
    $taskEvidenceSource = Join-Path $taskRoot ('artifacts/' + $taskEvidenceDirectory)
    if (Test-Path -LiteralPath $taskEvidenceSource -PathType Container) {
        $taskEvidenceTarget = Join-Path $taskEvidence $taskEvidenceDirectory
        New-Item -ItemType Directory -Path $taskEvidenceTarget -Force | Out-Null
        foreach ($taskEvidenceFile in $taskEvidenceFiles[$taskEvidenceDirectory]) {
            $taskEvidencePath = Join-Path $taskEvidenceSource $taskEvidenceFile
            if (Test-Path -LiteralPath $taskEvidencePath -PathType Leaf) { Copy-Item -LiteralPath $taskEvidencePath -Destination $taskEvidenceTarget }
        }
    }
}
Write-Output "IIS release package: $taskPackage"
Write-Output 'Only app/ is the IIS physical path. Preserve live config/, keys/, data/attachments and data/diagnostics during updates. Create both external data directories and set their application-pool Modify ACL before first start; see deploy/iis/README.md and docs/DIAGNOSTICS.md. The package logs/ directory is for ANCM stdout only.'
