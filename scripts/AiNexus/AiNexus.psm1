# Shared functions of the entry scripts in scripts/. Never writes setting or secret values to the output.
Set-StrictMode -Version Latest

# dotnet、npm 等子程序輸出 UTF-8；主控台預設用 OEM 字碼頁（如 950）解碼會讓中文變亂碼。
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Get-RepoRoot {
    <# .SYNOPSIS Repository root directory. #>
    $root
}

function Get-RepoLocalPaths {
    <# .SYNOPSIS Machine settings and secrets files under .local/. #>
    @{
        Root = $root
        Settings = Join-Path $root '.local/config/appsettings.Local.json'
        Secrets = Join-Path $root '.local/secrets/appsettings.Secrets.json'
    }
}

function Resolve-RepoArtifactPath {
    <# .SYNOPSIS Absolute path of an output directory; throws unless it is inside the repository artifacts/. #>
    param([Parameter(Mandatory)][string]$Path)
    $full = [IO.Path]::GetFullPath($Path, $root)
    $artifacts = (Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase)) { throw "輸出目錄必須在 artifacts/ 內：$Path" }
    $full
}

function Get-RepoSetting {
    <# .SYNOPSIS Value at a dotted path (Section.Key) of a settings hashtable, or $null. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Settings, [Parameter(Mandatory)][string]$Path)
    $node = $Settings
    foreach ($key in $Path.Split('.')) {
        if ($node -isnot [Collections.IDictionary] -or !$node.Contains($key)) { return $null }
        $node = $node[$key]
    }
    $node
}

