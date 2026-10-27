#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的連線設定建立一個新的 AiNexus 資料庫：結構、全文物件與種子資料。

.DESCRIPTION
先 build database/App，以 SqlPackage 套用 App.dacpac，再依序執行發布套件同一份手寫 SQL（全文物件、種子資料）。
只建立新庫：資料庫已存在時停止，不會改動 DBA 維護的開發庫；既有庫的升級見 docs/operations/database-setup.md。
需要有建庫權限的 SQL 登入。

.PARAMETER Server
建在另一台 SQL Server（例如 (localdb)\MSSQLLocalDB），以目前 Windows 身分連線；省略時用設定的 Databases.App 與秘密檔的帳密。

.PARAMETER Database
要建立的資料庫，省略時用設定的 Databases.App.Name；例如另建一個沙盒庫。

.COMPONENT
資料庫與合約

.EXAMPLE
./scripts/Initialize-Database.ps1

.EXAMPLE
./scripts/Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database AiNexus_Sandbox
#>
[CmdletBinding()]
param([string]$Server, [string]$Database)
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
if (!$Database) { $Database = Get-DatabaseSetting 'Name' }
$connection = if ($Server) { @{ Server = $Server; Database = $Database } } else {
    @{ Server = (Get-DatabaseSetting 'Server'); Database = $Database; TrustServerCertificate = [bool](Get-DatabaseSetting 'TrustServerCertificate') }
}
if (!$connection.Server -or !$Database) { throw '請先以 scripts/Configure-Local.ps1 設定 SQL server 與資料庫名稱。' }
$user = Get-DatabaseSetting 'User'
if (!$Server -and !(Get-DatabaseSetting 'IntegratedSecurity') -and $user) {
    $connection.Credential = [pscredential]::new($user, (ConvertTo-SecureString (Get-DatabaseSetting 'Password') -AsPlainText -Force))
}
$secrets = $null

$project = Join-Path $root 'database/App'
Invoke-RepoSteps "建立資料庫 $Database" @(
    @{ Name = '確認資料庫不存在'; Run = {
            if (Test-RepoDatabaseExists @connection) {
                throw "$($connection.Server) 上已有 $Database，這個腳本只建立新庫。既有庫的升級見 docs/operations/database-setup.md。"
            }
        }
    }
    @{ Name = '建置資料庫專案'; Run = {
            Push-Location -LiteralPath $root
            try {
                dotnet build database/App -c Release --nologo -v q
                if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗；請先執行 scripts/Restore.ps1。' }
            } finally { Pop-Location }
        }
    }
    @{ Name = '套用 App.dacpac'; Run = {
            # 新庫才套用 App.sqlproj 的 collation、相容層級與 RCSI；權限、登入與檔案位置是環境差異，全文物件不在 dacpac。
            $excluded = 'Users', 'Logins', 'DatabaseRoles', 'ApplicationRoles', 'RoleMembership', 'Permissions', 'Credentials',
                'DatabaseScopedCredentials', 'ServerRoles', 'ServerRoleMembership', 'ServerTriggers', 'LinkedServers', 'LinkedServerLogins',
                'Endpoints', 'ErrorMessages', 'Audits', 'ServerAuditSpecifications', 'DatabaseAuditSpecifications', 'EventSessions',
                'Certificates', 'MasterKeys', 'DatabaseEncryptionKeys', 'CryptographicProviders', 'Filegroups', 'Files', 'FullTextCatalogs', 'FullTextStoplists'
            $properties = @(
                'BlockOnPossibleDataLoss=True', 'GenerateSmartDefaults=False', "ExcludeObjectTypes=$($excluded -join ';')", 'IgnorePermissions=True',
                'IgnoreRoleMembership=True', 'IgnoreLoginSids=True', 'IgnoreAuthorizer=True', 'IgnoreUserSettingsObjects=True',
                'ScriptDatabaseOptions=True', 'ScriptDatabaseCollation=True', 'ScriptDatabaseCompatibility=True'
            ) | ForEach-Object { "/p:$_" }
            $target = [Data.Common.DbConnectionStringBuilder]::new()
            $target['Server'] = $connection.Server
            $target['Database'] = $Database
            $target['Encrypt'] = 'True'
            $target['TrustServerCertificate'] = [string][bool]$connection['TrustServerCertificate']
            $target['Persist Security Info'] = 'False'
            if ($connection['Credential']) {
                $target['User ID'] = $connection.Credential.UserName
                $target['Password'] = $connection.Credential.GetNetworkCredential().Password
            } else { $target['Integrated Security'] = 'True' }
            # 密碼只寫進目前使用者的暫存資料夾，結束前刪除。SqlPackage 在 response file 中把 \" 當引號，結尾不可是反斜線。
            $work = Join-Path ([IO.Path]::GetTempPath()) ('AiNexus-initialize-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $work | Out-Null
            Push-Location -LiteralPath $root
            try {
                $value = $target.ConnectionString
                if ($value.EndsWith('\')) { $value += ';' }
                $response = Join-Path $work 'connection.rsp'
                [IO.File]::WriteAllText($response, "/TargetConnectionString:`"$($value.Replace('"', '\"'))`"")
                $target = $null
                # SqlPackage 逐一列出每個建立的物件；成功時只在 -Verbose 顯示，失敗時全部印出，原因通常在最後幾行。
                $output = dotnet tool run sqlpackage -- /Action:Publish "/SourceFile:$(Join-Path $project 'bin/Release/App.dacpac')" "@$response" @properties 2>&1
                if ($LASTEXITCODE -ne 0) {
                    $output | Out-Host
                    throw "SqlPackage 失敗（結束碼 $LASTEXITCODE）；原因見上方輸出。找不到工具時先執行 scripts/Restore.ps1。"
                }
                $output | ForEach-Object { Write-Verbose "$_" }
            } finally {
                Pop-Location
                Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    @{ Name = '全文物件與種子資料'; Run = {
            $sql = New-RepoSqlConnection @connection
            $sql.add_InfoMessage({ param($source, $info) Write-Output "  $($info.Message)" | Out-Host })
            try {
                $sql.Open()
                foreach ($script in Get-RepoReleaseScript $project) {
                    Write-Output "執行 $($script.Name)"
                    Invoke-RepoSqlScript $sql $script.Text $script.Source
                }
            } finally { $sql.Dispose() }
        }
    }
)
