# Shared by setup, publishing and repository checks; never writes setting values to stdout.
function Get-NexusLocalPaths {
    $taskWorkspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    return @{ Root = $taskWorkspace; Settings = Join-Path $taskWorkspace '.local/config/appsettings.Local.json'; Secrets = Join-Path $taskWorkspace '.local/secrets/appsettings.Secrets.json' }
}

function Set-NexusSetting([System.Collections.IDictionary]$Value, [string]$Path, $Setting) {
    $taskKeys = $Path.Split('.')
    $taskNode = $Value
    for ($taskIndex = 0; $taskIndex -lt $taskKeys.Length - 1; $taskIndex++) {
        $taskKey = $taskKeys[$taskIndex]
        if ($taskNode[$taskKey] -isnot [System.Collections.IDictionary]) { $taskNode[$taskKey] = [ordered]@{} }
        $taskNode = $taskNode[$taskKey]
    }
    $taskNode[$taskKeys[-1]] = $Setting
}

# Writes through a temporary file that inherits the target's ACL, so a secrets file is never briefly readable by others.
function Save-NexusJson([string]$Path, [System.Collections.IDictionary]$Value) {
    $taskText = ($Value | ConvertTo-Json -Depth 40) + "`n"
    $taskAbsolute = [IO.Path]::GetFullPath($Path)
    $taskTemporary = $taskAbsolute + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($taskTemporary, '')
        if (Test-Path -LiteralPath $taskAbsolute) { Set-Acl -LiteralPath $taskTemporary -AclObject (Get-Acl -LiteralPath $taskAbsolute) }
        [IO.File]::WriteAllText($taskTemporary, $taskText, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $taskAbsolute) { [IO.File]::Replace($taskTemporary, $taskAbsolute, [NullString]::Value) }
        else { [IO.File]::Move($taskTemporary, $taskAbsolute) }
    } finally {
        if (Test-Path -LiteralPath $taskTemporary) { Remove-Item -LiteralPath $taskTemporary }
        $taskText = $null
    }
}

# Non-empty credential values (passwords, keys, connection strings; with -IncludeUser also SQL logins).
function Get-NexusSecretValues([System.Collections.IDictionary]$Value, [switch]$IncludeUser, [string]$Path = '') {
    foreach ($taskKey in $Value.Keys) {
        $taskNext = if ($Path) { "$Path.$taskKey" } else { $taskKey }
        if ($Value[$taskKey] -is [System.Collections.IDictionary]) { Get-NexusSecretValues $Value[$taskKey] -IncludeUser:$IncludeUser -Path $taskNext }
        elseif ($Value[$taskKey] -is [string] -and $Value[$taskKey].Length -gt 0 -and ($taskKey -match '^(Password|DnPass|ApiKey)$' -or $Path -eq 'ConnectionStrings' -or ($IncludeUser -and $taskKey -eq 'User'))) { $Value[$taskKey] }
    }
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

# Copies the templates on first use; existing machine files are never rewritten here.
function Initialize-NexusLocalSettings {
    $taskPaths = Get-NexusLocalPaths
    foreach ($taskDirectory in @((Split-Path $taskPaths.Settings), (Split-Path $taskPaths.Secrets))) { New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null }
    foreach ($taskTemplate in @(@{ Path = $taskPaths.Settings; Example = 'appsettings.Local.example.json' }, @{ Path = $taskPaths.Secrets; Example = 'appsettings.Secrets.example.json' })) {
        if (!(Test-Path -LiteralPath $taskTemplate.Path)) { Copy-Item -LiteralPath (Join-Path $taskPaths.Root ('backend/src/AiNexus.Host/' + $taskTemplate.Example)) -Destination $taskTemplate.Path }
    }
    Protect-NexusSecrets $taskPaths.Secrets
}
