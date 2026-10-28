#requires -Version 7.4

<#
.SYNOPSIS
產生新的 IIS 發布套件：app、config 範本、keys、logs，以及資料庫的 App.dacpac 與依序執行的手寫 SQL。

.DESCRIPTION
預設先執行 Build.ps1，再把發布產物複製到 artifacts/iis/<時間>/app；web.config 由 SDK 從
backend/src/AiNexus.Host/web.config 帶入，不再改寫。config/ 只在不存在時由範本建立，
並把 Files.StoragePath 與 Diagnostics.Directory 設為 -DataRoot 下的 attachments、diagnostics。
database/ 帶 App.dacpac（由 database/App 建置，這一版預期的資料庫結構）與 release/：依執行順序編號的手寫 SQL
（1 pre、3 全文、4 post、5 種子資料）。步驟 2 的結構升級 SQL 由 Visual Studio 結構描述比對 App.dacpac 與正式庫產生；
完整步驟見 database/release/README.txt 與 docs/operations/database-setup.md。
套件不含秘密，正式主機不需要 PowerShell 7 或 SqlPackage；只有 app/ 是 IIS 的實體路徑。

.PARAMETER DataRoot
正式主機上站外資料的根目錄（絕對路徑），例如 D:\AiNexus\data。

.PARAMETER DestinationPath
新套件的 app 目錄，必須是空的；預設 artifacts/iis/<UTC 時間>/app。不要指向執行中的 IIS 目錄。

.PARAMETER Release
這次發版的版本，對應 database/App/Data/<版本>/ 的一次性資料修正（pre-*.sql、post-*.sql）；省略時不帶。

.PARAMETER IncludeLocalConfig
以這台機器 .local/ 的設定與秘密取代範本（只用於自己的測試主機）。

.PARAMETER AllowUntrustedSql
設定 Databases.App.TrustServerCertificate=true，用於自簽憑證的 SQL Server。

.PARAMETER SkipBuild
使用 PublishDirectory 既有的發布產物。

.PARAMETER PublishDirectory
發布產物目錄，必須在 artifacts/ 內。

.COMPONENT
發布

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data'

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data' -SkipBuild -PublishDirectory artifacts/verification

.EXAMPLE
./scripts/Publish-IIS.ps1 -DataRoot 'D:\AiNexus\data' -Release 2026.11.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [string]$DestinationPath,
    [ValidatePattern('^[\w.-]+$')][string]$Release,
    [switch]$IncludeLocalConfig,
    [switch]$AllowUntrustedSql,
    [switch]$SkipBuild,
    [string]$PublishDirectory = 'artifacts/publish'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