function Set-RepoSetting {
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

function Read-RepoJson {
    <# .SYNOPSIS Reads a JSON settings file as a hashtable. #>
    param([Parameter(Mandatory)][string]$Path)
    [IO.File]::ReadAllText($Path) | ConvertFrom-Json -AsHashtable
}

function Save-RepoJson {
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

function Get-RepoSecretValues {
    <# .SYNOPSIS Non-empty credential values: passwords and keys; with -IncludeUser also SQL logins. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Settings, [switch]$IncludeUser, [string]$Path = '')
    foreach ($key in $Settings.Keys) {
        $value = $Settings[$key]
        $next = if ($Path) { "$Path.$key" } else { $key }
        if ($value -is [Collections.IDictionary]) { Get-RepoSecretValues $value -IncludeUser:$IncludeUser -Path $next }
        elseif ($value -is [string] -and $value.Length -gt 0 -and ($key -match '^(Password|BindPassword|ApiKey)$' -or ($IncludeUser -and $key -eq 'User'))) { $value }
    }
}

function Protect-RepoSecrets {
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

function Initialize-RepoLocalSettings {
    <# .SYNOPSIS Copies the settings templates into .local/ on first use; existing machine files are never rewritten. #>
    $paths = Get-RepoLocalPaths
    foreach ($file in @(
        @{ Path = $paths.Settings; Template = 'appsettings.Local.example.json' },
        @{ Path = $paths.Secrets; Template = 'appsettings.Secrets.example.json' }
    )) {
        New-Item -ItemType Directory -Path (Split-Path $file.Path) -Force | Out-Null
        if (!(Test-Path -LiteralPath $file.Path)) { Copy-Item -LiteralPath (Join-Path $root "backend/src/AiNexus.Host/$($file.Template)") -Destination $file.Path }
    }
    Protect-RepoSecrets $paths.Secrets
    $paths
}

function Assert-RepoHttpsCertificate {
    <# .SYNOPSIS Throws unless the .NET SDK development HTTPS certificate exists. #>
    dotnet dev-certs https --check --quiet
    if ($LASTEXITCODE -ne 0) { throw '找不到本機 HTTPS 憑證。請先執行 dotnet dev-certs https --trust，再重新啟動。純 HTTP 測試可明確使用 -Http（僅限 localhost）。' }
}

function Invoke-RepoHostCommand {
    <#
    .SYNOPSIS Runs a host command (verify …) in Development with the machine settings under .local/.
    .OUTPUTS The host's exit code.
    #>
    param([Parameter(Mandatory)][string[]]$Command)
    $paths = Initialize-RepoLocalSettings
    $previous = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        dotnet run --project (Join-Path $root 'backend/src/AiNexus.Host/AiNexus.Host.csproj') --no-launch-profile -- @Command --LocalConfigPath $paths.Settings --SecretsConfigPath $paths.Secrets | Out-Host
        $LASTEXITCODE
    } finally { $env:ASPNETCORE_ENVIRONMENT = $previous }
}

# Pester runs the tests of these scripts (scripts/tests); Windows ships Pester 3, which is too old.
$pesterVersion = [version]'5.7.1'

function Install-RepoPester {
    <# .SYNOPSIS Installs the pinned Pester for the current user when no compatible version is present. #>
    if (Get-Module -ListAvailable Pester | Where-Object Version -GE $pesterVersion) { return }
    # The built-in Pester 3 is signed by another publisher, so the upgrade has to skip the publisher check.
    Install-Module Pester -RequiredVersion $pesterVersion -Scope CurrentUser -Force -SkipPublisherCheck
}

function Import-RepoPester {
    <# .SYNOPSIS Imports a compatible Pester or explains how to install it. #>
    if (!(Get-Module -ListAvailable Pester | Where-Object Version -GE $pesterVersion)) { throw "需要 Pester $pesterVersion 以上；請執行 ./scripts/Restore.ps1。" }
    Import-Module Pester -MinimumVersion $pesterVersion
}

function Test-RepoForbiddenPath {
    <# .SYNOPSIS True for a repository-relative path that must never be committed (machine settings, secrets, generated output). #>
    param([Parameter(Mandatory)][string]$Path)
    # artifacts/ is the root output folder; feature source folders may also be named Artifacts.
    $Path -match '^artifacts/|(^|/)(\.local|node_modules|bin|obj)/|(^|/)appsettings\.(.*\.)?(Local|Secrets|Production|Staging)\.json$|(^|/)\.env($|\.(?!example$))'
}

# 以下是 Export-Database.ps1 的後處理：只處理檔案、不連資料庫，所以測試可以離線拿匯出樣本驗證。

# 匯出腳本產生、每次整批替換的全文資料夾；FullText/ 底下其他檔案是手寫的。
$fullTextFolders = @{ Catalogs = 'FullText/Catalogs'; Indexes = 'FullText/Indexes' }
# 資料庫專案內手寫或建置產出的頂層資料夾，匯出時不可刪除。schema 不能取這些名稱。
$keptProjectFolders = @('FullText', 'Seed', 'Data', 'bin', 'obj')

function Split-RepoSqlBatch {
    <# .SYNOPSIS Batches of a SqlPackage-generated script, split on lines that contain only GO. #>
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text)
    , [string[]]@($Text -split '(?im)^[ \t]*GO[ \t]*\r?$' | ForEach-Object Trim | Where-Object { $_ })
}

function Join-RepoSqlBatch {
    <# .SYNOPSIS Script text in the layout SqlPackage writes (LF line endings): each batch followed by a GO line. #>
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Batch)
    -join @($Batch | ForEach-Object { $_.Replace("`r`n", "`n") + "`n`n`nGO`n`n" })
}

function Get-BatchSortKey([string]$Batch) {
    if ($Batch -notmatch '^EXEC(UTE)?\s+(sys\.)?sp_addextendedproperty\b') { return "0`u{1}$Batch" }
    $values = @{}
    foreach ($match in [regex]::Matches($Batch, "@(name|level[0-2]type|level[0-2]name)\s*=\s*N'((?:[^']|'')*)'")) { $values[$match.Groups[1].Value] = $match.Groups[2].Value }
    # 說明語句排在最後；物件本身（沒有 level2）在欄位前，最後以整段文字比較，鍵相同時順序仍固定。
    (@('1') + @('level0type', 'level0name', 'level1type', 'level1name', 'level2type', 'level2name', 'name' | ForEach-Object { [string]$values[$_] }) + $Batch) -join "`u{1}"
}

