#requires -Version 7.4

<#
.SYNOPSIS
開發模式：同時啟動 dotnet watch（API）與 Angular dev server，存檔後自動更新。

.DESCRIPTION
Angular 的 /api、/health 代理到後端，同源 cookie 與 CSRF 照常運作。兩個程序的輸出寫在 .local/logs/，
Ctrl+C 會一起停止；任一程序結束時另一個也會停止。

.PARAMETER Restore
先執行 Restore.ps1。

.PARAMETER Http
改用 HTTP；只在 localhost 有效。

.PARAMETER BackendPort
API 埠號。

.PARAMETER FrontendPort
Angular dev server 埠號，開這個網址。

.EXAMPLE
./scripts/Start-Dev.ps1

.EXAMPLE
./scripts/Start-Dev.ps1 -BackendPort 5081 -FrontendPort 4201
#>
[CmdletBinding()]
param(
    [switch]$Restore,
    [switch]$Http,
    [ValidateRange(1024, 65535)][int]$BackendPort = 5080,
    [ValidateRange(1024, 65535)][int]$FrontendPort = 4200
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
if ($BackendPort -eq $FrontendPort) { throw 'FrontendPort 與 BackendPort 不可相同。' }
if (!$Http) { Assert-NexusHttpsCertificate }
$local = Initialize-NexusLocalSettings
if ($Restore) { & (Join-Path $PSScriptRoot 'Restore.ps1') }
$listeners = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
foreach ($port in @($BackendPort, $FrontendPort)) {
    if ($listeners.Port -contains $port) { throw "埠號 $port 已被使用；請先停止既有的預覽或改用其他埠號。" }
}
$node = (Get-Command node -CommandType Application | Select-Object -First 1).Source
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$ng = Join-Path $root 'frontend/node_modules/@angular/cli/bin/ng.js'
if (!(Test-Path -LiteralPath $ng)) { throw '找不到 Angular CLI；請先執行 scripts/Restore.ps1。' }

$scheme = if ($Http) { 'http' } else { 'https' }
$proxy = Join-Path $root '.local/config/dev-proxy.json'
$backend = @{ target = "${scheme}://localhost:$BackendPort"; secure = $true; changeOrigin = $false }
Save-NexusJson $proxy @{ '/api' = $backend; '/health' = $backend }
$certificate = $null
$sslArguments = @()
if (!$Http) {
    $certificates = Join-Path $root '.local/certs'
    New-Item -ItemType Directory -Path $certificates -Force | Out-Null
    $certificate = Join-Path $certificates 'nexus-dev.pem'
    dotnet dev-certs https --export-path $certificate --format PEM --no-password --quiet
    if ($LASTEXITCODE -ne 0) { throw '匯出本機 HTTPS 憑證失敗。' }
    $sslArguments = @('--ssl', '--ssl-cert', $certificate, '--ssl-key', (Join-Path $certificates 'nexus-dev.key'))
}
$logs = Join-Path $root '.local/logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Start-Child([string]$Name, [string]$Executable, [string]$Directory, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $info.Environment['NG_CLI_ANALYTICS'] = 'false'
    $info.Environment['DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER'] = '1'
    $info.Environment['DOTNET_WATCH_SUPPRESS_EMOJIS'] = '1'
    if ($certificate) { $info.Environment['NODE_EXTRA_CA_CERTS'] = $certificate }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    if (!$process.Start()) { throw "無法啟動 $Name。" }
    $out = [IO.File]::Open((Join-Path $logs "$Name.log"), 'Create', 'Write', 'ReadWrite')
    $err = [IO.File]::Open((Join-Path $logs "$Name.error.log"), 'Create', 'Write', 'ReadWrite')
    @{
        Name = $Name; Process = $process; Out = $out; Err = $err
        CopyOut = $process.StandardOutput.BaseStream.CopyToAsync($out)
        CopyErr = $process.StandardError.BaseStream.CopyToAsync($err)
    }
}

$children = [Collections.Generic.List[hashtable]]::new()
try {
    $children.Add((Start-Child 'backend' $dotnet $root @(
        'watch', '--project', 'backend/src/AiNexus.Host/AiNexus.Host.csproj', 'run', '--no-launch-profile', '--',
        '--urls', "${scheme}://localhost:$BackendPort", '--Security:AllowInsecureLocalhost', "$($Http.IsPresent)",
        '--LocalConfigPath', $local.Settings, '--SecretsConfigPath', $local.Secrets)))
    $children.Add((Start-Child 'frontend' $node (Join-Path $root 'frontend') (@(
        $ng, 'serve', '--host', 'localhost', '--port', "$FrontendPort", '--proxy-config', $proxy) + $sslArguments)))
    Write-Output "開發模式：${scheme}://localhost:$FrontendPort/chat（Angular + API）。儲存原始碼後自動更新。"
    Write-Output "啟動與錯誤紀錄：$logs。Ctrl+C 同時停止兩個服務。"
    while ($true) {
        foreach ($child in $children) {
            if ($child.Process.HasExited) { throw "$($child.Name) 已結束（$($child.Process.ExitCode)）；請查看 .local/logs/$($child.Name).error.log 與 .log。" }
        }
        Start-Sleep -Milliseconds 500
    }
} finally {
    foreach ($child in $children) {
        if (!$child.Process.HasExited) { $child.Process.Kill($true) }
        $child.Process.WaitForExit()
        try { [Threading.Tasks.Task]::WaitAll(@($child.CopyOut, $child.CopyErr), 3000) | Out-Null }
        finally { $child.Out.Dispose(); $child.Err.Dispose(); $child.Process.Dispose() }
    }
}
