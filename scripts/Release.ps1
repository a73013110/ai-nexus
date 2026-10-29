#requires -Version 7.4

<#
.SYNOPSIS
發版的唯一入口：不帶參數列出狀態；-Publish 產生發布包、演練資料庫更新並建立 tag；-Preview 只產生發布包。

.DESCRIPTION
版號 YYYY.MM.N 只存在 annotated tag（v2026.11.4），tag 說明的 Components trailer 記錄這次發了哪些部分（ADR 0027）。
元件依路徑判定：database/ 是資料庫，backend/、frontend/、contracts/ 是程式；各自和上一個含該元件的 tag 比較，有變更才放入。
還沒有任何含資料庫的發版時（正式機與開發共用同一個庫）只發程式。

發布包在 artifacts/releases/v<版號>/：README.md（執行順序）、app/（升級只替換它）與 database/
（update.sql、changes.md、compare.scmp、rollback.sql、manual/）；首次部署加 -DataRoot 另有 config/ 範本與空的 keys/、logs/。
資料庫更新包以起點 tag 與目前 commit 各建一份 dacpac 比對產生（git worktree，依 commit 快取在 .local/releases/），
並在 LocalDB 演練：以起點建庫、執行 update.sql，再和目前的 dacpac 比對必須沒有差異；預期快照取自演練庫。

