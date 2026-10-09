#requires -Version 7.4
param([string]$AppPath = 'D:\CoreProject\AiNexus\app', [string]$AppPool, [string]$BaseUrl)
$ErrorActionPreference = 'Stop'
$taskApp = [IO.Path]::GetFullPath($AppPath)
$taskChecks = [Collections.Generic.List[object]]::new()
function Add-NexusCheck([string]$Name, [bool]$Passed, [string]$Detail) { $taskChecks.Add([pscustomobject]@{ Check = $Name; Passed = $Passed; Detail = $Detail }) }
foreach ($taskFile in @('AiNexus.Host.dll','AiNexus.Host.runtimeconfig.json','appsettings.json','web.config','wwwroot/index.html')) {
    Add-NexusCheck $taskFile (Test-Path -LiteralPath (Join-Path $taskApp $taskFile) -PathType Leaf) 'Required published file'
}
if (!(Test-Path -LiteralPath (Join-Path $taskApp 'web.config'))) { $taskChecks | Format-Table -AutoSize; exit 1 }
[xml]$taskXml = [IO.File]::ReadAllText((Join-Path $taskApp 'web.config'))
$taskAncm = $taskXml.SelectSingleNode('//aspNetCore')
$taskEnv = @{}
foreach ($taskNode in $taskAncm.SelectNodes('environmentVariables/environmentVariable')) { $taskEnv[$taskNode.name] = $taskNode.value }
Add-NexusCheck 'Production' ($taskEnv.ASPNETCORE_ENVIRONMENT -eq 'Production') 'Use Production even with a self-signed SQL certificate'
Add-NexusCheck 'In-process' ($taskAncm.hostingModel -eq 'inprocess') 'One dedicated app pool'
Add-NexusCheck 'Stdout off' ($taskAncm.stdoutLogEnabled -eq 'false') 'Enable only temporarily for startup diagnosis'
foreach ($taskPair in @(@('LocalConfigPath','..\config\appsettings.Production.json'), @('SecretsConfigPath','..\config\appsettings.Secrets.json'))) {
    $taskRelative = if ($taskEnv[$taskPair[0]]) { $taskEnv[$taskPair[0]] } else { $taskPair[1] }
    $taskFile = [IO.Path]::GetFullPath($taskRelative, $taskApp)
    Add-NexusCheck $taskPair[0] (Test-Path -LiteralPath $taskFile -PathType Leaf) $taskFile
    if (Test-Path -LiteralPath $taskFile) {
        $taskSettings = [IO.File]::ReadAllText($taskFile) | ConvertFrom-Json -AsHashtable
        Add-NexusCheck ($taskPair[0] + ' v3') ($taskSettings.ConfigurationVersion -eq 3) 'Migrate both external settings files before starting this release'
        if ($taskPair[0] -eq 'LocalConfigPath') {
            Add-NexusCheck 'AllowedHosts configured' ([bool]$taskSettings.AllowedHosts -and $taskSettings.AllowedHosts -notmatch 'company\.internal') 'Use your actual IIS DNS host name (without scheme or port)'
            $taskAttachmentSetting = if ($taskEnv.Attachments__StoragePath) { $taskEnv.Attachments__StoragePath } else { $taskSettings.Attachments.StoragePath }
            $taskAttachmentAbsolute = ![string]::IsNullOrWhiteSpace([string]$taskAttachmentSetting) -and [IO.Path]::IsPathFullyQualified([string]$taskAttachmentSetting)
            Add-NexusCheck 'Attachment path absolute' $taskAttachmentAbsolute 'Attachments__StoragePath overrides the external JSON setting'
            if ($taskAttachmentAbsolute) {
                $taskAttachmentPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($taskAttachmentSetting))
                $taskAppCanonical = [IO.Path]::TrimEndingDirectorySeparator($taskApp)
                $taskOutside = !$taskAttachmentPath.Equals($taskAppCanonical, [StringComparison]::OrdinalIgnoreCase) -and !$taskAttachmentPath.StartsWith($taskAppCanonical + '\', [StringComparison]::OrdinalIgnoreCase)
                Add-NexusCheck 'Attachments outside app' $taskOutside $taskAttachmentPath
                Add-NexusCheck 'Attachment directory exists' (Test-Path -LiteralPath $taskAttachmentPath -PathType Container) 'VerifyDeployment additionally tests read/write/delete under the invoking identity; validate IIS ACLs separately'
            }
            $taskDiagnosticSetting = if ($taskEnv.Diagnostics__Directory) { $taskEnv.Diagnostics__Directory } elseif ($taskSettings.Diagnostics.Directory) { $taskSettings.Diagnostics.Directory } else { Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'AiNexus/diagnostics' }
            $taskDiagnosticAbsolute = [IO.Path]::IsPathFullyQualified([string]$taskDiagnosticSetting) -and !([string]$taskDiagnosticSetting).StartsWith('\\')
            Add-NexusCheck 'Diagnostics path host-local absolute' $taskDiagnosticAbsolute 'Diagnostics__Directory overrides external JSON; empty uses ProgramData'
            if ($taskDiagnosticAbsolute) {
                $taskDiagnosticPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($taskDiagnosticSetting))
                $taskAppCanonical = [IO.Path]::TrimEndingDirectorySeparator($taskApp)
                $taskOutside = !$taskDiagnosticPath.Equals($taskAppCanonical, [StringComparison]::OrdinalIgnoreCase) -and !$taskDiagnosticPath.StartsWith($taskAppCanonical + '\', [StringComparison]::OrdinalIgnoreCase)
                Add-NexusCheck 'Diagnostics outside app' $taskOutside $taskDiagnosticPath
                Add-NexusCheck 'Diagnostics directory exists' (Test-Path -LiteralPath $taskDiagnosticPath -PathType Container) 'App-pool Modify ACL and disk capacity require separate verification'
                $taskPhysical = $true
                for ($taskDirectory = [IO.DirectoryInfo]::new($taskDiagnosticPath); $null -ne $taskDirectory; $taskDirectory = $taskDirectory.Parent) {
                    if ($taskDirectory.Exists -and ($taskDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $taskPhysical = $false; break }
                }
                Add-NexusCheck 'Diagnostics physical path' $taskPhysical 'No junction or symlink in the journal path'
            }
        }
        $taskSettings = $null
    }
}
$taskKeyPath = [IO.Path]::GetFullPath($(if ($taskEnv.DataProtection__KeyRingPath) { $taskEnv.DataProtection__KeyRingPath } else { '..\keys' }), $taskApp)
Add-NexusCheck 'Persistent keys' (Test-Path -LiteralPath $taskKeyPath -PathType Container) $taskKeyPath
Add-NexusCheck 'Config outside wwwroot' (!(Test-Path -LiteralPath (Join-Path $taskApp 'wwwroot/appsettings.Secrets.json'))) 'Neither config nor keys may be web-accessible'
if ($AppPool) {
    $taskAppCmd = Join-Path $env:windir 'System32/inetsrv/appcmd.exe'
    foreach ($taskPair in @(@('managedRuntimeVersion',''), @('enable32BitAppOnWin64','false'), @('processModel.loadUserProfile','true'), @('processModel.maxProcesses','1'), @('processModel.idleTimeout','00:00:00'), @('startMode','AlwaysRunning'), @('recycling.disallowOverlappingRotation','true'))) {
        $taskResult = & $taskAppCmd list apppool $AppPool ("/text:" + $taskPair[0])
        Add-NexusCheck $taskPair[0] ($LASTEXITCODE -eq 0 -and ([string]$taskResult).Trim() -eq $taskPair[1]) ('Expected: ' + $taskPair[1])
    }
}
if ($BaseUrl) {
    if (![Uri]::IsWellFormedUriString($BaseUrl, [UriKind]::Absolute) -or ([Uri]$BaseUrl).Scheme -ne 'https') { throw 'BaseUrl must be an HTTPS URL.' }
    try {
        $taskResponse = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/health/live') -TimeoutSec 15
        Add-NexusCheck 'HTTPS live' ($taskResponse.StatusCode -eq 200) 'Liveness does not imply SQL or AD readiness'
        $taskSession = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/v1/auth/session') -TimeoutSec 15
        Add-NexusCheck 'AD session configured' ([bool]$taskSession.configured) 'The session endpoint is accessible; validate an actual login in the browser'
        $taskSession = $null
    } catch { Add-NexusCheck 'HTTPS access' $false 'Check DNS, HTTPS binding, site start and the Windows event log' }
}
$taskChecks | Format-Table -AutoSize
if (@($taskChecks | Where-Object { !$_.Passed }).Count) { exit 1 }
Write-Output 'Static checks passed. Separately verify SQL readiness, application-pool ACLs, real AD login and streaming as documented in deploy/iis/README.md.'
