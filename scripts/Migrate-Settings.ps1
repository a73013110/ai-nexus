#requires -Version 7.4
param([string]$SettingsPath, [string]$SecretsPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
$taskPaths = Get-NexusLocalPaths
if (!$SettingsPath) { $SettingsPath = $taskPaths.Settings }
if (!$SecretsPath) { $SecretsPath = $taskPaths.Secrets }
$taskFiles = @($SettingsPath, $SecretsPath)
$taskOriginals = @()
foreach ($taskFile in $taskFiles) {
    if (!(Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw 'Settings file is missing. Initialize it from the corresponding example first.' }
    $taskValue = [IO.File]::ReadAllText($taskFile) | ConvertFrom-Json -AsHashtable
    if ($null -ne $taskValue.ConfigurationVersion -and $taskValue.ConfigurationVersion -notin @(1,2,3)) { throw 'Unsupported ConfigurationVersion. Migration stopped without writing settings.' }
    $taskOriginals += $taskValue
}
$taskGeneral = ConvertTo-NexusV3 ($taskOriginals[0] | ConvertTo-Json -Depth 40 | ConvertFrom-Json -AsHashtable)
$taskPrivate = ConvertTo-NexusV3 ($taskOriginals[1] | ConvertTo-Json -Depth 40 | ConvertFrom-Json -AsHashtable)
$taskSensitivePaths = @('Database.User','Database.Password','AdAuthentication.DnPass','Inference.Providers.Google.ApiKey','Tools.WebSearch.ApiKey','ConnectionStrings.Nexus','ConnectionStrings.LegacyGdweb','ConnectionStrings.LegacyMeiho')
foreach ($taskSource in @('Gdweb','Meiho')) { $taskSensitivePaths += "Integrations.Sources.$taskSource.Database.User", "Integrations.Sources.$taskSource.Database.Password" }
foreach ($taskPath in $taskSensitivePaths) {
    $taskFound = Get-NexusSetting $taskGeneral $taskPath
    if ($null -eq $taskFound) { continue }
    $taskExisting = Get-NexusSetting $taskPrivate $taskPath
    if (![string]::IsNullOrEmpty([string]$taskFound)) {
        if (![string]::IsNullOrEmpty([string]$taskExisting) -and $taskExisting -cne $taskFound) { throw "Conflicting values at $taskPath. Resolve in your editor; neither file was changed and values were not printed." }
        Set-NexusSetting $taskPrivate $taskPath $taskFound
    }
    Remove-NexusSetting $taskGeneral $taskPath
}
# Back up BOTH original files before either replacement, preserving their original version and ACLs.
foreach ($taskIndex in 0,1) {
    $taskFile = $taskFiles[$taskIndex]
    $taskNormalized = if ($taskIndex -eq 0) { $taskGeneral } else { $taskPrivate }
    if (($taskOriginals[$taskIndex] | ConvertTo-Json -Depth 40 -Compress) -cne ($taskNormalized | ConvertTo-Json -Depth 40 -Compress)) {
        $taskOriginalVersion = if ($taskOriginals[$taskIndex].ConfigurationVersion) { $taskOriginals[$taskIndex].ConfigurationVersion } else { 1 }
        $taskBackup = $taskFile + '.v' + $taskOriginalVersion + '-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '.bak'
        [IO.File]::WriteAllText($taskBackup, '')
        Set-Acl -LiteralPath $taskBackup -AclObject (Get-Acl -LiteralPath $taskFile)
        [IO.File]::WriteAllBytes($taskBackup, [IO.File]::ReadAllBytes($taskFile))
    }
}
Save-NexusJson $SecretsPath $taskPrivate
Save-NexusJson $SettingsPath $taskGeneral
$taskPrivate = $taskGeneral = $taskOriginals = $taskFound = $taskExisting = $null
Write-Output 'Settings migrated to v3 and formatted in canonical order. Secrets are isolated, existing values and ACLs were preserved; no values were printed.'
