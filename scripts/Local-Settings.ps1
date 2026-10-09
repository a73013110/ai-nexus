. (Join-Path $PSScriptRoot 'Settings-Schema.ps1')

function Get-NexusLocalPaths {
    $taskWorkspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    return @{ Root = $taskWorkspace; Settings = Join-Path $taskWorkspace '.local/config/appsettings.Local.json'; Secrets = Join-Path $taskWorkspace '.local/secrets/appsettings.Secrets.json' }
}

function Protect-NexusSecrets([string]$Path) {
    $taskExistingAcl = Get-Acl -LiteralPath $Path
    $taskAllowedSids = @([System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    $taskExistingRules = @($taskExistingAcl.GetAccessRules($true, $false, [System.Security.Principal.SecurityIdentifier]))
    if ($taskExistingAcl.AreAccessRulesProtected -and $taskExistingRules.Count -eq 3 -and @($taskExistingRules | Where-Object { $_.IdentityReference.Value -notin $taskAllowedSids -or $_.AccessControlType -ne 'Allow' -or $_.FileSystemRights -ne 'FullControl' }).Count -eq 0) { return }
    $taskFileAcl = [System.Security.AccessControl.FileSecurity]::new()
    $taskFileAcl.SetAccessRuleProtection($true, $false)
    foreach ($taskFileSid in @([System.Security.Principal.WindowsIdentity]::GetCurrent().User, [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [System.Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $taskFileAcl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($taskFileSid, 'FullControl', 'Allow'))
    }
    [System.IO.FileSystemAclExtensions]::SetAccessControl([System.IO.FileInfo]::new($Path), $taskFileAcl)
}

function Initialize-NexusLocalSettings {
    $taskPaths = Get-NexusLocalPaths
    foreach ($taskDirectory in @((Split-Path $taskPaths.Settings), (Split-Path $taskPaths.Secrets))) { New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null }
    $taskLegacy = Join-Path $taskPaths.Root 'backend/src/AiNexus.Host/appsettings.Local.json'
    if (Test-Path -LiteralPath $taskLegacy) {
        $taskValues = [System.IO.File]::ReadAllText($taskLegacy) | ConvertFrom-Json -AsHashtable
        $taskPrivate = @{ Database = @{ User = $taskValues.Database.User; Password = $taskValues.Database.Password }; AdAuthentication = @{ DnPass = $taskValues.AdAuthentication.DnPass }; Inference = @{ GoogleApiKey = $taskValues.Inference.GoogleApiKey } }
        if ($taskValues.ConnectionStrings) { $taskPrivate.ConnectionStrings = $taskValues.ConnectionStrings }
        [void]$taskValues.Database.Remove('User'); [void]$taskValues.Database.Remove('Password')
        [void]$taskValues.AdAuthentication.Remove('DnPass'); [void]$taskValues.Inference.Remove('GoogleApiKey'); [void]$taskValues.Remove('ConnectionStrings')
        if (!(Test-Path -LiteralPath $taskPaths.Settings)) { Save-NexusJson $taskPaths.Settings $taskValues }
        $taskMerged = if (Test-Path -LiteralPath $taskPaths.Secrets) { [System.IO.File]::ReadAllText($taskPaths.Secrets) | ConvertFrom-Json -AsHashtable } else { @{} }
        foreach ($taskSection in $taskPrivate.Keys) {
            if (!$taskMerged[$taskSection]) { $taskMerged[$taskSection] = @{} }
            foreach ($taskField in $taskPrivate[$taskSection].Keys) {
                if ([string]::IsNullOrEmpty([string]$taskMerged[$taskSection][$taskField])) { $taskMerged[$taskSection][$taskField] = $taskPrivate[$taskSection][$taskField] }
            }
        }
        Save-NexusJson $taskPaths.Secrets $taskMerged
        Protect-NexusSecrets $taskPaths.Secrets
        $taskBackup = Join-Path (Split-Path $taskPaths.Secrets) ('legacy-settings-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '.json')
        Copy-Item -LiteralPath $taskLegacy -Destination $taskBackup
        Protect-NexusSecrets $taskBackup
        if ((Test-Path -LiteralPath $taskPaths.Settings) -and (Test-Path -LiteralPath $taskPaths.Secrets)) { Remove-Item -LiteralPath $taskLegacy }
        Write-Output '舊本機設定已拆分到 .local/config 與 .local/secrets；秘密值未輸出。'
    }
    foreach ($taskTemplate in @(@{ Path = $taskPaths.Settings; Example = 'appsettings.Local.example.json' }, @{ Path = $taskPaths.Secrets; Example = 'appsettings.Secrets.example.json' })) {
        if (!(Test-Path -LiteralPath $taskTemplate.Path)) { Copy-Item -LiteralPath (Join-Path $taskPaths.Root ('backend/src/AiNexus.Host/' + $taskTemplate.Example)) -Destination $taskTemplate.Path }
    }
    Protect-NexusSecrets $taskPaths.Secrets
    & (Join-Path $PSScriptRoot 'Migrate-Settings.ps1') -SettingsPath $taskPaths.Settings -SecretsPath $taskPaths.Secrets
}