-Publish 要求工作目錄乾淨、位於 master 或 hotfix/*；演練通過才建立 tag，確認後推送。

.PARAMETER Publish
發版：產生發布包、演練通過後建立 annotated tag。

.PARAMETER Preview
只產生發布包（v<下一版>-preview），不建立 tag；可以有未提交的修改，用來審核或在測試主機試裝。

.PARAMETER Only
只發其中一個部分：Database（程式還在跑內規流程時先發資料庫）或 App。省略時有變更的部分都放入。

.PARAMETER From
資料庫更新包的比對起點：目標庫版本頁顯示的版本（例如 v2026.11.3），或客戶庫尚未有版本紀錄時它對應的 commit。
省略時為上一個含資料庫的 tag。目標庫記錄的版本必須等於起點，update.sql 才會執行。

.PARAMETER DataRoot
首次部署（新主機）才需要：正式主機上站外資料的根目錄（絕對路徑），例如 D:\AiNexus\data。
給了才產生 config/ 範本（附件與診斷目錄設在其下）與空的 keys/、logs/；升級只替換 app/，不必給。

.PARAMETER AllowUntrustedSql
首次部署的 config 設定 Databases.App.TrustServerCertificate=true，用於自簽憑證的 SQL Server；要和 -DataRoot 一起用。

.PARAMETER IncludeLocalConfig
首次部署的 config 以這台機器 .local/ 的設定與秘密取代範本（只用於自己的測試主機）；要和 -DataRoot 一起用。

.COMPONENT
發布

.EXAMPLE
./scripts/Release.ps1

.EXAMPLE
./scripts/Release.ps1 -Publish

.EXAMPLE
./scripts/Release.ps1 -Publish -DataRoot 'D:\AiNexus\data'   # 新主機首次部署

.EXAMPLE
./scripts/Release.ps1 -Publish -Only Database

.EXAMPLE
./scripts/Release.ps1 -Preview -Only Database -From v2026.11.3
#>
[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$Preview,
    [ValidateSet('App', 'Database')][string]$Only,
    [string]$From,
    [string]$DataRoot,
    [switch]$AllowUntrustedSql,
    [switch]$IncludeLocalConfig
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
if ($Publish -and $Preview) { throw '-Publish 與 -Preview 只能擇一。' }
$appPaths = @('backend', 'frontend', 'contracts')
$labels = @{ app = '程式'; database = '資料庫' }

$branch = @(Invoke-RepoGit rev-parse --abbrev-ref HEAD)[0]
$head = @(Invoke-RepoGit rev-parse HEAD)[0]
$dirty = @(Invoke-RepoGit status --porcelain)
$reachable = @(Get-RepoReleaseTag -Merged)
$next = Get-RepoNextVersion @(Get-RepoReleaseTag | ForEach-Object Version)
$base = @{}
foreach ($component in 'app', 'database') { $base[$component] = $reachable | Where-Object { $_.Components -contains $component } | Select-Object -First 1 }
$appSince = if ($base.app) { $base.app.Commit }

# 比對起點：-From 可以是發版 tag 或 commit；不是發版 tag 時，目標庫必須還沒有版本紀錄。
$start = if ($From) {
    $tag = @(Get-RepoReleaseTag) | Where-Object { $_.Tag -eq $From -or $_.Version -eq $From } | Select-Object -First 1
    if ($tag) { [pscustomobject]@{ Label = $tag.Tag; Version = $tag.Version; Commit = $tag.Commit } }
    else { $commit = @(Invoke-RepoGit rev-parse --verify "$From^{commit}")[0]; [pscustomobject]@{ Label = $commit.Substring(0, 7); Version = $null; Commit = $commit } }
} elseif ($base.database) { [pscustomobject]@{ Label = $base.database.Tag; Version = $base.database.Version; Commit = $base.database.Commit } }

# 各元件自上次含它的發版以來改過的檔案；預覽另含未提交的修改。
function Get-ChangedFile([string]$Since, [string[]]$Path) {
    $range = @(if ($Since) { $Since; if (!$Preview) { 'HEAD' } })
    $arguments = if ($range.Count) { @('diff', '--name-only') + $range + '--' + $Path } else { @('ls-files', '--') + $Path }
    $files = @(Invoke-RepoGit @arguments)
    if ($Preview) { $untracked = @('ls-files', '--others', '--exclude-standard', '--') + $Path; $files += @(Invoke-RepoGit @untracked) }
    @($files | Where-Object { $_ })
}
$changed = [ordered]@{
    app = @(Get-ChangedFile $appSince $appPaths).Count -gt 0
    database = @(Get-ChangedFile $(if ($start) { $start.Commit }) @('database')).Count -gt 0
}

if (!$Publish -and !$Preview) {
    Write-Host "AI Nexus 發版狀態（$branch，$($head.Substring(0, 7))$(if ($dirty.Count) { "，$($dirty.Count) 個未提交的修改" })）"
    $last = $reachable | Select-Object -First 1
    Write-Host ("  上次發版    " + $(if ($last) { "$($last.Tag)（$($last.Date.ToString('yyyy-MM-dd'))，$(($last.Components | ForEach-Object { $labels[$_] }) -join '、')）" } else { '（沒有）' }))
    Write-Host "  下一個版號  v$next"
    Write-Host ''
    $commits = @(Get-RepoConventionalCommit -From $appSince -Path $appPaths)
    Write-Host "程式（自 $(if ($base.app) { $base.app.Tag } else { '第一個 commit' })）：$(if ($changed.app) { "有變更，$($commits.Count) 個 feat／fix" } else { '沒有變更' })"
    # 只列最近 15 個，完整清單在發版時寫進 tag 與 version.json。
    if ($commits.Count -gt 15) { Write-Host "  …較早的 $($commits.Count - 15) 個略過" }
    foreach ($item in $commits | Select-Object -Last 15) { Write-Host "  $($item.Type)$(if ($item.Scope) { "($($item.Scope))" }): $($item.Subject)" }
    $objects = @(if ($start) { Get-RepoDatabaseChange -From $start.Commit } else { Get-RepoDatabaseChange })
    Write-Host "資料庫（自 $(if ($start) { $start.Label } else { '第一個 commit' })）：$(if ($changed.database) { "有變更，$($objects.Count) 個物件" } else { '沒有變更' })"
    $names = @{ Added = '新增'; Modified = '修改'; Deleted = '刪除' }
    # 還沒有起點時整個結構都算新增，只列數量。
    if ($start) { foreach ($item in $objects) { Write-Host "  $($names[$item.Change]) $($item.Type) $($item.Name)" } }
    else { Write-Host '  還沒有含資料庫的發版：-Publish 只發程式（正式機與開發共用同一個庫時）。第一次發資料庫以 -Only Database -From 指定目標庫對應的 commit，新客戶改用 Initialize-Database.ps1 建庫。' }
    foreach ($file in @(Get-RepoDatabaseChange -Scripts -From $(if ($start) { $start.Commit }))) { Write-Host "  一次性資料修正 $file" }

    # 開發庫在最後一次提交匯出之後改過的物件＝可能還沒匯出；查不到就略過，不影響狀態。
    $shared = Get-RepoSharedSetting
    $since = @(Invoke-RepoGit log -1 --format=%cI -- database/App)[0]
    if ($shared.Values.Server -and $shared.Values.Database -and $since) {
        try {
            $credential = if ($shared.IntegratedSecurity -or "$($shared.Values.Server)" -like '(localdb)*') { $null } else { Get-RepoSavedCredential }
            if (!$credential -and !$shared.IntegratedSecurity -and "$($shared.Values.Server)" -notlike '(localdb)*') { throw '沒有儲存 SQL 帳密' }
            $connection = New-RepoSqlConnection -Server $shared.Values.Server -Database $shared.Values.Database -Credential $credential -TrustServerCertificate:([bool]$shared.Values.TrustServerCertificate)
            $connection.ConnectionString += ';Connect Timeout=5'
            try {
                $connection.Open()
                $command = $connection.CreateCommand()
                $command.CommandText = "SELECT s.[name] + N'.' + o.[name], o.[type_desc], o.[modify_date] FROM sys.objects AS o JOIN sys.schemas AS s ON s.[schema_id] = o.[schema_id] WHERE o.[is_ms_shipped] = 0 AND o.[type] IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'TR', 'SN') AND s.[name] <> N'releases' AND o.[modify_date] > @Since ORDER BY o.[modify_date] DESC"
                [void]$command.Parameters.AddWithValue('@Since', [DateTimeOffset]::Parse($since).LocalDateTime)
                $reader = $command.ExecuteReader()
                $pending = @(while ($reader.Read()) { "  $($reader.GetString(0))（$($reader.GetString(1).ToLowerInvariant())，$($reader.GetDateTime(2).ToString('MM-dd HH:mm'))）" })
                Write-Host "開發庫 $($shared.Values.Database)：$(if ($pending.Count) { "$($pending.Count) 個物件在最後一次提交匯出後修改過，可能還沒匯出（scripts/Export-Database.ps1）" } else { '沒有匯出後才修改的物件' })"
                $pending | ForEach-Object { Write-Host $_ }
            } finally { $connection.Dispose() }
        } catch { Write-Host "開發庫：未檢查（$($_.Exception.InnerException?.Message ?? $_.Exception.Message)）" }
    }
    Write-Host ''
    Write-Host '發版：./scripts/Release.ps1 -Publish（新主機首次部署加 -DataRoot <站外資料目錄>；只發資料庫加 -Only Database）'
    return
}

# 以下產生發布包。
if ($Publish) {
    if ($dirty.Count) { throw "工作目錄有 $($dirty.Count) 個未提交的修改；發版只從已提交的 commit 建置。先提交，或用 -Preview 試產。" }
    if ($branch -ne 'master' -and $branch -notlike 'hotfix/*') { throw "發版只能在 master 或 hotfix/* 分支（目前 $branch）。修舊版請從 tag 開 hotfix/* 分支。" }
    $tagged = @(Get-RepoReleaseTag) | Where-Object Commit -EQ $head
    if ($tagged) { throw "HEAD 已經是 $($tagged[0].Tag)。" }
}
$components = @($changed.Keys | Where-Object { $changed[$_] -and (!$Only -or $_ -eq $Only.ToLowerInvariant()) })
# 還沒有含資料庫的發版＝正式機與開發共用同一個庫（尚未上線），結構已經在目標庫裡，沒有東西要發；明確要發資料庫時才停下。
if ('database' -in $components -and !$start -and !$Only) {
    $components = @($components | Where-Object { $_ -ne 'database' })
    Write-Warning '還沒有含資料庫的發版，略過資料庫（共用開發庫時正常）。正式庫分開後以 -Only Database -From <分庫時的 commit> 發第一次資料庫。'
}
if (!$components.Count) {
    throw "$(if ($Only) { "$($labels[$Only.ToLowerInvariant()])" } else { '程式與資料庫' })自上次發版沒有變更，不需要發版。"
}
if ('database' -in $components -and !$start) {
    throw '還沒有含資料庫的發版 tag，無法決定比對起點。新客戶以 scripts/Initialize-Database.ps1 建庫；既有的客戶庫以 -From <commit> 指定它目前對應的 commit。'
}
if ($DataRoot -and 'app' -notin $components) { throw '-DataRoot 只用於放入程式的首次部署。' }
if (!$DataRoot -and ($IncludeLocalConfig -or $AllowUntrustedSql)) { throw '-IncludeLocalConfig 與 -AllowUntrustedSql 只影響首次部署的 config，要和 -DataRoot 一起用。' }

$version = if ($Publish) { $next } else { "$next-preview" }
$package = Resolve-RepoArtifactPath "artifacts/releases/v$version"
if (Test-Path -LiteralPath $package) {
    if ((Get-Item -LiteralPath $package).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '拒絕清空經由連結的輸出目錄。' }
    Remove-Item -LiteralPath $package -Recurse -Force
}
New-Item -ItemType Directory -Path $package -Force | Out-Null
$work = Join-Path $root ".local/releases/work-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Path $work -Force | Out-Null
# SSMS 開檔要有 BOM 才不會把中文註解與 N'' 字串當成系統字碼頁；行尾用 CRLF。
$sqlEncoding = [Text.UTF8Encoding]::new($true)
function Save-Text([string]$Path, [string]$Text, [switch]$Sql) {
    New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
    $content = $Text.Replace("`r`n", "`n").TrimEnd() + "`n"
    if ($Sql) { [IO.File]::WriteAllText($Path, $content.Replace("`n", "`r`n"), $sqlEncoding) } else { [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false)) }
}

$notes = @(Get-RepoConventionalCommit -From $appSince -Path $appPaths)
$databaseVersion = if ('database' -in $components) { $version } elseif ($base.database) { $base.database.Version }
$schemaChanges = @()
$manual = @()
$rehearsal = $null
$steps = [Collections.Generic.List[Collections.IDictionary]]::new()

if ('app' -in $components) {
    $steps.Add(@{ Name = '建置程式'; Run = {
                $publishFolder = 'artifacts/releases/.publish'
                & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $publishFolder -Runtime win-x64 -Version $version
                New-RepoIisPackage -Publish (Resolve-RepoArtifactPath $publishFolder) -Destination $package -DataRoot $DataRoot -IncludeLocalConfig:$IncludeLocalConfig -AllowUntrustedSql:$AllowUntrustedSql
                # 網站從內容根目錄讀取（關於對話框、需要的最低資料庫版本）；程式的版號另寫在組件裡。
                $info = [ordered]@{
                    version = $version
                    revision = $head
                    builtAt = [DateTimeOffset]::UtcNow.ToString('o')
                    minimumDatabaseVersion = $databaseVersion
                    changes = @($notes | ForEach-Object { [ordered]@{ type = $_.Type; scope = $_.Scope; subject = $_.Subject } })
                }
                Save-Text (Join-Path $package 'app/version.json') ($info | ConvertTo-Json -Depth 4)
            }
        })
}

if ('database' -in $components) {
    $steps.Add(@{ Name = '建置兩份 dacpac'; Run = {
                $script:current = Get-RepoDatabaseBuild -Ref $(if ($Publish) { $head })
                $script:origin = Get-RepoDatabaseBuild -Ref $start.Commit
            }
        })
    $steps.Add(@{ Name = '產生結構更新'; Run = {
                $structure = Join-Path $work 'structure.sql'
                Invoke-RepoSqlPackage (@('/Action:Script', "/SourceFile:$($current.Dacpac)", "/TargetFile:$($origin.Dacpac)", '/TargetDatabaseName:AiNexus', "/OutputPath:$structure") + @(Get-RepoSqlPackageOption Update))
                $script:scripts = @(Get-RepoReleaseScript $current.Path)
                $script:draft = @{
                    Version = $version; StartVersion = $start.Version; StartPoint = $start.Label
                    Structure = [IO.File]::ReadAllText($structure); Script = $scripts
                }
            }
        })
    $steps.Add(@{ Name = '在 LocalDB 演練'; Hint = '演練庫保留在 (localdb)\MSSQLLocalDB，查完自行刪除。'; Run = {
                $server = '(localdb)\MSSQLLocalDB'
                $script:rehearsal = "AiNexus_Rehearsal_$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
                Publish-RepoDacpac -Dacpac $origin.Dacpac -Server $server -Database $rehearsal
                $connection = New-RepoSqlConnection -Server $server -Database $rehearsal
                $connection.Open()
                try {
                    # 起點庫要像已經更新到起點的客戶庫：有起點的版本紀錄。
                    if ($start.Version) {
                        $command = $connection.CreateCommand()
                        $command.CommandText = "IF OBJECT_ID(N'[releases].[Deployments]', N'U') IS NOT NULL INSERT INTO [releases].[Deployments] ([Component], [Version], [Revision], [DeployedAt], [Host]) VALUES (N'database', @Version, @Revision, SYSDATETIMEOFFSET(), N'rehearsal')"
                        [void]$command.Parameters.AddWithValue('@Version', $start.Version)
                        [void]$command.Parameters.AddWithValue('@Revision', $start.Commit)
                        [void]$command.ExecuteNonQuery()
                    }
                    $before = @(Get-RepoSchemaSnapshot $connection)
                    Invoke-RepoSqlCmdScript $connection (New-RepoDatabaseUpdateScript @draft) -Variable @{ DatabaseName = $rehearsal } -Name 'update.sql（演練）'
                    $after = @(Get-RepoSchemaSnapshot $connection)

                    $report = Join-Path $work 'verify.xml'
                    Invoke-RepoSqlPackage (@('/Action:DeployReport', "/SourceFile:$($current.Dacpac)", "/TargetConnectionString:Server=$server;Database=$rehearsal;Integrated Security=True;Encrypt=False", "/OutputPath:$report") + @(Get-RepoSqlPackageOption Update | Where-Object { $_ -notlike '*CommentOutSetVarDeclarations*' }))
                    $operations = @(([xml][IO.File]::ReadAllText($report)).GetElementsByTagName('Item') | ForEach-Object { "$($_.Type) $($_.Value)" })
                    if ($operations.Count) { throw "演練庫更新後仍與這一版不同（$($operations.Count) 項）：$($operations -join '；')" }

                    $script:record = @(New-RepoReleaseRecordScript -Version $version -Revision $head -StartPoint $start.Label -Snapshot $after -IgnorePattern @(Read-RepoExclusionList (Join-Path $current.Path 'export-exclusions.txt')))
                    Invoke-RepoSqlCmdScript $connection ((@('USE [$(DatabaseName)];', 'GO') + $record) -join "`n") -Variable @{ DatabaseName = $rehearsal } -Name '記錄步驟（演練）'
                    $check = $connection.CreateCommand()
                    $check.CommandText = 'EXEC [releases].[usp_ListSchemaChanges]'
                    $reader = $check.ExecuteReader()
                    $left = @(while ($reader.Read()) { "$($reader.GetString(0)).$($reader.GetString(1))" })
                    $reader.Dispose()
                    if ($left.Count) { throw "寫入快照後演練庫仍有異動：$($left -join '、')；releases.udf_SchemaObjects 的算法不穩定。" }
                    $script:schemaChanges = @(Compare-RepoSchemaSnapshot $before $after)
                } finally { $connection.Dispose() }
                $master = New-RepoSqlConnection -Server $server
                $master.Open()
                try {
                    $drop = $master.CreateCommand()
                    $drop.CommandText = "ALTER DATABASE [$rehearsal] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$rehearsal];"
                    [void]$drop.ExecuteNonQuery()
                } finally { $master.Dispose() }
                $script:rehearsal = $null
            }
        })
    $steps.Add(@{ Name = '寫入資料庫更新包'; Run = {
                $folder = Join-Path $package 'database'
                Save-Text (Join-Path $folder 'update.sql') (New-RepoDatabaseUpdateScript @draft -Record $record) -Sql
                $rollback = @(New-RepoRollbackScript $schemaChanges)
                Save-Text (Join-Path $folder 'rollback.sql') ((@(
                            "-- v$version 回復到 $($start.Label)：只含 procedure、function、view、觸發程序；資料表一律以更新前的備份回復。"
                            '-- 在 SSMS 連到目標庫執行（一般模式即可）；執行後版本紀錄不變，網站的版本頁會把這些物件列為異動，直到重新更新。'
                            'GO'
                        ) + $(if ($rollback.Count) { $rollback } else { '-- 這一版沒有改程式物件。' })) -join "`n") -Sql

                $compare = Join-Path $folder 'compare'
                New-Item -ItemType Directory -Path $compare -Force | Out-Null
                $sourceDacpac = Join-Path $compare "v$version.dacpac"
                $targetDacpac = Join-Path $compare "$($start.Label).dacpac"
                Copy-Item -LiteralPath $current.Dacpac -Destination $sourceDacpac
                Copy-Item -LiteralPath $origin.Dacpac -Destination $targetDacpac
                New-RepoSchemaComparisonFile -Source $sourceDacpac -Target $targetDacpac -Path (Join-Path $folder 'compare.scmp')

                $script:manual = @(Get-RepoDatabaseChange -Scripts -From $start.Commit)
                if ($Preview) { $script:manual += @(Invoke-RepoGit ls-files --others --exclude-standard -- database/App/Scripts | Where-Object { $_ -like '*.sql' }) }
                foreach ($file in $manual) {
                    New-Item -ItemType Directory -Path (Join-Path $folder 'manual') -Force | Out-Null
                    Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $folder 'manual')
                }

                $kinds = @{ Added = '新增'; Modified = '修改'; Removed = '刪除' }
                $lines = @(
                    "# v$version 資料庫變更"
                    ''
                    "比對起點 $($start.Label)，取自演練庫更新前後的結構快照；完整差異以 Visual Studio 開啟 compare.scmp。"
                    ''
                    '| 變更 | 種類 | 物件 |'
                    '| --- | --- | --- |'
                    @($schemaChanges | ForEach-Object { "| $($kinds[$_.Change]) | $($_.Type) | ``$($_.Name)`` |" })
                    if (!$schemaChanges.Count) { '| — | — | 結構沒有變更（只有種子資料或全文） |' }
                )
                foreach ($table in $schemaChanges | Where-Object { $_.Type -eq 'table' -and $_.Change -eq 'Modified' }) {
                    $old = @($table.Before.Definition -split "`n")
                    $new = @($table.After.Definition -split "`n")
                    $lines += @('', "## $($table.Name)", '', '```diff')
                    $lines += @($old | Where-Object { $_ -notin $new } | ForEach-Object { "- $_" }) + @($new | Where-Object { $_ -notin $old } | ForEach-Object { "+ $_" })
                    $lines += '```'
                }
                if ($manual.Count) { $lines += @('', '## 一次性資料修正（manual/）', '', '不在 update.sql 內；由工程師判斷是否、何時執行。', '') + @($manual | ForEach-Object { "- $(Split-Path $_ -Leaf)" }) }
                Save-Text (Join-Path $folder 'changes.md') ($lines -join "`n")
            }
        })
}

$steps.Add(@{ Name = '寫入說明'; Run = {
            $has = @{ app = 'app' -in $components; database = 'database' -in $components }
            $readme = @(
                "# AI Nexus v$version"
                ''
                "- 內容：$(($components | ForEach-Object { $labels[$_] }) -join '、')；commit ``$head``"
                $(if ($has.database) { "- 資料庫更新包的起點：$($start.Label)（目標庫版本頁顯示的資料庫版本必須是它）" })
                $(if ($has.app) { "- 這一版程式需要的資料庫版本：$(if ($databaseVersion) { "v$databaseVersion 以上" } else { '（沒有紀錄）' })" })
                ''
                '## 執行順序'
                ''
                '先停站並完成備份（SQL 與附件同一時點，見 docs/operations/backup.md）。'
                ''
            )
            $number = 0
            if ($has.database) {
                $readme += @(
                    "$((++$number)). 資料庫：在 SSMS 開啟 ``database\update.sql``，切到 SQLCMD 模式，連到目標庫執行。資料庫名稱不是 AiNexus 時改第一個 ``:setvar``。"
                    '   - 版本不符時停止並說明：改用 `Release.ps1 -From v<目標庫版本>` 重新產生。'
                    '   - 有手動異動時列出物件並停止：確認可以覆蓋（或已回收進開發庫）後把 `AcceptDrift` 改成 1 重新執行。'
                    '   - 審核：`changes.md` 列出變更的物件；Visual Studio 開啟 `compare.scmp`（套件搬到別台時重新選擇 `compare\` 下的兩個 dacpac）。'
                    $(if ($manual.Count) { "$((++$number)). 一次性資料修正：``database\manual\`` 的 $($manual.Count) 個腳本，依 changes.md 判斷是否執行。" })
                )
            }
            if ($has.app) {
                $readme += @(
                    "$((++$number)). 程式：以 ``app\`` 替換網站實體路徑，保留執行中的 config、keys 與站外資料（docs/operations/iis-upgrade.md）。$(if ($DataRoot) { '首次部署：把 `config\`、`keys\`、`logs\` 放在 app 旁邊，依 docs/operations/iis-deployment.md 填 config 與設定目錄權限。' } else { '新主機首次部署要另以 `-DataRoot` 產生 config 範本。' })"
                    '   - 網站啟動時檢查資料庫結構；不一致時拒絕啟動並在事件記錄列出差異。'
                )
            }
            $readme += @("$((++$number)). 開啟網站，在「版本」頁確認程式與資料庫的版本，以及沒有未預期的結構異動。")
            if ($has.database) { $readme += @('', '## 回復', '', '程式物件可執行 `database\rollback.sql` 回到起點；資料表與資料一律以更新前的備份回復。') }
            Save-Text (Join-Path $package 'README.md') (($readme | Where-Object { $null -ne $_ }) -join "`n")
        }
    })

try { Invoke-RepoSteps "產生 v$version" $steps }
catch {
    if ($rehearsal) { Write-Warning "演練庫 $rehearsal 保留在 (localdb)\MSSQLLocalDB 供檢查，查完自行刪除。" }
    throw
} finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }

Write-Output "發布包：$package"
if (!$Publish) { return }

$tag = "v$version"
$message = @(
    "AI Nexus $tag"
    ''
    @($notes | Where-Object Type -EQ 'feat' | ForEach-Object { "- feat$(if ($_.Scope) { "($($_.Scope))" }): $($_.Subject)" })
    @($notes | Where-Object Type -EQ 'fix' | ForEach-Object { "- fix$(if ($_.Scope) { "($($_.Scope))" }): $($_.Subject)" })
    @($schemaChanges | ForEach-Object { "- database: $($_.Change.ToLowerInvariant()) $($_.Type) $($_.Name)" })
    ''
    "Components: $($components -join ', ')"
) -join "`n"
$file = Join-Path ([IO.Path]::GetTempPath()) "AiNexus-tag-$tag.txt"
[IO.File]::WriteAllText($file, $message, [Text.UTF8Encoding]::new($false))
try { Invoke-RepoGit tag -a $tag -F $file --cleanup=verbatim HEAD | Out-Null } finally { Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue }
Write-Output "已建立 $tag（$($components -join ', ')）。"

$push = "git push origin refs/tags/$tag"
if ([Console]::IsInputRedirected -or !$PSCmdlet.ShouldContinue("推送 $tag 到 origin？其他人與 hotfix 分支要看到這個發版才算數。", '推送 tag')) {
    Write-Output "尚未推送；確認後執行：$push"
    return
}
Invoke-RepoGit push origin "refs/tags/$tag" | Out-Host
Write-Output "已推送 $tag。"
