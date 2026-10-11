#requires -Version 7.4

<#
.SYNOPSIS
在 IIS 主機上靜態檢查已部署的網站：必要檔案、web.config、外部設定、金鑰與站外目錄、集區與 HTTPS。

.DESCRIPTION
隨發布套件複製到主機上執行，所以不依賴 repo 的其他檔案。不輸出秘密；讀得到檔案不代表集區身分可讀。
SQL、設定與站外目錄寫入另用主機指令 verify deployment 檢查，見 docs/operations/iis-verification.md。

.PARAMETER AppPath
IIS 網站的實體路徑（app 目錄）。

.PARAMETER AppPool
application pool 名稱；指定時以 appcmd 檢查集區設定（需要系統管理員權限）。

.PARAMETER BaseUrl
網站的 HTTPS 網址；指定時檢查 /health/live 與登入設定。

.EXAMPLE
./Verify-IIS.ps1 -AppPath 'D:\AiNexus\app' -AppPool AiNexus -BaseUrl 'https://ai-nexus.example.edu'
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath, [string]$AppPool, [string]$BaseUrl)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$app = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($AppPath))
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) { $checks.Add([pscustomobject]@{ Check = $Name; Passed = $Passed; Detail = $Detail }) }
function Test-Outside([string]$Path) { !$Path.Equals($app, [StringComparison]::OrdinalIgnoreCase) -and !$Path.StartsWith($app + '\', [StringComparison]::OrdinalIgnoreCase) }

foreach ($file in @('AiNexus.Host.dll', 'AiNexus.Host.runtimeconfig.json', 'appsettings.json', 'web.config', 'wwwroot/index.html')) {
    Add-Check $file (Test-Path -LiteralPath (Join-Path $app $file) -PathType Leaf) '發布產物必要檔案'
}
if (!(Test-Path -LiteralPath (Join-Path $app 'web.config'))) { $checks | Format-Table -AutoSize; exit 1 }
[xml]$xml = [IO.File]::ReadAllText((Join-Path $app 'web.config'))
$ancm = $xml.SelectSingleNode('//aspNetCore')
$environment = @{}
foreach ($node in $ancm.SelectNodes('environmentVariables/environmentVariable')) { $environment[$node.name] = $node.value }
Add-Check 'Production' ($environment['ASPNETCORE_ENVIRONMENT'] -eq 'Production') '自簽 SQL 憑證也使用 Production'
Add-Check 'In-process' ($ancm.hostingModel -eq 'inprocess') '一個專用 application pool'
Add-Check 'Stdout off' ($ancm.stdoutLogEnabled -eq 'false') '只在排查啟動問題時暫時開啟'

foreach ($pair in @(@('LocalConfigPath', '..\config\appsettings.Production.json'), @('SecretsConfigPath', '..\config\appsettings.Secrets.json'))) {
    $relative = if ($environment[$pair[0]]) { $environment[$pair[0]] } else { $pair[1] }
    $file = [IO.Path]::GetFullPath($relative, $app)
    Add-Check $pair[0] (Test-Path -LiteralPath $file -PathType Leaf) $file
    if ($pair[0] -ne 'LocalConfigPath' -or !(Test-Path -LiteralPath $file)) { continue }

    $settings = [IO.File]::ReadAllText($file) | ConvertFrom-Json -AsHashtable
    $section = { param([string]$Name) if ($settings.Contains($Name) -and $settings[$Name] -is [Collections.IDictionary]) { $settings[$Name] } else { @{} } }
    $hosts = [string]$settings['AllowedHosts']
    Add-Check 'AllowedHosts configured' ($hosts -and $hosts -notmatch 'company\.internal') '填實際的 IIS DNS 主機名稱（不含 scheme 與 port）'

    $attachments = if ($environment['Attachments__StoragePath']) { $environment['Attachments__StoragePath'] } else { [string](& $section 'Attachments')['StoragePath'] }
    $absolute = ![string]::IsNullOrWhiteSpace($attachments) -and [IO.Path]::IsPathFullyQualified($attachments)
    Add-Check 'Attachment path absolute' $absolute 'web.config 的 Attachments__StoragePath 優先於外部 JSON'
    if ($absolute) {
        $path = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($attachments))
        Add-Check 'Attachments outside app' (Test-Outside $path) $path
        Add-Check 'Attachment directory exists' (Test-Path -LiteralPath $path -PathType Container) 'verify deployment 另以呼叫者身分測試讀寫刪；IIS ACL 需另外驗證'
    }

    $diagnostics = if ($environment['Diagnostics__Directory']) { $environment['Diagnostics__Directory'] }
        elseif ((& $section 'Diagnostics')['Directory']) { (& $section 'Diagnostics')['Directory'] }
        else { Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'AiNexus/diagnostics' }
    $absolute = [IO.Path]::IsPathFullyQualified([string]$diagnostics) -and !([string]$diagnostics).StartsWith('\\')
    Add-Check 'Diagnostics path host-local absolute' $absolute 'web.config 的 Diagnostics__Directory 優先於外部 JSON；空值使用 ProgramData'
    if ($absolute) {
        $path = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($diagnostics))
        Add-Check 'Diagnostics outside app' (Test-Outside $path) $path
        Add-Check 'Diagnostics directory exists' (Test-Path -LiteralPath $path -PathType Container) 'application pool 的 Modify 權限與磁碟容量需另外驗證'
        $physical = $true
        for ($directory = [IO.DirectoryInfo]::new($path); $null -ne $directory; $directory = $directory.Parent) {
            if ($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $physical = $false; break }
        }
        Add-Check 'Diagnostics physical path' $physical '日誌路徑不可經過 junction 或 symlink'
    }
    $settings = $null
}

