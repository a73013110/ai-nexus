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
    <# .SYNOPSIS .local/ 下的本機設定檔與秘密檔路徑。 #>
    @{
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

function Disable-RepoTelemetry {
    <#
    .SYNOPSIS 關閉 Angular CLI 與 .NET CLI 的使用資料回傳。
    .DESCRIPTION 沒有設定時 Angular CLI 第一次執行會停下來詢問是否分享使用資料。設在目前工作階段的環境變數，之後啟動的 npm、dotnet 都沿用。
    #>
    $env:NG_CLI_ANALYTICS = 'false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
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
$keptProjectFolders = @('FullText', 'Seed', 'Scripts', 'bin', 'obj')

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

function ConvertTo-SqlLiteral([string]$Value) { "N'" + $Value.Replace("'", "''") + "'" }

# 匯出的全文語句是 CREATE、沒有存在檢查；依第一個 CREATE 判斷是哪個物件（同檔的 ALTER 也是同一個物件）。
function Get-FullTextObject([string]$File, [string[]]$Batch) {
    $name = '(?:\[(?:[^\]]|\]\])+\]|[\w@#$]+)'
    $create = $Batch | Where-Object { $_ -match '^CREATE\b' } | Select-Object -First 1
    if (!$create) { throw "$File 沒有 CREATE 語句，無法判斷全文物件是否已存在。" }
    if ($create -match "^CREATE\s+FULLTEXT\s+INDEX\s+ON\s+($name\.$name)") {
        return @{ Name = $Matches[1]; Exists = "EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID($(ConvertTo-SqlLiteral $Matches[1])))" }
    }
    $views = @{ 'FULLTEXT CATALOG' = 'sys.fulltext_catalogs'; 'FULLTEXT STOPLIST' = 'sys.fulltext_stoplists'; 'SEARCH PROPERTY LIST' = 'sys.registered_search_property_lists' }
    if ($create -match "^CREATE\s+(FULLTEXT\s+CATALOG|FULLTEXT\s+STOPLIST|SEARCH\s+PROPERTY\s+LIST)\s+($name)") {
        $object = $Matches[2]
        $plain = if ($object.StartsWith('[')) { $object.Substring(1, $object.Length - 2).Replace(']]', ']') } else { $object }
        return @{ Name = $object; Exists = "EXISTS (SELECT 1 FROM $($views[($Matches[1] -replace '\s+', ' ').ToUpperInvariant()]) WHERE name = $(ConvertTo-SqlLiteral $plain))" }
    }
    throw "$File 不是可辨識的全文物件（目錄、停用字表、搜尋屬性清單或全文索引）。"
}

function Get-RepoReleaseScript {
    <#
    .SYNOPSIS The hand-written SQL that runs after the schema update, in run order: full-text objects, then seed data.
    .DESCRIPTION
    update.sql 與 Initialize-Database.ps1 在結構之後依序執行，所以每一份都要可以重跑：全文腳本包成「沒有全文元件或物件已存在就略過」
    （匯出的 CREATE 沒有存在檢查；已存在物件的定義變更不會套用，由 DBA 手動處理），種子資料本身只新增缺少的列。
    update.sql 以 SQLCMD 模式執行，所以手寫腳本不能有 SQLCMD 指令，也不能出現 $(（會被當成變數）。
    #>
    param([Parameter(Mandatory)][string]$Path)
    $files = {
        param([string]$Folder)
        if (Test-Path -LiteralPath $Folder -PathType Container) {
            Get-ChildItem -LiteralPath $Folder -Filter '*.sql' -File | Sort-Object { $_.Name } -Culture ([cultureinfo]::InvariantCulture)
        }
    }
    $steps = @(
        @{ Label = 'fulltext'; Title = '全文'; Files = @(@(& $files (Join-Path $Path 'FullText/Catalogs')) + @(& $files (Join-Path $Path 'FullText/Indexes'))) }
        @{ Label = 'seed'; Title = '種子資料'; Files = @(& $files (Join-Path $Path 'Seed')) }
    )
    foreach ($step in $steps) {
        foreach ($file in $step.Files | Where-Object { $_ }) {
            $source = [IO.Path]::GetRelativePath($Path, $file.FullName).Replace('\', '/')
            $text = [IO.File]::ReadAllText($file.FullName)
            if ($text -match '(?im)^\s*:(setvar|on|r|connect)\b') { throw "$source 含 SQLCMD 指令；手寫腳本只能用 T-SQL 與 GO。" }
            if ($text.Contains('$(')) { throw "$source 含 `$(；update.sql 以 SQLCMD 模式執行，會被當成變數。請改寫（例如拆成 N'`$' + N'('）。" }
            $header = "-- $($step.Title)：$source"
            if ($step.Label -eq 'fulltext') {
                $batches = Split-RepoSqlBatch $text
                $object = Get-FullTextObject $source $batches
                $text = @(
                    "$header。沒有全文元件或物件已存在時略過；已存在物件的定義變更由 DBA 手動處理。"
                    "IF CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')) <> 1"
                    "    PRINT $(ConvertTo-SqlLiteral "略過 $($object.Name)：沒有全文元件，混合檢索改用 vector 模式。");"
                    "ELSE IF $($object.Exists)"
                    "    PRINT $(ConvertTo-SqlLiteral "略過 $($object.Name)：已存在。");"
                    'ELSE'
                    'BEGIN'
                    @($batches | ForEach-Object { "    EXEC ($(ConvertTo-SqlLiteral $_.Replace("`r`n", "`n")));" })
                    'END'
                    'GO'
                    ''
                ) -join "`n"
            } else { $text = "$header`n$text" }
            [pscustomobject]@{ Name = "$($step.Label)-$($file.Name)"; Source = $source; Text = $text }
        }
    }
}

function Invoke-RepoSqlScript {
    <# .SYNOPSIS Runs a T-SQL script on an open connection, one batch per GO line, the way SSMS runs it without SQLCMD mode. #>
    param([Parameter(Mandatory)][Data.SqlClient.SqlConnection]$Connection, [Parameter(Mandatory)][string]$Text, [string]$Name = 'SQL')
    foreach ($batch in Split-RepoSqlBatch $Text) {
        $command = $Connection.CreateCommand()
        $command.CommandText = $batch
        $command.CommandTimeout = 0
        try { [void]$command.ExecuteNonQuery() }
        catch { throw "執行 $Name 失敗：$($_.Exception.InnerException?.Message ?? $_.Exception.Message)" }
    }
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

function New-RepoSqlConnection {
    <#
    .SYNOPSIS An unopened connection to a database (master by default), with the SQL login when given, else the current Windows identity.
    .DESCRIPTION
    PowerShell 內附的 System.Data.SqlClient 不支援 LocalDB（shared memory）的加密，所以只有 LocalDB 不要求加密。
    變數名稱不分大小寫：區域變數不能叫 $credential，否則會蓋掉 -Credential，靜默改用 Windows 驗證。
    #>
    param([Parameter(Mandatory)][string]$Server, [string]$Database = 'master', [pscredential]$Credential, [switch]$TrustServerCertificate)
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Server
    $builder['Initial Catalog'] = $Database
    $builder['Encrypt'] = $Server -notlike '(localdb)*'
    $builder['TrustServerCertificate'] = [bool]$TrustServerCertificate
    $sqlCredential = $null
    if ($Credential) {
        $password = $Credential.Password.Copy()
        $password.MakeReadOnly()
        $sqlCredential = [Data.SqlClient.SqlCredential]::new($Credential.UserName, $password)
    } else { $builder['Integrated Security'] = $true }
    [Data.SqlClient.SqlConnection]::new($builder.ConnectionString, $sqlCredential)
}

function Test-RepoDatabaseExists {
    <# .SYNOPSIS True when the server already has the database (queried through master). #>
    param([Parameter(Mandatory)][string]$Server, [Parameter(Mandatory)][string]$Database, [pscredential]$Credential, [switch]$TrustServerCertificate)
    $connection = New-RepoSqlConnection -Server $Server -Credential $Credential -TrustServerCertificate:$TrustServerCertificate
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT DB_ID(@Name)'
        [void]$command.Parameters.AddWithValue('@Name', $Database)
        $command.ExecuteScalar() -isnot [DBNull]
    } finally { $connection.Dispose() }
}

# 以下是發版（ADR 0027、0028）：版號、tag、元件判定、資料庫建置與更新包；Release.ps1、Build.ps1、Initialize-Database.ps1 共用。

# 元件依路徑判定；其他路徑（docs、scripts、tooling）不屬於任何元件，只改它們不需要發版。
$componentPaths = [ordered]@{ app = @('backend/', 'frontend/', 'contracts/'); database = @('database/') }
$releaseTagPattern = '^v(\d{4})\.(\d{2})\.(\d+)$'

function Invoke-RepoGit {
    <# .SYNOPSIS Runs git in the repository and returns its standard output lines; throws with git's message when it fails. #>
    param([Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$Arguments)
    $output = & git -C $root @Arguments 2>&1
    $failed = $LASTEXITCODE -ne 0
    $lines = @($output | Where-Object { $_ -isnot [Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    if ($failed) { throw "git $($Arguments -join ' ') 失敗：$(@($output | ForEach-Object { "$_" }) -join "`n")" }
    $lines
}

function Get-RepoPathComponent {
    <# .SYNOPSIS Components (app, database) that repository-relative paths belong to, in a fixed order. #>
    param([AllowEmptyCollection()][string[]]$Path = @())
    foreach ($component in $componentPaths.Keys) {
        if (@($Path | Where-Object { $file = $_.Replace('\', '/'); @($componentPaths[$component] | Where-Object { $file.StartsWith($_) }).Count }).Count) { $component }
    }
}

function Get-RepoReleaseTag {
    <#
    .SYNOPSIS Release tags (annotated vYYYY.MM.N), newest version first, with commit, date and the components from the Components trailer.
    .DESCRIPTION lightweight tag 與名稱不符的 tag 不算發版。-Merged 只留 HEAD 能到達的（hotfix 分支看不到 master 之後的發版）。
    #>
    param([switch]$Merged)
    $format = '%(refname:strip=2)%09%(objecttype)%09%(*objectname)%09%(creatordate:iso-strict)%09%(contents:trailers:key=Components,valueonly,separator=%x2C)'
    $arguments = @('for-each-ref', "--format=$format") + @(if ($Merged) { '--merged=HEAD' }) + 'refs/tags/v*'
    $tags = foreach ($line in Invoke-RepoGit @arguments) {
        $fields = $line.Split("`t")
        if ($fields.Count -lt 5 -or $fields[1] -ne 'tag' -or !($fields[0] -match $releaseTagPattern)) { continue }
        [pscustomobject]@{
            Tag = $fields[0]
            Version = $fields[0].Substring(1)
            Key = [int]$Matches[1] * 100000000L + [int]$Matches[2] * 1000000L + [int]$Matches[3]
            Commit = $fields[2]
            Date = [DateTimeOffset]::Parse($fields[3], [cultureinfo]::InvariantCulture)
            Components = @($fields[4].Split(',') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
        }
    }
    @($tags | Sort-Object Key -Descending)
}

function Get-RepoNextVersion {
    <# .SYNOPSIS The next version YYYY.MM.N for a date: N counts the releases of that month from 0. #>
    param([AllowEmptyCollection()][string[]]$Existing = @(), [datetime]$Date = [datetime]::Now)
    $prefix = $Date.ToString('yyyy.MM', [cultureinfo]::InvariantCulture)
    $used = @($Existing | Where-Object { $_ -match "^$([regex]::Escape($prefix))\.(\d+)$" } | ForEach-Object { [int]$Matches[1] })
    "$prefix.$(if ($used.Count) { ($used | Measure-Object -Maximum).Maximum + 1 } else { 0 })"
}

function Get-RepoVersion {
    <#
    .SYNOPSIS The build version of HEAD from the nearest release tag: 2026.11.4 on the tag, 2026.11.4-dev.3 three commits later, 0.0.0-dev without a tag.
    .DESCRIPTION git describe 預設只認 annotated tag；Revision 是 HEAD 的完整 SHA，建置時寫進 InformationalVersion（版本+commit）。
    #>
    $revision = @(Invoke-RepoGit rev-parse HEAD)[0]
    $described = & git -C $root describe --match 'v[0-9][0-9][0-9][0-9].[0-9][0-9].*' --long --abbrev=7 2>$null
    $version = if ($LASTEXITCODE -eq 0 -and "$described" -match '^v(\d{4}\.\d{2}\.\d+)-(\d+)-g[0-9a-f]+$') {
        if ($Matches[2] -eq '0') { $Matches[1] } else { "$($Matches[1])-dev.$($Matches[2])" }
    } else { '0.0.0-dev' }
    [pscustomobject]@{ Version = $version; Revision = $revision; Short = $revision.Substring(0, 7) }
}

function Get-RepoConventionalCommit {
    <# .SYNOPSIS feat and fix commits (type, scope, subject) between two refs that touched the given paths, oldest first. #>
    param([string]$From, [string]$To = 'HEAD', [string[]]$Path = @())
    $range = if ($From) { "$From..$To" } else { $To }
    $arguments = @('log', '--reverse', '--no-merges', '--format=%h%x09%s', $range) + @(if ($Path.Count) { @('--') + $Path })
    foreach ($line in Invoke-RepoGit @arguments) {
        $hash, $subject = $line.Split("`t", 2)
        if ($subject -match '^(feat|fix)(\(([^)]+)\))?!?:\s*(.+)$') {
            [pscustomobject]@{ Type = $Matches[1]; Scope = "$($Matches[3])"; Subject = $Matches[4]; Commit = $hash }
        }
    }
}

function Get-RepoDatabaseChange {
    <#
    .SYNOPSIS Database objects whose exported file changed between two refs (Added, Modified, Deleted), from the paths under database/App.
    .DESCRIPTION 只看檔案、不建置，所以狀態列表很快；實際的結構差異由更新包的演練確認。Scripts/ 另由 -Scripts 取得（只算新增的）。
    #>
    param([string]$From, [string]$To = 'HEAD', [switch]$Scripts)
    if ($Scripts) {
        $arguments = if ($From) { @('diff', '--name-only', '--diff-filter=A', "$From..$To", '--', 'database/App/Scripts') } else { @('ls-tree', '-r', '--name-only', $To, '--', 'database/App/Scripts') }
        return @(Invoke-RepoGit @arguments | Where-Object { $_ -like '*.sql' })
    }
    $arguments = if ($From) { @('diff', '--name-status', '--no-renames', "$From..$To", '--', 'database/App') } else { @('ls-tree', '-r', '--name-only', $To, '--', 'database/App') }
    foreach ($line in Invoke-RepoGit @arguments) {
        $status, $file = if ($From) { $line.Split("`t", 2) } else { 'A', $line }
        if ($file -notmatch '^database/App/([^/]+)/([^/]+)/([^/]+)\.sql$' -or $Matches[1] -in $keptProjectFolders) { continue }
        [pscustomobject]@{
            Change = @{ A = 'Added'; M = 'Modified'; D = 'Deleted' }[$status.Substring(0, 1)] ?? 'Modified'
            Type = @{ Tables = 'table'; Views = 'view'; StoredProcedures = 'procedure'; Functions = 'function'; Synonyms = 'synonym'; Sequences = 'sequence'; UserDefinedTypes = 'type' }[$Matches[2]] ?? $Matches[2]
            Name = "$($Matches[1]).$($Matches[3])"
        }
    }
}

function ConvertTo-RepoLikePattern {
    <# .SYNOPSIS SQL LIKE pattern of a PowerShell wildcard (schema.name in export-exclusions.txt): brackets, % and _ escaped, * ? turned into % _. #>
    param([Parameter(Mandatory)][string]$Pattern)
    $Pattern.Replace('[', '[[]').Replace('%', '[%]').Replace('_', '[_]').Replace('*', '%').Replace('?', '_')
}

# update.sql 以 SQLCMD 模式執行：字串常值裡的 $( 會被當成變數，拆成兩段；超過 4000 字的串接要先轉成 nvarchar(max)，否則被截斷。
function ConvertTo-SqlCmdLiteral([AllowEmptyString()][string]$Value) {
    $parts = @($Value.Replace("'", "''") -split '\$\(')
    if ($parts.Count -eq 1) { return "N'$($parts[0])'" }
    "CONVERT(nvarchar(max), N'$($parts[0])')" + -join @($parts | Select-Object -Skip 1 | ForEach-Object { " + N'`$' + N'(' + N'$_'" })
}

function Get-RepoDatabaseBuild {
    <#
    .SYNOPSIS The built database project of a commit: a folder with App.dacpac, FullText/ and Seed/, cached under .local/releases/<commit>/.
    .DESCRIPTION
    以 git worktree 取出該 commit 建置，不動工作目錄；SDK 由 global.json 固定，同一個 commit 建出同一份結構，所以依 commit 快取、不保存 dacpac。
    省略 Ref 時建置工作目錄的 database/App（可能有未提交的修改，不快取）。
    #>
    param([string]$Ref)
    if (!$Ref) {
        $project = Join-Path $root 'database/App'
        dotnet build $project -c Release --nologo -v q | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗；請先執行 scripts/Restore.ps1。' }
        return [pscustomobject]@{ Path = $project; Dacpac = Join-Path $project 'bin/Release/App.dacpac'; Commit = $null }
    }
    $commit = @(Invoke-RepoGit rev-parse --verify "$Ref^{commit}")[0]
    $cache = Join-Path $root ".local/releases/$commit"
    $dacpac = Join-Path $cache 'App.dacpac'
    if (!(Test-Path -LiteralPath $dacpac -PathType Leaf)) {
        $worktree = Join-Path $root ".local/worktrees/$commit"
        if (Test-Path -LiteralPath $worktree) { Invoke-RepoGit worktree remove --force $worktree | Out-Null }
        Invoke-RepoGit worktree add --detach $worktree $commit | Out-Null
        try {
            $project = Join-Path $worktree 'database/App'
            Write-Host "建置 $($commit.Substring(0, 7)) 的資料庫專案"
            dotnet build $project -c Release --nologo -v q | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "$Ref 的資料庫專案 build 失敗。" }
            # 先寫到暫存資料夾再改名：中斷時不會留下缺檔的快取。
            $staging = "$cache.tmp"
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction Ignore
            New-Item -ItemType Directory -Path $staging -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $project 'bin/Release/App.dacpac') -Destination $staging
            foreach ($folder in 'FullText', 'Seed') {
                if (Test-Path -LiteralPath (Join-Path $project $folder)) { Copy-Item -LiteralPath (Join-Path $project $folder) -Destination $staging -Recurse }
            }
            Remove-Item -LiteralPath $cache -Recurse -Force -ErrorAction Ignore
            Move-Item -LiteralPath $staging -Destination $cache
        } finally { Invoke-RepoGit worktree remove --force $worktree | Out-Null }
    }
    [pscustomobject]@{ Path = $cache; Dacpac = $dacpac; Commit = $commit }
}

# SqlPackage 對建庫與更新共用的排除：權限、登入與檔案位置是環境差異，全文物件不在 dacpac（ADR 0010 的發版參數）。
$sqlPackageExcludedTypes = 'Users', 'Logins', 'DatabaseRoles', 'ApplicationRoles', 'RoleMembership', 'Permissions', 'Credentials',
    'DatabaseScopedCredentials', 'ServerRoles', 'ServerRoleMembership', 'ServerTriggers', 'LinkedServers', 'LinkedServerLogins',
    'Endpoints', 'ErrorMessages', 'Audits', 'ServerAuditSpecifications', 'DatabaseAuditSpecifications', 'EventSessions',
    'Certificates', 'MasterKeys', 'DatabaseEncryptionKeys', 'CryptographicProviders', 'Filegroups', 'Files', 'FullTextCatalogs', 'FullTextStoplists'

function Get-RepoSqlPackageOption {
    <#
    .SYNOPSIS SqlPackage /p: options for creating a database (Create) or for the update script between two versions (Update).
    .DESCRIPTION
    Create 才寫入 App.sqlproj 的 collation、相容層級與 RCSI；Update 不動資料庫選項。
    Update 比的是兩份 dacpac，不是客戶庫：起點有、這一版沒有的物件就是被刪除的，所以 DropObjectsNotInSource=True；
    客戶端自己加的物件不在起點，不會被刪，由異動偵測列出。可能遺失資料時腳本執行到該處即停止。
    #>
    param([Parameter(Mandatory)][ValidateSet('Create', 'Update')][string]$Purpose)
    $common = @(
        'BlockOnPossibleDataLoss=True', 'GenerateSmartDefaults=False', "ExcludeObjectTypes=$($sqlPackageExcludedTypes -join ';')",
        'IgnorePermissions=True', 'IgnoreRoleMembership=True', 'IgnoreLoginSids=True', 'IgnoreAuthorizer=True', 'IgnoreUserSettingsObjects=True'
    )
    $specific = if ($Purpose -eq 'Create') { 'ScriptDatabaseOptions=True', 'ScriptDatabaseCollation=True', 'ScriptDatabaseCompatibility=True' }
    else {
        'ScriptDatabaseOptions=False', 'ScriptDatabaseCollation=False', 'ScriptDatabaseCompatibility=False', 'DropObjectsNotInSource=True',
        'IgnoreColumnOrder=True', 'IgnoreFilegroupPlacement=True', 'CommentOutSetVarDeclarations=True'
    }
    @($common + $specific) | ForEach-Object { "/p:$_" }
}

function Invoke-RepoSqlPackage {
    <# .SYNOPSIS Runs the pinned SqlPackage (dotnet tool) from the repository root; prints its output only with -Verbose or on failure. #>
    param([Parameter(Mandatory)][string[]]$Argument)
    Push-Location -LiteralPath $root
    try { $output = dotnet tool run sqlpackage -- @Argument 2>&1 } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) {
        $output | Out-Host
        throw "SqlPackage 失敗（結束碼 $LASTEXITCODE）；原因見上方輸出。找不到工具時先執行 scripts/Restore.ps1。"
    }
    $output | ForEach-Object { Write-Verbose "$_" }
}

function Publish-RepoDacpac {
    <#
    .SYNOPSIS Creates a new database from a dacpac with the Create options (collation, compatibility level and RCSI from App.sqlproj).
    .DESCRIPTION 密碼只寫進目前使用者暫存資料夾的 response file，結束前刪除；SqlPackage 在 response file 中把 \" 當引號，結尾不可是反斜線。
    #>
    param(
        [Parameter(Mandatory)][string]$Dacpac, [Parameter(Mandatory)][string]$Server, [Parameter(Mandatory)][string]$Database,
        [pscredential]$Credential, [switch]$TrustServerCertificate
    )
    $target = [Data.Common.DbConnectionStringBuilder]::new()
    $target['Server'] = $Server
    $target['Database'] = $Database
    $target['Encrypt'] = 'True'
    $target['TrustServerCertificate'] = [string][bool]$TrustServerCertificate
    $target['Persist Security Info'] = 'False'
    if ($Credential) {
        $target['User ID'] = $Credential.UserName
        $target['Password'] = $Credential.GetNetworkCredential().Password
    } else { $target['Integrated Security'] = 'True' }
    $work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-publish-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $value = $target.ConnectionString
        $target = $null
        if ($value.EndsWith('\')) { $value += ';' }
        $response = Join-Path $work 'connection.rsp'
        [IO.File]::WriteAllText($response, "/TargetConnectionString:`"$($value.Replace('"', '\"'))`"")
        Invoke-RepoSqlPackage (@('/Action:Publish', "/SourceFile:$Dacpac", "@$response") + @(Get-RepoSqlPackageOption Create))
    } finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}

function Invoke-RepoSqlCmdScript {
    <#
    .SYNOPSIS Runs a script the way SSMS runs it in SQLCMD mode, supporting what update.sql uses: :setvar, :on error exit and $(name).
    .DESCRIPTION 呼叫端傳入的 Variable 優先於腳本內的 :setvar（例如把 DatabaseName 換成演練庫）；遇到第一個錯誤即停止，等同 :on error exit。
    #>
    param(
        [Parameter(Mandatory)][Data.SqlClient.SqlConnection]$Connection, [Parameter(Mandatory)][string]$Text,
        [hashtable]$Variable = @{}, [string]$Name = 'SQL'
    )
    $values = @{}
    foreach ($key in $Variable.Keys) { $values[$key] = $Variable[$key] }
    $batch = [Text.StringBuilder]::new()
    $number = 0
    foreach ($line in @($Text -split '\r?\n') + 'GO') {
        if ($line -match '^\s*:setvar\s+(\w+)\s+"?([^"]*)"?\s*$') {
            if (!$Variable.ContainsKey($Matches[1])) { $values[$Matches[1]] = $Matches[2] }
            continue
        }
        if ($line -match '^\s*:on\s+error\s+exit\s*$') { continue }
        if ($line -match '^\s*:') { throw "$Name 含不支援的 SQLCMD 指令：$line" }
        if ($line -notmatch '^\s*GO\s*$') { [void]$batch.AppendLine($line); continue }
        $number++
        $sql = [regex]::Replace($batch.ToString(), '\$\((\w+)\)', {
                param($match)
                $key = $match.Groups[1].Value
                if (!$values.ContainsKey($key)) { throw "$Name 第 $number 個批次使用了未定義的 SQLCMD 變數 $key。" }
                $values[$key]
            })
        [void]$batch.Clear()
        if (!$sql.Trim()) { continue }
        $command = $Connection.CreateCommand()
        $command.CommandText = $sql
        $command.CommandTimeout = 0
        try { [void]$command.ExecuteNonQuery() }
        catch { throw "執行 $Name 第 $number 個批次失敗：$($_.Exception.InnerException?.Message ?? $_.Exception.Message)" }
    }
}

function Get-RepoSchemaSnapshot {
    <#
    .SYNOPSIS The schema snapshot of a database, computed with the repository's releases.udf_SchemaObjects body run as an ad-hoc query.
    .DESCRIPTION 以目前版本的算法計算，目標庫不必有 releases schema（起點可能早於這個機制，開發庫也不一定已匯入）。
    #>
    param([Parameter(Mandatory)][Data.SqlClient.SqlConnection]$Connection, [string]$Definition = (Join-Path $root 'database/App/releases/Functions/udf_SchemaObjects.sql'))
    $text = [IO.File]::ReadAllText($Definition)
    if ($text -notmatch '(?s)\bRETURN\s+(WITH\b.*?);?\s*GO\s*$') { throw "$Definition 不是預期的 inline function（RETURN WITH ...）。" }
    $command = $Connection.CreateCommand()
    $command.CommandText = $Matches[1]
    $command.CommandTimeout = 0
    $table = [Data.DataTable]::new()
    $reader = $command.ExecuteReader()
    try { $table.Load($reader) } finally { $reader.Dispose() }
    foreach ($row in $table.Rows) {
        [pscustomobject]@{ SchemaName = $row.SchemaName; Name = $row.Name; Type = $row.Type; Hash = [byte[]]$row.Hash; Definition = $row.Definition; ModifiedAt = $row.ModifiedAt }
    }
}

function Compare-RepoSchemaSnapshot {
    <# .SYNOPSIS Objects that differ between two snapshots (Added, Modified, Removed), ordered by type and name. #>
    param([AllowEmptyCollection()][object[]]$Before = @(), [AllowEmptyCollection()][object[]]$After = @())
    $old = @{}
    foreach ($item in $Before) { $old["$($item.SchemaName).$($item.Name)"] = $item }
    $new = @{}
    foreach ($item in $After) { $new["$($item.SchemaName).$($item.Name)"] = $item }
    $changes = foreach ($key in @($old.Keys) + @($new.Keys) | Select-Object -Unique) {
        $a = $old[$key]
        $b = $new[$key]
        $change = if (!$a) { 'Added' } elseif (!$b) { 'Removed' } elseif ([Convert]::ToHexString($a.Hash) -ne [Convert]::ToHexString($b.Hash)) { 'Modified' }
        if ($change) { [pscustomobject]@{ Change = $change; Type = ($b ?? $a).Type; Name = $key; Before = $a; After = $b } }
    }
    @($changes | Sort-Object Type, Name)
}

function New-RepoRollbackScript {
    <#
    .SYNOPSIS T-SQL that puts the procedures, functions, views and triggers changed between two snapshots back to the first one.
    .DESCRIPTION
    只處理程式物件：改過的與刪除的以起點定義 CREATE OR ALTER，新增的 DROP。資料表無法安全反向（可能已有新資料），一律用備份回復。
    function 在 inline 與多陳述式之間改過時不能 ALTER，要先 DROP；這種情況少見，腳本執行失敗時由 DBA 處理。
    #>
    param([AllowEmptyCollection()][object[]]$Change = @())
    $modules = @($Change | Where-Object { $_.Type -in 'procedure', 'function', 'view', 'trigger' })
    $keyword = @{ procedure = 'PROCEDURE'; function = 'FUNCTION'; view = 'VIEW'; trigger = 'TRIGGER' }
    foreach ($item in $modules | Sort-Object @{ Expression = { if ($_.Change -eq 'Added') { 0 } else { 1 } } }, Name) {
        $schema, $name = $item.Name.Split('.', 2)
        $quoted = "[$($schema.Replace(']', ']]'))].[$($name.Replace(']', ']]'))]"
        if ($item.Change -eq 'Added') {
            "DROP $($keyword[$item.Type]) IF EXISTS $quoted;"
            'GO'
            continue
        }
        $definition = [regex]::new('\bCREATE\s+(?=(PROC|PROCEDURE|FUNCTION|VIEW|TRIGGER)\b)', 'IgnoreCase').Replace($item.Before.Definition, 'CREATE OR ALTER ', 1)
        $definition.TrimEnd()
        'GO'
    }
}

function New-RepoReleaseRecordScript {
    <#
    .SYNOPSIS The last step of update.sql: replaces the expected snapshot and ignore patterns, then records the database version.
    .DESCRIPTION 快照取自演練庫（同一份 update.sql 執行後的結果），所以客戶庫在更新前就存在的修改也會被列為異動（ADR 0028）。
    #>
    param(
        [Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$Revision, [string]$StartPoint,
        [AllowEmptyCollection()][object[]]$Snapshot = @(), [AllowEmptyCollection()][string[]]$IgnorePattern = @()
    )
    'SET XACT_ABORT ON;'
    'BEGIN TRANSACTION;'
    'DELETE FROM [releases].[SchemaObjects];'
    $rows = @($Snapshot | Sort-Object SchemaName, Name)
    for ($i = 0; $i -lt $rows.Count; $i += 100) {
        'INSERT INTO [releases].[SchemaObjects] ([SchemaName], [Name], [Type], [Hash], [Definition]) VALUES'
        $chunk = $rows[$i..([Math]::Min($i + 99, $rows.Count - 1))]
        ($chunk | ForEach-Object {
            "    ($(ConvertTo-SqlCmdLiteral $_.SchemaName), $(ConvertTo-SqlCmdLiteral $_.Name), $(ConvertTo-SqlCmdLiteral $_.Type), 0x$([Convert]::ToHexString($_.Hash)), $(ConvertTo-SqlCmdLiteral $_.Definition))"
        }) -join ",`n"
        ';'
    }
    'DELETE FROM [releases].[IgnoredObjects];'
    foreach ($pattern in $IgnorePattern | Select-Object -Unique) { "INSERT INTO [releases].[IgnoredObjects] ([Pattern]) VALUES ($(ConvertTo-SqlCmdLiteral (ConvertTo-RepoLikePattern $pattern)));" }
    "INSERT INTO [releases].[Deployments] ([Component], [Version], [Revision], [StartPoint], [DeployedAt], [Host]) VALUES (N'database', $(ConvertTo-SqlCmdLiteral $Version), $(ConvertTo-SqlCmdLiteral $Revision), $(if ($StartPoint) { ConvertTo-SqlCmdLiteral $StartPoint } else { 'NULL' }), SYSDATETIMEOFFSET(), LEFT(ISNULL(HOST_NAME(), N''), 128));"
    'COMMIT;'
    'GO'
}

function New-RepoDatabaseUpdateScript {
    <#
    .SYNOPSIS update.sql: version and drift checks, the schema update, full-text and seed data, then the record step; run once in SSMS (SQLCMD mode).
    .DESCRIPTION
    StartVersion 是比對起點的版號；起點不是發版 tag（-From 指定 commit）時為空，目標庫必須還沒有版本紀錄。
    Record 省略時不含第 4 步：演練先執行到第 3 步，再從演練庫取快照組出完整的檔案。
    #>
    param(
        [Parameter(Mandatory)][string]$Version, [string]$StartVersion, [string]$StartPoint, [Parameter(Mandatory)][AllowEmptyString()][string]$Structure,
        [AllowEmptyCollection()][object[]]$Script = @(), [string[]]$Record, [string]$DatabaseName = 'AiNexus'
    )
    $expected = if ($StartVersion) { ConvertTo-SqlCmdLiteral $StartVersion } else { 'NULL' }
    $from = if ($StartPoint) { $StartPoint } else { '空資料庫' }
    @(
        "-- AiNexus 資料庫更新 v$Version（比對起點：$from）"
        '-- 在 SSMS 開啟 SQLCMD 模式（查詢 > SQLCMD 模式）連到目標庫執行；先停站並備份。資料庫名稱不同時改下一行。'
        "--"
        ":setvar DatabaseName `"$DatabaseName`""
        '-- 前置檢查列出手動異動並停止時：確認這些修改可以被覆蓋（或已回收進開發庫），改成 1 後重新執行。'
        ':setvar AcceptDrift "0"'
        ':setvar DefaultFilePrefix "AiNexus"'
        ':setvar DefaultDataPath ""'
        ':setvar DefaultLogPath ""'
        ':on error exit'
        'GO'
        ':setvar __IsSqlCmdEnabled "True"'
        'GO'
        "IF N'`$(__IsSqlCmdEnabled)' NOT LIKE N'True'"
        'BEGIN'
        "    PRINT N'請先開啟 SQLCMD 模式再執行。';"
        '    SET NOEXEC ON;'
        'END'
        'GO'
        'USE [$(DatabaseName)];'
        'GO'
        ''
        '-- ===== 1. 前置檢查 ====='
        "DECLARE @expected nvarchar(64) = $expected, @current nvarchar(64);"
        "IF OBJECT_ID(N'[releases].[Deployments]', N'U') IS NOT NULL"
        "    SELECT TOP (1) @current = [Version] FROM [releases].[Deployments] WHERE [Component] = N'database' ORDER BY [Id] DESC;"
        "IF ISNULL(@current, N'') <> ISNULL(@expected, N'')"
        'BEGIN'
        "    DECLARE @message nvarchar(2048) = FORMATMESSAGE(N'目標庫的資料庫版本是 %s，這份更新包要求 %s。請以 Release.ps1 -From v<目標庫版本> 重新產生更新包。', ISNULL(N'v' + @current, N'（沒有紀錄）'), ISNULL(N'v' + @expected, N'（沒有紀錄）'));"
        '    THROW 50000, @message, 1;'
        'END'
        'GO'
        "IF OBJECT_ID(N'[releases].[usp_ListSchemaChanges]', N'P') IS NOT NULL"
        'BEGIN'
        # 同一個 SSMS 視窗重跑時是同一個工作階段，上次停下時留下的暫存表還在。
        '    DROP TABLE IF EXISTS #changes;'
        '    CREATE TABLE #changes ([SchemaName] sysname, [Name] sysname, [Type] nvarchar(16), [Change] nvarchar(16), [ChangedAt] datetimeoffset(7));'
        '    INSERT INTO #changes EXEC [releases].[usp_ListSchemaChanges];'
        '    IF EXISTS (SELECT 1 FROM #changes)'
        '    BEGIN'
        '        SELECT * FROM #changes ORDER BY [ChangedAt] DESC;'
        "        IF N'`$(AcceptDrift)' <> N'1'"
        "            THROW 50000, N'目標庫在上次更新後有手動異動（見結果），更新可能覆蓋它們。確認後把 AcceptDrift 改成 1 重新執行。', 1;"
        "        PRINT N'已確認手動異動，繼續更新。';"
        '    END'
        '    DROP TABLE #changes;'
        'END'
        'GO'
        ''
        '-- ===== 2. 結構（SqlPackage 以兩份 dacpac 產生）====='
        $Structure.Replace("`r`n", "`n").TrimEnd()
        'GO'
        ''
        '-- ===== 3. 全文、種子資料（可重跑）====='
        @($Script | ForEach-Object { $_.Text.Replace("`r`n", "`n").TrimEnd(); 'GO' })
        ''
        if ($Record) {
            '-- ===== 4. 記錄版本與這一版應有的結構 ====='
            $Record
            "PRINT N'已更新到 v$Version。以下若有結果，是與這一版不同的物件（更新前就有的手動修改），請在網站的版本頁確認。';"
            'EXEC [releases].[usp_ListSchemaChanges];'
            'GO'
        }
    ) -join "`n"
}

function New-RepoIisPackage {
    <#
    .SYNOPSIS Lays out the IIS part of a release package from a win-x64 publish folder: app/, config/ templates, empty keys/ and logs/.
    .DESCRIPTION
    只有 app/ 是 IIS 實體路徑；config/ 只在不存在時由範本建立，Files.StoragePath 與 Diagnostics.Directory 設為 DataRoot 下的
    attachments、diagnostics。套件不含秘密（-IncludeLocalConfig 只用於自己的測試主機），正式主機不需要 PowerShell 或 SqlPackage。
    #>
    param(
        [Parameter(Mandatory)][string]$Publish, [Parameter(Mandatory)][string]$Destination, [Parameter(Mandatory)][string]$DataRoot,
        [switch]$IncludeLocalConfig, [switch]$AllowUntrustedSql
    )
    # Windows 路徑在任何平台都檢查：套件是給 Windows IIS 主機的。
    if ($DataRoot -notmatch '^[A-Za-z]:\\') { throw 'DataRoot 必須是正式主機上的絕對路徑，例如 D:\AiNexus\data。' }
    $deps = Join-Path $Publish 'AiNexus.Host.deps.json'
    if (!(Test-Path -LiteralPath $deps)) { throw "找不到發布產物：$Publish。" }
    # 其他平台的產物缺 Windows 版的 SqlClient 與 AD 元件，在 IIS 上啟動才會失敗，所以在打包時就擋下。
    if ((Get-Content -LiteralPath $deps -Raw | ConvertFrom-Json).runtimeTarget.name -notlike '*/win-x64') { throw '發布產物不是 win-x64；請以 scripts/Build.ps1 -Runtime win-x64 重新建置。' }
    $app = Join-Path $Destination 'app'
    foreach ($directory in @($app, (Join-Path $Destination 'config'), (Join-Path $Destination 'keys'), (Join-Path $Destination 'logs'))) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    Get-ChildItem -LiteralPath $Publish | Where-Object { $_.Name -notin @('logs', 'App_Data') } | Copy-Item -Destination $app -Recurse

    # SDK 對 RID 發布會把 processPath 改成 apphost 卻不動 arguments；兩者不一致時 exe 會收到 dll 參數，IIS 啟動失敗。
    $aspNetCore = ([xml](Get-Content -LiteralPath (Join-Path $app 'web.config') -Raw)).SelectSingleNode('//aspNetCore')
    if (!$aspNetCore -or $aspNetCore.processPath -ne '.\AiNexus.Host.exe' -or $aspNetCore.arguments) {
        throw "web.config 的 aspNetCore 必須是 processPath='.\AiNexus.Host.exe'、arguments 為空；實際為 processPath='$($aspNetCore.processPath)'、arguments='$($aspNetCore.arguments)'。請修 backend/src/AiNexus.Host/web.config。"
    }

    $settingsPath = Join-Path $Destination 'config/appsettings.Production.json'
    $secretsPath = Join-Path $Destination 'config/appsettings.Secrets.json'
    $templates = Join-Path $root 'backend/src/AiNexus.Host'
    $sources = if ($IncludeLocalConfig) { Initialize-RepoLocalSettings } else {
        @{ Settings = Join-Path $templates 'appsettings.Production.example.json'; Secrets = Join-Path $templates 'appsettings.Secrets.example.json' }
    }
    if (!(Test-Path -LiteralPath $settingsPath)) { Copy-Item -LiteralPath $sources.Settings -Destination $settingsPath }
    if (!(Test-Path -LiteralPath $secretsPath)) { Copy-Item -LiteralPath $sources.Secrets -Destination $secretsPath; Protect-RepoSecrets $secretsPath }
    $settings = Read-RepoJson $settingsPath
    Set-RepoSetting $settings 'Files.StoragePath' ($DataRoot.TrimEnd('\') + '\attachments')
    Set-RepoSetting $settings 'Diagnostics.Directory' ($DataRoot.TrimEnd('\') + '\diagnostics')
    if ($AllowUntrustedSql) { Set-RepoSetting $settings 'Databases.App.TrustServerCertificate' $true }
    Save-RepoJson $settingsPath $settings
}

function New-RepoSchemaComparisonFile {
    <#
    .SYNOPSIS Writes a Visual Studio schema comparison (.scmp) of two dacpacs with the update options, through DacFx in a child process.
    .DESCRIPTION
    DacFx 只存絕對路徑（實測相對路徑讀不到），套件搬到別台後要在 VS 重新選擇兩個 dacpac。
    在子程序載入 DacFx，避免它的 SqlClient 與目前工作階段已載入的組件衝突。
    #>
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Target, [Parameter(Mandatory)][string]$Path)
    $config = Read-RepoJson (Join-Path $root '.config/dotnet-tools.json')
    $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
    $tools = Join-Path $nuget "microsoft.sqlpackage/$($config.tools['microsoft.sqlpackage'].version)/tools/net8.0/any"
    if (!(Test-Path -LiteralPath $tools)) { throw "找不到 SqlPackage 的 DacFx：$tools；請先執行 scripts/Restore.ps1。" }
    $options = @(Get-RepoSqlPackageOption Update | ForEach-Object { $_.Substring(3) } | Where-Object { $_ -notlike 'CommentOutSetVarDeclarations=*' })
    $child = @'
param($Tools, $Source, $Target, $Path, $Options)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $Tools 'runtimes/win/lib/net8.0/Microsoft.Data.SqlClient.dll')
Add-Type -Path (Join-Path $Tools 'Microsoft.SqlServer.Dac.dll')
Add-Type -Path (Join-Path $Tools 'Microsoft.SqlServer.Dac.Extensions.dll')
$deploy = [Microsoft.SqlServer.Dac.DacDeployOptions]::new()
foreach ($option in $Options.Split('|')) {
    $name, $value = $option.Split('=', 2)
    $property = $deploy.GetType().GetProperty($name)
    $deploy.$name = if ($name -eq 'ExcludeObjectTypes') { [Microsoft.SqlServer.Dac.ObjectType[]]$value.Split(';') } else { [Convert]::ChangeType($value, $property.PropertyType) }
}
$comparison = [Microsoft.SqlServer.Dac.Compare.SchemaComparison]::new([Microsoft.SqlServer.Dac.Compare.SchemaCompareDacpacEndpoint]::new($Source), [Microsoft.SqlServer.Dac.Compare.SchemaCompareDacpacEndpoint]::new($Target))
$comparison.Options = $deploy
$comparison.SaveToFile($Path, $true)
'@
    $file = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-scmp-' + [Guid]::NewGuid().ToString('N') + '.ps1')
    [IO.File]::WriteAllText($file, $child)
    try {
        pwsh -NoProfile -NonInteractive -File $file -Tools $tools -Source $Source -Target $Target -Path $Path -Options ($options -join '|') | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '產生 compare.scmp 失敗；原因見上方輸出。' }
    } finally { Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue }
}

# 以下是腳本選單（Show-Scripts.ps1）與步驟摘要（Invoke-RepoSteps）用的顯示寬度、腳本目錄與指令組裝。

# 終端機裡中日韓文字與全形標點佔 2 格。East Asian Ambiguous 字元（─ → … ✓ ⚠ 等）依終端機與字型是 1 或 2 格，
# 無法事先知道，這裡一律算 1 格；所以選單與摘要的排版只用 ASCII 與中文，不用這類符號。
$wideRanges = @(
    0x1100, 0x115F, 0x2E80, 0x303E, 0x3041, 0x33FF, 0x3400, 0x4DBF, 0x4E00, 0x9FFF, 0xA000, 0xA4CF, 0xA960, 0xA97F,
    0xAC00, 0xD7A3, 0xF900, 0xFAFF, 0xFE10, 0xFE19, 0xFE30, 0xFE6F, 0xFF00, 0xFF60, 0xFFE0, 0xFFE6,
    0x1F300, 0x1F64F, 0x1F900, 0x1F9FF, 0x20000, 0x3FFFD)

function Get-RuneWidth([int]$Code) {
    if ($Code -ge 0x20 -and $Code -lt 0x7F) { return 1 }
    if ($Code -lt 0xA0) { return 0 }
    if ($Code -lt 0x300) { return 1 }
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($Code) -in 'NonSpacingMark', 'EnclosingMark', 'Format') { return 0 }
    for ($i = 0; $i -lt $wideRanges.Count; $i += 2) {
        if ($Code -lt $wideRanges[$i]) { return 1 }
        if ($Code -le $wideRanges[$i + 1]) { return 2 }
    }
    1
}

function Get-RepoDisplayWidth {
    <# .SYNOPSIS Terminal columns a text occupies: CJK and fullwidth characters take two, combining marks none. #>
    param([AllowEmptyString()][AllowNull()][string]$Text)
    $width = 0
    # 以 rune 計算：CJK 擴充區（例如 𠀀）是一對 surrogate，用 char 會算成兩個字。
    foreach ($rune in "$Text".EnumerateRunes()) { $width += Get-RuneWidth $rune.Value }
    $width
}

function Format-RepoDisplayText {
    <# .SYNOPSIS Text cut to at most Width columns (ending in .. when cut, never splitting a wide character); with -Pad filled with spaces to exactly Width. #>
    param([AllowEmptyString()][AllowNull()][string]$Text, [Parameter(Mandatory)][int]$Width, [switch]$Pad)
    if ($Width -le 0) { return '' }
    $Text = "$Text"
    $used = Get-RepoDisplayWidth $Text
    if ($used -gt $Width) {
        $suffix = '..'.Substring(0, [Math]::Min(2, $Width))
        $limit = $Width - $suffix.Length
        $builder = [Text.StringBuilder]::new()
        $used = 0
        foreach ($rune in $Text.EnumerateRunes()) {
            $size = Get-RuneWidth $rune.Value
            if ($used + $size -gt $limit) { break }
            [void]$builder.Append($rune.ToString())
            $used += $size
        }
        $Text = $builder.ToString() + $suffix
        $used += $suffix.Length
    }
    if ($Pad) { $Text + (' ' * ($Width - $used)) } else { $Text }
}

# 中文排版的避頭點：這些標點不放在行首，換行時把前一個字一起帶到下一行。
$noLineStart = '，。、；：！？）」』〉》】〕,.;:!?)]}'

function Split-RepoDisplayLine {
    <#
    .SYNOPSIS Lines of at most Width columns, keeping existing line breaks.
    .DESCRIPTION 優先在空白處換行（英文單字與指令參數不被切開）；行內沒有合適的空白時在任意兩字之間換行，這正是中文的換行方式。
    #>
    param([AllowEmptyString()][AllowNull()][string]$Text, [Parameter(Mandatory)][int]$Width)
    foreach ($paragraph in "$Text" -split '\r?\n') {
        $runes = [Collections.Generic.List[string]]::new()
        $sizes = [Collections.Generic.List[int]]::new()
        $used = 0
        foreach ($rune in $paragraph.EnumerateRunes()) {
            $character = $rune.ToString()
            $size = Get-RuneWidth $rune.Value
            if ($used + $size -gt $Width -and $runes.Count) {
                $space = $runes.LastIndexOf(' ')
                $cut = if ($character -eq ' ') { $runes.Count }
                elseif ($space -gt 0 -and $space -ge $runes.Count / 3) { $space }
                elseif ($noLineStart.Contains($character) -and $runes.Count -gt 1) { $runes.Count - 1 }
                else { $runes.Count }
                (-join $runes.GetRange(0, $cut)).TrimEnd()
                $skip = if ($cut -lt $runes.Count -and $runes[$cut] -eq ' ') { 1 } else { 0 }
                $runes.RemoveRange(0, $cut + $skip)
                $sizes.RemoveRange(0, $cut + $skip)
                $used = 0
                foreach ($kept in $sizes) { $used += $kept }
                if ($character -eq ' ' -and !$runes.Count) { continue }
            }
            $runes.Add($character)
            $sizes.Add($size)
            $used += $size
        }
        -join $runes
    }
}

function Join-HelpLine([string]$Text) {
    # help 在原始檔裡手動斷行；中文接中文不能補空白（否則顯示成「停止， 不會改動」），英文之間才補。空行分段。
    $paragraphs = [Collections.Generic.List[string]]::new()
    $current = ''
    foreach ($line in "$Text" -split '\r?\n' | ForEach-Object Trim) {
        if (!$line) {
            if ($current) { $paragraphs.Add($current); $current = '' }
            continue
        }
        # 條列項目（- 、* 、1. 開頭）各自一行。
        $current = if ($current -and $line -match '^([-*]|\d+\.)\s') { "$current`n$line" }
        elseif (!$current -or (Get-RuneWidth ([int]$current[-1])) -gt 1 -or (Get-RuneWidth ([int]$line[0])) -gt 1) { $current + $line }
        else { "$current $line" }
    }
    if ($current) { $paragraphs.Add($current) }
    $paragraphs -join "`n"
}

# 腳本選單的分類，取自入口腳本 help 的 .COMPONENT，依這個順序顯示；值是分類標題旁的補充說明。
$scriptCategories = [ordered]@{
    '開發' = '日常啟動與預覽'
    '初次設定' = '新機器或剛 clone 時'
    '建置與檢查' = '提交與送 PR 前'
    '資料庫與合約' = 'API 合約、資料表結構、建新庫'
    '發布' = '發版狀態、發布包與 tag'
}

function Get-RepoScriptCategory {
    <# .SYNOPSIS Script menu categories in display order, each with its note. #>
    $scriptCategories
}

function Resolve-ScriptParameter([object[]]$Parameters, [string]$Name) {
    $exact = @($Parameters | Where-Object Name -EQ $Name)
    if ($exact.Count) { return $exact[0] }
    $prefix = @($Parameters | Where-Object { $_.Name.StartsWith($Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($prefix.Count -eq 1) { $prefix[0] }
}

function ConvertFrom-ArgumentAst([Management.Automation.Language.Ast]$Argument) {
    if ($Argument -is [Management.Automation.Language.ConstantExpressionAst]) { return $Argument.Value }
    if ($Argument -is [Management.Automation.Language.ArrayLiteralAst]) { return , @($Argument.Elements | ForEach-Object { ConvertFrom-ArgumentAst $_ }) }
    $Argument.Extent.Text
}

function ConvertTo-ScriptParameter([Management.Automation.Language.ParameterAst]$Parameter, [Management.Automation.Language.CommentHelpInfo]$Help) {
    $name = $Parameter.Name.VariablePath.UserPath
    $attributes = @($Parameter.Attributes | Where-Object { $_ -is [Management.Automation.Language.AttributeAst] })
    $mandatory = @($attributes | Where-Object { $_.TypeName.Name -eq 'Parameter' } | ForEach-Object NamedArguments |
        Where-Object { $_.ArgumentName -eq 'Mandatory' -and ($_.ExpressionOmitted -or $_.Argument.Extent.Text -eq '$true') }).Count -gt 0
    $set = @($attributes | Where-Object { $_.TypeName.Name -eq 'ValidateSet' })
    $choices = @(if ($set.Count) { $set[0].PositionalArguments | ForEach-Object Value })
    $type = $Parameter.StaticType
    $kind = if ($type -eq [switch]) { 'Switch' }
    elseif ($choices.Count) { 'Choice' }
    elseif ($type -in [int], [long]) { 'Number' }
    elseif ($type -eq [string[]]) { 'List' }
    elseif ($type -in [string], [object]) { 'Text' }
    elseif ($type -eq [pscredential]) { 'Credential' }
    else { 'Unsupported' }
    $default = $Parameter.DefaultValue
    $text = if ($Help -and $Help.Parameters.Keys -contains $name.ToUpperInvariant()) { Join-HelpLine $Help.Parameters[$name.ToUpperInvariant()] } else { '' }
    [pscustomobject]@{
        Name = $name
        Kind = $kind
        Mandatory = $mandatory
        Choices = $choices
        Default = if ($default -is [Management.Automation.Language.ConstantExpressionAst]) { [string]$default.Value } else { $null }
        # 例如 Start-Local 的 PublishDirectory 依環境變數決定；選單只標示「依環境」，不在這裡執行運算式。
        DynamicDefault = $null -ne $default -and $default -isnot [Management.Automation.Language.ConstantExpressionAst] -and $default.Extent.Text -ne '@()'
        Help = $text
    }
}

function ConvertFrom-ScriptExample([string]$Text, [string]$Name, [object[]]$Parameters) {
    $ast = [Management.Automation.Language.Parser]::ParseInput($Text, [ref]$null, [ref]$null)
    $command = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true) |
        Where-Object { "$($_.GetCommandName())" -like "*$Name.ps1" })
    if (!$command.Count) { return }
    $values = [ordered]@{}
    $elements = $command[0].CommandElements
    for ($i = 1; $i -lt $elements.Count; $i++) {
        if ($elements[$i] -isnot [Management.Automation.Language.CommandParameterAst]) { continue }
        $parameter = Resolve-ScriptParameter $Parameters $elements[$i].ParameterName
        if (!$parameter) { continue }
        $argument = $elements[$i].Argument
        if (!$argument -and $parameter.Kind -ne 'Switch' -and $i + 1 -lt $elements.Count -and $elements[$i + 1] -isnot [Management.Automation.Language.CommandParameterAst]) {
            $argument = $elements[++$i]
        }
        if ($parameter.Kind -eq 'Unsupported') { continue }
        # 帳密只記「執行前要詢問」，範例裡的 (Get-Credential) 也是這個意思。
        if ($parameter.Kind -eq 'Credential') { $values[$parameter.Name] = $true; continue }
        $values[$parameter.Name] = if ($parameter.Kind -eq 'Switch') { !$argument -or $argument.Extent.Text -ne '$false' } else { ConvertFrom-ArgumentAst $argument }
    }
    [pscustomobject]@{ Text = $command[0].Extent.Text; Parameters = $values }
}

function Get-RepoScriptCatalog {
    <#
    .SYNOPSIS Entry scripts with their category, help, parsed examples and parameters, ordered for the script menu.
    .DESCRIPTION 只解析原始檔（AST），不執行腳本、也不呼叫 Get-Help：說明以腳本自己的 comment-based help 為唯一來源。
    #>
    param([string]$Path = (Join-Path $root 'scripts'))
    $order = @($scriptCategories.Keys)
    $items = foreach ($file in Get-ChildItem -LiteralPath $Path -Filter '*.ps1' | Where-Object BaseName -NE 'Show-Scripts') {
        $ast = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$null)
        $help = $ast.GetHelpContent()
        $parameters = @(if ($ast.ParamBlock) { $ast.ParamBlock.Parameters | ForEach-Object { ConvertTo-ScriptParameter $_ $help } })
        [pscustomobject]@{
            Name = $file.BaseName
            Path = $file.FullName
            Category = if ($help -and $help.Component) { $help.Component.Trim() } else { '' }
            Synopsis = if ($help) { Join-HelpLine $help.Synopsis } else { '' }
            Description = if ($help -and $help.Description) { Join-HelpLine $help.Description } else { '' }
            Parameters = $parameters
            Examples = @(if ($help) { $help.Examples | ForEach-Object { ConvertFrom-ScriptExample $_ $file.BaseName $parameters } })
        }
    }
    $items | Sort-Object @{ Expression = { $index = $order.IndexOf($_.Category); if ($index -lt 0) { $order.Count } else { $index } } }, Name
}

function Format-Argument($Value) {
    if ($Value -is [int] -or $Value -is [long]) { return [string]$Value }
    $text = [string]$Value
    # 只有字母開頭、沒有空白與特殊字元的值才不加引號：數字開頭會被當成數字（08 變 8、1kb 變 1024），
    # 中文輸入法的全形空白、破折號（—）與彎引號（‘’“”）在 PowerShell 分別是分隔、參數前綴與引號。
    if ($text -match '^[\p{L}_./\\][\p{L}\p{Nd}\p{M}_./\\:=~-]*$' -and $text -notmatch '^\.\d') { return $text }
    # 單引號字串裡，任一種單引號（含 ‘ ’ ‚ ‛）都要重複一次才是字面值。
    "'" + ($text -replace "['\u2018\u2019\u201A\u201B]", '$0$0') + "'"
}

function Format-RepoCommandLine {
    <#
    .SYNOPSIS The command line that runs an entry script with these parameters, quoted so PowerShell reads back exactly the same values.
    .DESCRIPTION 帳密一律寫成 (Get-Credential)，不論傳入的是實際帳密或 [pscredential]::Empty（代表執行前詢問），指令裡不會出現帳號或密碼。
    #>
    param([Parameter(Mandatory)][string]$Name, [Collections.IDictionary]$Parameters = @{})
    $words = [Collections.Generic.List[string]]::new()
    $words.Add("./scripts/$Name.ps1")
    foreach ($key in $Parameters.Keys) {
        $value = $Parameters[$key]
        if ($null -eq $value) { continue }
        if ($value -is [pscredential]) {
            $words.Add("-$key (Get-Credential)")
            continue
        }
        if ($value -is [bool] -or $value -is [switch]) {
            if ($value) { $words.Add("-$key") }
            continue
        }
        $words.Add("-$key")
        $words.Add($(if ($value -is [array]) { (@($value) | ForEach-Object { Format-Argument $_ }) -join ',' } else { Format-Argument $value }))
    }
    $words -join ' '
}

# 選單的共用設定：網站與腳本共用 .local/config 的同一組資料庫設定；入口腳本有同名參數時由選單帶入。
# 帳密另存在 .local/secrets（只有目前使用者可讀），見 Get-RepoSavedCredential。
$sharedSettings = [ordered]@{
    Server = @{ Path = 'Databases.App.Server'; Label = '資料庫伺服器' }
    Database = @{ Path = 'Databases.App.Name'; Label = '資料庫名稱' }
    TrustServerCertificate = @{ Path = 'Databases.App.TrustServerCertificate'; Label = '信任伺服器憑證' }
}

function Get-RepoSharedSetting {
    <#
    .SYNOPSIS Shared script-menu settings (database server, name, certificate trust) with the values the site uses.
    .DESCRIPTION
    依網站的載入順序取值：.local/config/appsettings.Local.json 有值就用它，否則用 appsettings.json 的預設。
    回傳 Values、每個鍵的 Label，以及已儲存的 SQL 帳號（SavedUser）與是否存了密碼（HasSavedPassword）；密碼本身不回傳。
    #>
    param(
        [string]$Path = (Get-RepoLocalPaths).Settings,
        [string]$DefaultsPath = (Join-Path $root 'backend/src/AiNexus.Host/appsettings.json'),
        [string]$SecretsPath = (Get-RepoLocalPaths).Secrets
    )
    $local = if (Test-Path -LiteralPath $Path -PathType Leaf) { Read-RepoJson $Path } else { @{} }
    $defaults = if (Test-Path -LiteralPath $DefaultsPath -PathType Leaf) { Read-RepoJson $DefaultsPath } else { @{} }
    $values = [ordered]@{}
    $labels = [ordered]@{}
    foreach ($key in $sharedSettings.Keys) {
        $value = Get-RepoSetting $local $sharedSettings[$key].Path
        if ($null -eq $value -or "$value" -eq '') { $value = Get-RepoSetting $defaults $sharedSettings[$key].Path }
        $values[$key] = $value
        $labels[$key] = $sharedSettings[$key].Label
    }
    $secrets = if (Test-Path -LiteralPath $SecretsPath -PathType Leaf) { Read-RepoJson $SecretsPath } else { @{} }
    $user = Get-RepoSetting $secrets 'Databases.App.User'
    $integrated = Get-RepoSetting $local 'Databases.App.IntegratedSecurity'
    if ($null -eq $integrated) { $integrated = Get-RepoSetting $defaults 'Databases.App.IntegratedSecurity' }
    [pscustomobject]@{
        Values = $values
        Labels = $labels
        SavedUser = "$user"
        HasSavedPassword = "$(Get-RepoSetting $secrets 'Databases.App.Password')" -ne ''
        IntegratedSecurity = $integrated -eq $true
    }
}

function Get-RepoSavedCredential {
    <# .SYNOPSIS The SQL login saved in the machine secrets file (the one the site uses), or nothing when the user or password is missing. #>
    param([string]$SecretsPath = (Get-RepoLocalPaths).Secrets)
    if (!(Test-Path -LiteralPath $SecretsPath -PathType Leaf)) { return }
    $secrets = Read-RepoJson $SecretsPath
    $user = "$(Get-RepoSetting $secrets 'Databases.App.User')"
    $password = "$(Get-RepoSetting $secrets 'Databases.App.Password')"
    if (!$user -or !$password) { return }
    [pscredential]::new($user, (ConvertTo-SecureString $password -AsPlainText -Force))
}

function Set-RepoSavedCredential {
    <#
    .SYNOPSIS Saves or clears the SQL login in the machine secrets file and restricts the file to the current user.
    .DESCRIPTION 和 Configure-Local.ps1 寫同一個位置：網站也用這組帳密連線。Password 為 $null 時只改帳號、保留原本的密碼；-Clear 刪除帳號與密碼。
    #>
    # Password 不宣告型別：[string] 會把 $null 轉成空字串，「保留原密碼」就變成「清除密碼」。
    param([string]$User, $Password = $null, [switch]$Clear, [string]$SecretsPath = (Get-RepoLocalPaths).Secrets)
    if (!(Test-Path -LiteralPath $SecretsPath -PathType Leaf) -and $SecretsPath -eq (Get-RepoLocalPaths).Secrets) { Initialize-RepoLocalSettings | Out-Null }
    $secrets = Read-RepoJson $SecretsPath
    if ($Clear) {
        Set-RepoSetting $secrets 'Databases.App.User' ''
        Set-RepoSetting $secrets 'Databases.App.Password' ''
    } else {
        Set-RepoSetting $secrets 'Databases.App.User' $User
        if ($null -ne $Password) { Set-RepoSetting $secrets 'Databases.App.Password' ([string]$Password) }
    }
    Save-RepoJson $SecretsPath $secrets
    Protect-RepoSecrets $SecretsPath
}

function Set-RepoSharedSetting {
    <# .SYNOPSIS Writes shared script-menu settings into the machine settings file; keys other than the shared ones are ignored. #>
    param([Parameter(Mandatory)][Collections.IDictionary]$Values, [string]$Path = (Get-RepoLocalPaths).Settings)
    # 第一次使用時與 Configure-Local.ps1 一樣由範本建立 .local 設定，不另外產生一份格式不同的檔案。
    if (!(Test-Path -LiteralPath $Path -PathType Leaf) -and $Path -eq (Get-RepoLocalPaths).Settings) { Initialize-RepoLocalSettings | Out-Null }
    $settings = Read-RepoJson $Path
    foreach ($key in $Values.Keys) { if ($sharedSettings.Contains($key)) { Set-RepoSetting $settings $sharedSettings[$key].Path $Values[$key] } }
    Save-RepoJson $Path $settings
}

function Get-RepoScriptHistory {
    <# .SYNOPSIS Recently run entry scripts with their parameters, newest first; nothing when the file is missing or unreadable. #>
    param([Parameter(Mandatory)][string]$Path)
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    # 歷程只是方便用的紀錄，壞掉就當作沒有，不讓選單因此打不開。
    try { $data = Read-RepoJson $Path } catch { return }
    if ($data -isnot [Collections.IDictionary] -or !$data.Contains('Recent')) { return }
    foreach ($entry in @($data['Recent'])) {
        if ($entry -isnot [Collections.IDictionary] -or !$entry['Name']) { continue }
        $parameters = [ordered]@{}
        if ($entry['Parameters'] -is [Collections.IDictionary]) { foreach ($key in $entry['Parameters'].Keys) { $parameters[$key] = $entry['Parameters'][$key] } }
        [pscustomobject]@{ Name = [string]$entry['Name']; Parameters = $parameters }
    }
}

function Add-RepoScriptHistory {
    <# .SYNOPSIS Records a run at the top of the history, keeping only the latest run of each script; credentials are stored only as "ask before running". #>
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Name, [Collections.IDictionary]$Parameters = @{}, [int]$Limit = 5)
    $recorded = [ordered]@{}
    foreach ($key in $Parameters.Keys) { $recorded[$key] = if ($Parameters[$key] -is [pscredential]) { $true } else { $Parameters[$key] } }
    $recent = @([ordered]@{ Name = $Name; Parameters = $recorded }) + @(Get-RepoScriptHistory $Path | Where-Object Name -NE $Name |
            Select-Object -First ($Limit - 1) | ForEach-Object { [ordered]@{ Name = $_.Name; Parameters = $_.Parameters } })
    New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
    Save-RepoJson $Path ([ordered]@{ Recent = $recent })
}

# Invoke-RepoSteps 巢狀時（Verify 的一步呼叫 Build.ps1）只有最外層印摘要、改分頁標題與進度。
# 深度記在全域變數 RepoStepDepth：每支腳本都以 Import-Module -Force 載入，模組重新載入時模組變數會歸零。

function Format-StepText([string]$Text, [string]$Style) {
    if ($PSStyle.OutputRendering -eq 'PlainText' -or $env:NO_COLOR) { return $Text }
    $code = switch ($Style) {
        'Heading' { $PSStyle.Bold + $PSStyle.Foreground.Cyan }
        'Pass' { $PSStyle.Foreground.Green }
        'Fail' { $PSStyle.Bold + $PSStyle.Foreground.Red }
        'Dim' { $PSStyle.Dim }
        default { '' }
    }
    $code + $Text + $PSStyle.Reset
}

function Format-StepElapsed([TimeSpan]$Elapsed) {
    '{0}:{1:00}' -f [int][Math]::Floor($Elapsed.TotalMinutes), $Elapsed.Seconds
}

function Write-StepSummary([string]$Title, [object[]]$Results, [TimeSpan]$Elapsed, [bool]$Passed) {
    $labels = @($Results | ForEach-Object { "[$($_.Number)] $($_.Name)" })
    $width = ($labels | ForEach-Object { Get-RepoDisplayWidth $_ } | Measure-Object -Maximum).Maximum
    Write-Host ''
    Write-Host (Format-StepText "$Title：$(if ($Passed) { '通過' } else { '失敗' })（共 $(Format-StepElapsed $Elapsed)）" $(if ($Passed) { 'Pass' } else { 'Fail' }))
    for ($i = 0; $i -lt $Results.Count; $i++) {
        $result = $Results[$i]
        $style = switch ($result.Status) { '成功' { 'Pass' } '失敗' { 'Fail' } default { 'Dim' } }
        $time = if ($result.Elapsed) { (Format-StepElapsed $result.Elapsed).PadLeft(5) } else { '     ' }
        $line = '  ' + (Format-RepoDisplayText $labels[$i] $width -Pad) + '  ' + (Format-StepText (Format-RepoDisplayText $result.Status 6 -Pad) $style) + '  ' + $time
        if ($result.Detail) { $line += '  ' + $result.Detail }
        Write-Host $line
        if ($result.Hint) { Write-Host ((' ' * ($width + 17)) + $result.Hint) }
    }
}

function Invoke-RepoSteps {
    <#
    .SYNOPSIS Runs named steps in order with [n/total] headings, then prints each step's result and time, also when one fails.
    .DESCRIPTION
    每一步是 @{ Name = '名稱'; Run = { ... }; Skip = '略過原因'（有值就略過）; Hint = '失敗時去哪裡看' }。
    第一個失敗之後的步驟標為「未執行」，摘要印完才拋出原本的錯誤，結束碼不變。
    步驟直接執行、不接管它的輸出：dotnet、npm 的輸出一旦被管線接走就會判定為重導，失去顏色與即時進度。
    最外層在 GitHub Actions 把每一步包成可折疊的 ::group::；在 Windows Terminal 另外更新分頁標題與工作列進度（OSC 9;4）。
    #>
    param([Parameter(Mandatory)][string]$Title, [Parameter(Mandatory)][Collections.IDictionary[]]$Step)
    $depth = [int](Get-Variable RepoStepDepth -Scope Global -ValueOnly -ErrorAction Ignore)
    $outer = $depth -eq 0
    $indent = '  ' * $depth
    $global:RepoStepDepth = $depth + 1
    $actions = $env:GITHUB_ACTIONS -eq 'true'
    $terminal = $outer -and !$actions -and $env:WT_SESSION -and ![Console]::IsOutputRedirected
    $previousTitle = $null
    if ($terminal) { try { $previousTitle = $Host.UI.RawUI.WindowTitle } catch { $terminal = $false } }
    $results = [Collections.Generic.List[object]]::new()
    $failure = $null
    $total = [Diagnostics.Stopwatch]::StartNew()
    try {
        for ($i = 0; $i -lt $Step.Count; $i++) {
            $item = $Step[$i]
            $label = "[$($i + 1)/$($Step.Count)] $($item['Name'])"
            $result = [pscustomobject]@{ Number = $i + 1; Name = $item['Name']; Status = '未執行'; Elapsed = $null; Detail = ''; Hint = '' }
            $results.Add($result)
            if ($failure) { continue }
            if ($item['Skip']) {
                $result.Status = '略過'
                $result.Detail = $item['Skip']
                Write-Host (Format-StepText "$indent$label：略過（$($item['Skip'])）" 'Dim')
                continue
            }
            if ($actions -and $outer) { Write-Host "::group::$label" }
            Write-Host (Format-StepText "$indent$label" 'Heading')
            if ($terminal) {
                $Host.UI.RawUI.WindowTitle = "$label - $Title"
                [Console]::Write("`e]9;4;1;$([int](100 * $i / $Step.Count))`a")
            }
            $watch = [Diagnostics.Stopwatch]::StartNew()
            try {
                & $item['Run']
                $result.Status = '成功'
            } catch {
                $failure = $_
                $result.Status = '失敗'
                $result.Detail = @("$($_.Exception.Message)" -split '\r?\n')[0]
                $result.Hint = "$($item['Hint'])"
                if ($terminal) { [Console]::Write("`e]9;4;2;100`a") }
            } finally {
                $result.Elapsed = $watch.Elapsed
                if ($actions -and $outer) { Write-Host '::endgroup::' }
            }
        }
        if ($outer) { Write-StepSummary $Title $results $total.Elapsed ($null -eq $failure) }
        if ($failure) { throw $failure }
    } finally {
        if ($outer) { Remove-Variable RepoStepDepth -Scope Global -ErrorAction Ignore } else { $global:RepoStepDepth = $depth }
        if ($terminal) {
            $Host.UI.RawUI.WindowTitle = $previousTitle
            [Console]::Write("`e]9;4;0;0`a")
        }
    }
}

Export-ModuleMember -Function *-Repo*
