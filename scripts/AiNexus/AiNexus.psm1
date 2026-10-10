# Shared functions of the entry scripts in scripts/. Never writes setting or secret values to the output.
Set-StrictMode -Version Latest

# dotnet、npm 等子程序輸出 UTF-8；主控台預設用 OEM 字碼頁（如 950）解碼會讓中文變亂碼。
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Get-NexusRoot {
    <# .SYNOPSIS Repository root directory. #>
    $root
}

function Get-NexusLocalPaths {
    <# .SYNOPSIS Machine settings and secrets files under .local/. #>
    @{
        Root = $root
        Settings = Join-Path $root '.local/config/appsettings.Local.json'
        Secrets = Join-Path $root '.local/secrets/appsettings.Secrets.json'
    }
}

function Resolve-NexusArtifactPath {
    <# .SYNOPSIS Absolute path of an output directory; throws unless it is inside the repository artifacts/. #>
    param([Parameter(Mandatory)][string]$Path)
    $full = [IO.Path]::GetFullPath($Path, $root)
    $artifacts = (Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase)) { throw "輸出目錄必須在 artifacts/ 內：$Path" }
    $full
}

function Get-NexusSetting {
    <# .SYNOPSIS Value at a dotted path (Section.Key) of a settings hashtable, or $null. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Settings, [Parameter(Mandatory)][string]$Path)
    $node = $Settings
    foreach ($key in $Path.Split('.')) {
        if ($node -isnot [Collections.IDictionary] -or !$node.Contains($key)) { return $null }
        $node = $node[$key]
    }
    $node
}

function Set-NexusSetting {
    <# .SYNOPSIS Sets a dotted path (Section.Key) of a settings hashtable, creating missing sections. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Settings, [Parameter(Mandatory)][string]$Path, [AllowNull()]$Value)
    $keys = $Path.Split('.')
    $node = $Settings
    for ($i = 0; $i -lt $keys.Length - 1; $i++) {
        $key = $keys[$i]
        if (!$node.Contains($key) -or $node[$key] -isnot [Collections.IDictionary]) { $node[$key] = [ordered]@{} }
        $node = $node[$key]
    }
    $node[$keys[-1]] = $Value
}

function Read-NexusJson {
    <# .SYNOPSIS Reads a JSON settings file as a hashtable. #>
    param([Parameter(Mandatory)][string]$Path)
    [IO.File]::ReadAllText($Path) | ConvertFrom-Json -AsHashtable
}

function Save-NexusJson {
    <#
    .SYNOPSIS Writes UTF-8 JSON through a temporary file.
    .DESCRIPTION The temporary file gets the target's permissions first, so a secrets file is never briefly readable by others.
    #>
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][Collections.IDictionary]$Value)
    $text = ($Value | ConvertTo-Json -Depth 40) + "`n"
    $target = [IO.Path]::GetFullPath($Path)
    $temporary = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporary, '')
        if (Test-Path -LiteralPath $target) {
            if ($IsWindows) { Set-Acl -LiteralPath $temporary -AclObject (Get-Acl -LiteralPath $target) }
            else { [IO.File]::SetUnixFileMode($temporary, [IO.File]::GetUnixFileMode($target)) }
        }
        [IO.File]::WriteAllText($temporary, $text, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temporary, $target, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $target) }
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

function Get-NexusSecretValues {
    <# .SYNOPSIS Non-empty credential values: passwords, keys and connection strings; with -IncludeUser also SQL logins. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Settings, [switch]$IncludeUser, [string]$Path = '')
    foreach ($key in $Settings.Keys) {
        $value = $Settings[$key]
        $next = if ($Path) { "$Path.$key" } else { $key }
        if ($value -is [Collections.IDictionary]) { Get-NexusSecretValues $value -IncludeUser:$IncludeUser -Path $next }
        elseif ($value -is [string] -and $value.Length -gt 0 -and ($key -match '^(Password|BindPassword|ApiKey)$' -or $Path -eq 'ConnectionStrings' -or ($IncludeUser -and $key -eq 'User'))) { $value }
    }
}