# Windows paths are checked on any OS, because the package is built for a Windows IIS host.
if ($DataRoot -notmatch '^[A-Za-z]:\\') { throw 'DataRoot 必須是正式主機上的絕對路徑，例如 D:\AiNexus\data。' }
$root = Get-RepoRoot
$publish = Resolve-RepoArtifactPath $PublishDirectory
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $publish -Runtime win-x64 }
$deps = Join-Path $publish 'AiNexus.Host.deps.json'
if (!(Test-Path -LiteralPath $deps)) { throw '找不到發布產物；請先執行 scripts/Build.ps1。' }
# 其他平台的產物缺 Windows 版的 SqlClient 與 AD 元件，在 IIS 上啟動才會失敗，所以在打包時就擋下。
if ((Get-Content -LiteralPath $deps -Raw | ConvertFrom-Json).runtimeTarget.name -notlike '*/win-x64') { throw '發布產物不是 win-x64；請以 scripts/Build.ps1 -Runtime win-x64 重新建置。' }
if (!$DestinationPath) { $DestinationPath = Join-Path $root ('artifacts/iis/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '/app') }
$app = [IO.Path]::GetFullPath($DestinationPath)
if ((Test-Path -LiteralPath $app) -and @(Get-ChildItem -LiteralPath $app -Force).Count) {
    throw 'DestinationPath 必須是新套件的空 app 目錄；替換執行中的網站請依 docs/operations/iis-deployment.md。'
}
$package = Split-Path $app
foreach ($directory in @($app, (Join-Path $package 'config'), (Join-Path $package 'keys'), (Join-Path $package 'logs'))) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
Get-ChildItem -LiteralPath $publish | Where-Object { $_.Name -notin @('logs', 'App_Data') } | Copy-Item -Destination $app -Recurse

# SDK 對 RID 發布會把 processPath 改成 apphost 卻不動 arguments；兩者不一致時 exe 會收到 dll 參數，IIS 啟動失敗。
$aspNetCore = ([xml](Get-Content -LiteralPath (Join-Path $app 'web.config') -Raw)).SelectSingleNode('//aspNetCore')
if (!$aspNetCore -or $aspNetCore.processPath -ne '.\AiNexus.Host.exe' -or $aspNetCore.arguments) {
    throw "web.config 的 aspNetCore 必須是 processPath='.\AiNexus.Host.exe'、arguments 為空；實際為 processPath='$($aspNetCore.processPath)'、arguments='$($aspNetCore.arguments)'。請修 backend/src/AiNexus.Host/web.config。"
}

$settingsPath = Join-Path $package 'config/appsettings.Production.json'
$secretsPath = Join-Path $package 'config/appsettings.Secrets.json'
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
$settings = $null

# 與 app/ 同一份原始碼建置，結構比對的來源不會和程式脫節。
$project = Join-Path $root 'database/App'
$data = Join-Path $project 'Data'
$versions = @(if (Test-Path -LiteralPath $data -PathType Container) { Get-ChildItem -LiteralPath $data -Directory | ForEach-Object Name })
if ($Release -and $Release -notin $versions) { Write-Output "database/App/Data/$Release 不存在：這一版沒有一次性資料修正。" }
elseif (!$Release -and $versions.Count) { Write-Warning "未指定 -Release，不帶 Data/ 的一次性資料修正（有：$($versions -join '、')）。" }
$scripts = @(Get-RepoReleaseScript $project -Release $Release)
Push-Location -LiteralPath $root
try {
    dotnet build database/App --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗；請先執行 scripts/Restore.ps1。' }
} finally { Pop-Location }
$database = Join-Path $package 'database'
$releaseFolder = Join-Path $database 'release'
New-Item -ItemType Directory -Path $releaseFolder -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'bin/Release/App.dacpac') -Destination $database
# SSMS 開檔要有 BOM 才不會把中文註解與 N'' 字串當成系統字碼頁。
$encoding = [Text.UTF8Encoding]::new($true)
foreach ($script in $scripts) { [IO.File]::WriteAllText((Join-Path $releaseFolder $script.Name), $script.Text.Replace("`r`n", "`n").Replace("`n", "`r`n"), $encoding) }
$list = @(foreach ($step in 1, 3, 4, 5) {
    $names = @($scripts | Where-Object { $_.Name.StartsWith("$step-") } | ForEach-Object Name)
    $title = @{ 1 = '一次性資料修正（結構前）'; 3 = '全文目錄與索引（可重跑）'; 4 = '一次性資料修正（結構後）'; 5 = '種子資料（可重跑）' }[$step]
    "$step. $title"
    if ($names.Count) { $names | ForEach-Object { "   $_" } } else { '   （這一版沒有，略過）' }
    if ($step -eq 1) {
        '2. 結構：Visual Studio 結構描述比對，來源 database\App.dacpac、目標正式庫；選項見 docs/operations/database-setup.md'
        '   審核差異後「產生指令碼」，在 SSMS 執行（產生的指令碼要開 SQLCMD 模式）'
    }
})
$readme = @(
    "AiNexus 資料庫發版步驟（版本：$(if ($Release) { $Release } else { '未指定' })，產生於 $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm')) UTC）"
    ''
    '先停站並備份。以下依序在 SSMS 連到正式庫執行（release\ 的檔案用一般模式即可，T-SQL 與 GO）：'
    ''
    $list
    ''
    '6. 再做一次結構描述比對，應該沒有差異（全文、權限、使用者不在比對範圍）'
    '7. 啟動網站；結構和程式不一致時網站會拒絕啟動，並在事件記錄列出缺少的表或欄位'
)
[IO.File]::WriteAllText((Join-Path $releaseFolder 'README.txt'), ($readme -join "`r`n") + "`r`n", $encoding)
Write-Output "IIS 發布套件：$package"
Write-Output "資料庫：依 $(Join-Path $releaseFolder 'README.txt') 的順序執行；結構以結構描述比對 database\App.dacpac 與正式庫。"
Write-Output "只有 app/ 是 IIS 實體路徑。更新時保留執行中的 config/、keys/ 與 $DataRoot；首次啟動前建立站外資料目錄並給 application pool Modify 權限，見 docs/operations/iis-deployment.md。套件的 logs/ 只給 ANCM stdout 使用。"