$keys = [IO.Path]::GetFullPath($(if ($environment['DataProtection__KeyRingPath']) { $environment['DataProtection__KeyRingPath'] } else { '..\keys' }), $app)
Add-Check 'Persistent keys' (Test-Path -LiteralPath $keys -PathType Container) $keys
Add-Check 'Config outside wwwroot' (!(Test-Path -LiteralPath (Join-Path $app 'wwwroot/appsettings.Secrets.json'))) '設定與金鑰都不可經由網站存取'

if ($AppPool) {
    $appcmd = Join-Path $env:windir 'System32/inetsrv/appcmd.exe'
    foreach ($pair in @(@('managedRuntimeVersion', ''), @('enable32BitAppOnWin64', 'false'), @('processModel.loadUserProfile', 'true'), @('processModel.maxProcesses', '1'), @('processModel.idleTimeout', '00:00:00'), @('startMode', 'AlwaysRunning'), @('recycling.disallowOverlappingRotation', 'true'))) {
        $result = & $appcmd list apppool $AppPool ("/text:" + $pair[0])
        Add-Check $pair[0] ($LASTEXITCODE -eq 0 -and ([string]$result).Trim() -eq $pair[1]) ('預期：' + $pair[1])
    }
}
if ($BaseUrl) {
    if (![Uri]::IsWellFormedUriString($BaseUrl, [UriKind]::Absolute) -or ([Uri]$BaseUrl).Scheme -ne 'https') { throw 'BaseUrl 必須是 HTTPS 網址。' }
    try {
        $response = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/health/live') -TimeoutSec 15
        Add-Check 'HTTPS live' ($response.StatusCode -eq 200) '存活不代表 SQL 或 AD 已就緒'
        $session = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/v1/auth/session') -TimeoutSec 15
        Add-Check 'AD session configured' ([bool]$session.configured) '端點可存取；實際登入仍需在瀏覽器驗收'
    } catch { Add-Check 'HTTPS access' $false '檢查 DNS、HTTPS 繫結、網站是否啟動與 Windows 事件記錄' }
}
$checks | Format-Table -AutoSize
if (@($checks | Where-Object { !$_.Passed }).Count) { exit 1 }
Write-Output '靜態檢查通過。SQL 就緒、application pool ACL、實際 AD 登入與串流仍需依 docs/operations/iis-verification.md 驗證。'