function Format-RepoSqlBatchOrder {
    <#
    .SYNOPSIS Batches of one object file in a stable order: the object itself first, then indexes and other batches, then descriptions.
    .DESCRIPTION
    SqlPackage 依資料庫內部順序輸出索引與欄位說明：不同資料庫順序不同，同一庫改了別的東西（例如相容層級）也可能重排，
    排序後才不會出現只有順序的 diff。檔案內的順序不影響 dacpac（模型自己解析相依），所以可以放心排序。
    #>
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Batch)
    if ($Batch.Count -le 2) { return , $Batch }
    # 要宣告成 string[]：object[] 傳給 Array.Sort 時 PowerShell 會轉成副本，排序結果就丟了。
    [string[]]$rest = $Batch[1..($Batch.Count - 1)]
    $keys = [string[]]@($rest | ForEach-Object { Get-BatchSortKey $_ })
    [Array]::Sort($keys, $rest, [StringComparer]::Ordinal)
    , (@($Batch[0]) + $rest)
}

function Test-RepoFullTextBatch {
    <# .SYNOPSIS True for a batch that needs the full-text component: catalogs, stoplists, search property lists and full-text indexes. #>
    param([Parameter(Mandatory)][string]$Batch)
    $Batch -match '^(CREATE|ALTER)\s+(FULLTEXT\s+(CATALOG|STOPLIST|INDEX)|SEARCH\s+PROPERTY\s+LIST)\b'
}

function Invoke-RepoExtractPostProcess {
    <#
    .SYNOPSIS Turns a SqlPackage SchemaObjectType extract folder into the committed layout; returns the removed files.
    .DESCRIPTION
    就地修改 Path：
    - 移除符合 ExcludeTable（萬用字元，比對 schema.name）的資料表，以及使用者、角色、角色成員（Extract 沒有對應的排除參數）。
    - 全文物件移到 FullText/Catalogs（目錄、停用字表）與 FullText/Indexes/<schema>.<物件>.sql：
      ExcludeObjectTypes 排除不了全文索引，留在模型裡會讓沒有全文元件的環境（LocalDB）無法部署。
    - 主物件之後的索引與欄位說明語句排序；檔案一律 UTF-8 無 BOM、LF（與 .gitattributes 一致，避免只有行尾的 diff）。
    回傳被移除的檔案（相對路徑），由呼叫端印出，避免悄悄漏掉物件。
    #>
    param([Parameter(Mandatory)][string]$Path, [string[]]$ExcludeTable = @())
    $removed = [Collections.Generic.List[string]]::new()
    $encoding = [Text.UTF8Encoding]::new($false)
    foreach ($pattern in $ExcludeTable) { if ($pattern -notmatch '^[^.]+\.[^.]+$') { throw "ExcludeTable 必須是 schema.name（可用萬用字元）：$pattern" } }
    foreach ($folder in $keptProjectFolders) {
        if (Test-Path -LiteralPath (Join-Path $Path $folder)) { throw "匯出結果有名為 $folder 的 schema，會和資料庫專案的保留資料夾衝突；請改 schema 名稱。" }
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $Path -Filter '*.sql' -Recurse)) {
        $relative = [IO.Path]::GetRelativePath($Path, $file.FullName).Replace('\', '/')
        $parts = $relative.Split('/')
        $excluded = $parts.Length -eq 3 -and $parts[1] -eq 'Tables' -and @($ExcludeTable | Where-Object { "$($parts[0]).$($file.BaseName)" -like $_ }).Count
        # schema 也放在 Security/，只能依內容判斷，不能整個資料夾刪掉。
        $principal = $parts[0] -eq 'Security' -and [IO.File]::ReadAllText($file.FullName) -match '(?im)^\s*CREATE\s+(USER|ROLE|APPLICATION\s+ROLE)\b|^\s*ALTER\s+ROLE\b|sp_addrolemember'
        if ($excluded -or $principal) {
            Remove-Item -LiteralPath $file.FullName
            $removed.Add($relative)
            continue
        }

        $batches = Split-RepoSqlBatch ([IO.File]::ReadAllText($file.FullName))
        $fullText = @($batches | Where-Object { Test-RepoFullTextBatch $_ })
        $rest = Format-RepoSqlBatchOrder @($batches | Where-Object { !(Test-RepoFullTextBatch $_) })
        foreach ($group in @($fullText | Group-Object { if ($_ -match '^\S+\s+FULLTEXT\s+INDEX\b') { 'Indexes' } else { 'Catalogs' } })) {
            $name = if ($group.Name -eq 'Indexes') { "$($parts[0]).$($file.BaseName).sql" } else { $file.Name }
            $target = Join-Path $Path "$($fullTextFolders[$group.Name])/$name"
            if (Test-Path -LiteralPath $target) { throw "全文物件的目標檔已存在：$target" }
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            [IO.File]::WriteAllText($target, (Join-RepoSqlBatch $group.Group), $encoding)
        }
        if (!$rest.Count) { Remove-Item -LiteralPath $file.FullName; continue }
        $text = if ($fullText.Count -or ($rest -join "`0") -ne ($batches -join "`0")) { Join-RepoSqlBatch $rest } else { [IO.File]::ReadAllText($file.FullName).Replace("`r`n", "`n") }
        [IO.File]::WriteAllText($file.FullName, $text, $encoding)
    }

    Remove-RepoEmptyFolder $Path
    , $removed.ToArray()
}

