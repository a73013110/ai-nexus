#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的連線設定建立一個新的 AiNexus 資料庫：結構、全文物件與種子資料，並記錄版本與結構快照。

.DESCRIPTION
先 build database/App，以 SqlPackage 套用 App.dacpac，再依序執行 update.sql 同一份手寫 SQL（全文物件、種子資料），
最後寫入資料庫版本與這一版應有的結構快照（releases schema，ADR 0028），網站的版本頁與日後的 update.sql 以它為準。
只建立新庫：資料庫已存在時停止，不會改動 DBA 維護的開發庫；既有庫的升級見 docs/operations/database-setup.md。
需要有建庫權限的 SQL 登入。

.PARAMETER Server
建在另一台 SQL Server（例如 (localdb)\MSSQLLocalDB），以目前 Windows 身分連線；省略時用設定的 Databases.App 與秘密檔的帳密。

.PARAMETER Database
要建立的資料庫，省略時用設定的 Databases.App.Name；例如另建一個沙盒庫。指定 -Version 時預設為 <名稱>_v<版號>。

.PARAMETER Version
以某個發版 tag（例如 v2026.11.3）的結構建庫，用來在本機重現客戶的資料庫；以 git worktree 取出建置，不動工作目錄。
省略時用工作目錄的 database/App，記錄的版本由最近的 tag 推得（例如 2026.11.4-dev.3）。

.COMPONENT
資料庫與合約

.EXAMPLE
./scripts/Initialize-Database.ps1

.EXAMPLE
./scripts/Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_Sandbox

.EXAMPLE
./scripts/Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Version v2026.11.3
#>
[CmdletBinding()]
param([string]$Server, [string]$Database, [string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$paths = Initialize-RepoLocalSettings
$defaults = Read-RepoJson (Join-Path $root 'backend/src/AiNexus.Host/appsettings.json')
$local = Read-RepoJson $paths.Settings
$secrets = Read-RepoJson $paths.Secrets
function Get-DatabaseSetting([string]$Key) {
    foreach ($settings in $secrets, $local, $defaults) {
        $value = Get-RepoSetting $settings "Databases.App.$Key"
        if ($null -ne $value -and "$value" -ne '') { return $value }
    }
}
$tag = $null
if ($Version) {
    $tag = @(Get-RepoReleaseTag) | Where-Object { $_.Tag -eq $Version -or $_.Version -eq $Version } | Select-Object -First 1
    if (!$tag) { throw "找不到發版 tag $Version（git tag -l 'v*'）。" }
}
if (!$Database) { $Database = Get-DatabaseSetting 'Name'; if ($tag -and $Database) { $Database += '_v' + $tag.Version.Replace('.', '_') } }
$connection = if ($Server) { @{ Server = $Server; Database = $Database } } else {
    @{ Server = (Get-DatabaseSetting 'Server'); Database = $Database; TrustServerCertificate = [bool](Get-DatabaseSetting 'TrustServerCertificate') }
}
if (!$connection.Server -or !$Database) { throw '請先以 scripts/Configure-Local.ps1 設定 SQL server 與資料庫名稱。' }
$user = Get-DatabaseSetting 'User'
if (!$Server -and !(Get-DatabaseSetting 'IntegratedSecurity') -and $user) {
    $connection.Credential = [pscredential]::new($user, (ConvertTo-SecureString (Get-DatabaseSetting 'Password') -AsPlainText -Force))
}
$secrets = $null

$build = $null
$recorded = if ($tag) { [pscustomobject]@{ Version = $tag.Version; Revision = $tag.Commit } } else { Get-RepoVersion }
Invoke-RepoSteps "建立資料庫 $Database" @(
    @{ Name = '確認資料庫不存在'; Run = {
            if (Test-RepoDatabaseExists @connection) {
                throw "$($connection.Server) 上已有 $Database，這個腳本只建立新庫。既有庫的升級見 docs/operations/database-setup.md。"
            }
        }
    }
    @{ Name = '建置資料庫專案'; Run = { $script:build = Get-RepoDatabaseBuild -Ref $(if ($tag) { $tag.Commit }) } }
    @{ Name = '套用 App.dacpac'; Run = {
            # 新庫才套用 App.sqlproj 的 collation、相容層級與 RCSI。
            Publish-RepoDacpac -Dacpac $build.Dacpac @connection
        }
    }
    @{ Name = '全文物件、種子資料與版本紀錄'; Run = {
            $sql = New-RepoSqlConnection @connection
            $sql.add_InfoMessage({ param($source, $info) Write-Output "  $($info.Message)" | Out-Host })
            try {
                $sql.Open()
                foreach ($script in Get-RepoReleaseScript $build.Path) {
                    Write-Output "執行 $($script.Name)"
                    Invoke-RepoSqlScript $sql $script.Text $script.Source
                }
                # 新庫就是這一版應有的結構，快照直接取自它；早於 releases schema 的 tag 沒有這些表，略過。
                $record = $sql.CreateCommand()
                $record.CommandText = @'
IF OBJECT_ID(N'[releases].[Deployments]', N'U') IS NOT NULL
BEGIN
    INSERT INTO [releases].[SchemaObjects] ([SchemaName], [Name], [Type], [Hash], [Definition])
    SELECT [SchemaName], [Name], [Type], [Hash], [Definition] FROM [releases].[udf_SchemaObjects]();
    INSERT INTO [releases].[IgnoredObjects] ([Pattern]) SELECT [value] FROM OPENJSON(@Ignored);
    INSERT INTO [releases].[Deployments] ([Component], [Version], [Revision], [DeployedAt], [Host])
    VALUES (N'database', @Version, @Revision, SYSDATETIMEOFFSET(), LEFT(ISNULL(HOST_NAME(), N''), 128));
END
'@
                $ignored = @(Read-RepoExclusionList (Join-Path $build.Path 'export-exclusions.txt') | Select-Object -Unique | ForEach-Object { ConvertTo-RepoLikePattern $_ })
                [void]$record.Parameters.AddWithValue('@Ignored', (ConvertTo-Json -InputObject $ignored -Compress))
                [void]$record.Parameters.AddWithValue('@Version', $recorded.Version)
                [void]$record.Parameters.AddWithValue('@Revision', $recorded.Revision)
                [void]$record.ExecuteNonQuery()
                Write-Output "記錄資料庫版本 v$($recorded.Version)"
            } finally { $sql.Dispose() }
        }
    }
)