function Protect-NexusSecrets {
    <# .SYNOPSIS Restricts a secrets file to the current user (Windows: also SYSTEM and Administrators). #>
    param([Parameter(Mandatory)][string]$Path)
    if (!$IsWindows) {
        [IO.File]::SetUnixFileMode($Path, [IO.UnixFileMode]'UserRead, UserWrite')
        return
    }
    $current = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $allowed = @($current, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    $existing = Get-Acl -LiteralPath $Path
    $rules = @($existing.GetAccessRules($true, $false, [Security.Principal.SecurityIdentifier]))
    $unexpected = @($rules | Where-Object { $_.IdentityReference.Value -notin $allowed.Value -or $_.AccessControlType -ne 'Allow' -or $_.FileSystemRights -ne 'FullControl' })
    if ($existing.AreAccessRulesProtected -and $rules.Count -eq 3 -and $unexpected.Count -eq 0) { return }
    $acl = [Security.AccessControl.FileSecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in $allowed) { $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'Allow')) }
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($Path), $acl)
}

function Initialize-NexusLocalSettings {
    <# .SYNOPSIS Copies the settings templates into .local/ on first use; existing machine files are never rewritten. #>
    $paths = Get-NexusLocalPaths
    foreach ($file in @(
        @{ Path = $paths.Settings; Template = 'appsettings.Local.example.json' },
        @{ Path = $paths.Secrets; Template = 'appsettings.Secrets.example.json' }
    )) {
        New-Item -ItemType Directory -Path (Split-Path $file.Path) -Force | Out-Null
        if (!(Test-Path -LiteralPath $file.Path)) { Copy-Item -LiteralPath (Join-Path $root "backend/src/AiNexus.Host/$($file.Template)") -Destination $file.Path }
    }
    Protect-NexusSecrets $paths.Secrets
    $paths
}

function Assert-NexusHttpsCertificate {
    <# .SYNOPSIS Throws unless the .NET SDK development HTTPS certificate exists. #>
    dotnet dev-certs https --check --quiet
    if ($LASTEXITCODE -ne 0) { throw '找不到本機 HTTPS 憑證。請先執行 dotnet dev-certs https --trust，再重新啟動。純 HTTP 測試可明確使用 -Http（僅限 localhost）。' }
}

function Invoke-NexusHostCommand {
    <#
    .SYNOPSIS Runs a host command (db init, verify …) in Development with the machine settings under .local/.
    .OUTPUTS The host's exit code.
    #>
    param([Parameter(Mandatory)][string[]]$Command)
    $paths = Initialize-NexusLocalSettings
    $previous = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        dotnet run --project (Join-Path $root 'backend/src/AiNexus.Host/AiNexus.Host.csproj') --no-launch-profile -- @Command --LocalConfigPath $paths.Settings --SecretsConfigPath $paths.Secrets | Out-Host
        $LASTEXITCODE
    } finally { $env:ASPNETCORE_ENVIRONMENT = $previous }
}

# Pester runs the tests of these scripts (scripts/tests); Windows ships Pester 3, which is too old.
$pesterVersion = [version]'5.7.1'

function Install-NexusPester {
    <# .SYNOPSIS Installs the pinned Pester for the current user when no compatible version is present. #>
    if (Get-Module -ListAvailable Pester | Where-Object Version -GE $pesterVersion) { return }
    # The built-in Pester 3 is signed by another publisher, so the upgrade has to skip the publisher check.
    Install-Module Pester -RequiredVersion $pesterVersion -Scope CurrentUser -Force -SkipPublisherCheck
}

function Import-NexusPester {
    <# .SYNOPSIS Imports a compatible Pester or explains how to install it. #>
    if (!(Get-Module -ListAvailable Pester | Where-Object Version -GE $pesterVersion)) { throw "需要 Pester $pesterVersion 以上；請執行 ./scripts/Restore.ps1。" }
    Import-Module Pester -MinimumVersion $pesterVersion
}

function Test-NexusForbiddenPath {
    <# .SYNOPSIS True for a repository-relative path that must never be committed (machine settings, secrets, generated output). #>
    param([Parameter(Mandatory)][string]$Path)
    # artifacts/ is the root output folder; feature source folders may also be named Artifacts.
    $Path -match '^artifacts/|(^|/)(\.local|node_modules|bin|obj)/|(^|/)appsettings\.(.*\.)?(Local|Secrets|Production|Staging)\.json$|(^|/)\.env($|\.(?!example$))'
}

Export-ModuleMember -Function *-Nexus*