function Read-RepoExclusionList {
    <#
    .SYNOPSIS Table patterns (schema.name, wildcards allowed) listed in an export exclusion file; empty when the file is missing.
    .DESCRIPTION 一行一個，# 之後是註解，空行略過。沒有清單就什麼都不排除：不依名稱猜測暫存表或備份表。
    #>
    param([Parameter(Mandatory)][string]$Path)
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    [IO.File]::ReadAllLines($Path) | ForEach-Object { ($_ -replace '#.*$', '').Trim() } | Where-Object { $_ }
}

function Get-RepoUnusedExclusion {
    <#
    .SYNOPSIS Exclusion patterns that matched none of the removed tables (relative paths as Invoke-RepoExtractPostProcess returns them).
    .DESCRIPTION 表刪掉後留在清單裡的項目，之後可能意外排除同名的新表，所以由呼叫端警告。
    #>
    param([AllowEmptyCollection()][string[]]$Pattern = @(), [AllowEmptyCollection()][string[]]$Removed = @())
    $tables = @($Removed | Where-Object { $_ -match '^[^/]+/Tables/[^/]+\.sql$' } | ForEach-Object {
        $parts = $_.Split('/'); "$($parts[0]).$([IO.Path]::GetFileNameWithoutExtension($parts[2]))" })
    $Pattern | Where-Object { $item = $_; !@($tables | Where-Object { $_ -like $item }).Count }
}

function Remove-RepoEmptyFolder {
    <# .SYNOPSIS Removes the empty folders under Path, deepest first; Path itself stays. #>
    param([Parameter(Mandatory)][string]$Path, [string[]]$Keep = @())
    Get-ChildItem -LiteralPath $Path -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { [IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/').Split('/')[0] -notin $Keep } |
        Where-Object { !(Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item
}

function Update-RepoDatabaseProject {
    <#
    .SYNOPSIS Replaces the exported *.sql of a database project folder with a processed extract.
    .DESCRIPTION 物件被刪除時檔案也要跟著消失，所以先刪掉上一次匯出的檔案再複製；手寫資料夾、bin／obj 與 *.sql 以外的檔案（sqlproj）保留。
    #>
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($file in @(Get-ChildItem -LiteralPath $Destination -Filter '*.sql' -Recurse)) {
        $relative = [IO.Path]::GetRelativePath($Destination, $file.FullName).Replace('\', '/')
        $generated = $relative.Split('/')[0] -notin $keptProjectFolders -or @($fullTextFolders.Values | Where-Object { $relative.StartsWith("$_/") }).Count
        if ($generated) { Remove-Item -LiteralPath $file.FullName }
    }
    Remove-RepoEmptyFolder $Destination -Keep $keptProjectFolders
    foreach ($file in @(Get-ChildItem -LiteralPath $Source -Filter '*.sql' -Recurse)) {
        $target = Join-Path $Destination ([IO.Path]::GetRelativePath($Source, $file.FullName))
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}

# 資料庫層級選項與 SQL 專案屬性的對應；每物件一檔的匯出不含這些選項，只能寫在 sqlproj。
$databaseSettings = [ordered]@{ Collation = 'DefaultCollation'; CompatibilityLevel = 'CompatibilityMode'; ReadCommittedSnapshot = 'ReadCommittedSnapshot' }

function Get-RepoSqlProjectSetting {
    <# .SYNOPSIS Collation, compatibility level and RCSI an SDK-style SQL project writes into its dacpac ($null when not set). #>
    param([Parameter(Mandatory)][string]$Path)
    $xml = [xml][IO.File]::ReadAllText($Path)
    $settings = [ordered]@{}
    foreach ($key in $databaseSettings.Keys) {
        $node = $xml.SelectSingleNode("/Project/PropertyGroup/$($databaseSettings[$key])")
        $settings[$key] = if ($node) { $node.InnerText.Trim() } else { $null }
    }
    $settings
}

function Compare-RepoDatabaseSetting {
    <# .SYNOPSIS Messages for each database option that differs from the SQL project; nothing when they agree. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Database, [Parameter(Mandatory)][Collections.IDictionary]$Project)
    foreach ($key in $databaseSettings.Keys) {
        $property = $databaseSettings[$key]
        if ([string]::IsNullOrEmpty($Project[$key])) { "sqlproj 沒有設定 $property（資料庫為 $($Database[$key])）。" }
        elseif ("$($Database[$key])" -ne "$($Project[$key])") { "$property 不一致：資料庫為 $($Database[$key])，sqlproj 為 $($Project[$key])。" }
    }
}

function Test-RepoDatabaseExists {
    <#
    .SYNOPSIS True when the server already has the database (queried through master).
    .DESCRIPTION PowerShell 內附的 System.Data.SqlClient 不支援 LocalDB（shared memory）的加密，所以只有 LocalDB 不要求加密。
    #>
    param([Parameter(Mandatory)][string]$Server, [Parameter(Mandatory)][string]$Database, [pscredential]$Credential, [switch]$TrustServerCertificate)
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Server
    $builder['Initial Catalog'] = 'master'
    $builder['Encrypt'] = $Server -notlike '(localdb)*'
    $builder['TrustServerCertificate'] = [bool]$TrustServerCertificate
    $credential = $null
    if ($Credential) {
        $password = $Credential.Password.Copy()
        $password.MakeReadOnly()
        $credential = [Data.SqlClient.SqlCredential]::new($Credential.UserName, $password)
    } else { $builder['Integrated Security'] = $true }
    $connection = [Data.SqlClient.SqlConnection]::new($builder.ConnectionString, $credential)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT DB_ID(@Name)'
        [void]$command.Parameters.AddWithValue('@Name', $Database)
        $command.ExecuteScalar() -isnot [DBNull]
    } finally { $connection.Dispose() }
}

Export-ModuleMember -Function *-Repo*
