#requires -Version 7.4

<#
.SYNOPSIS
開發模式：同時啟動 dotnet watch（API）與 Angular dev server，存檔後自動更新。

.DESCRIPTION
先啟動 API，等它開始監聽再啟動 Angular。Angular 的 /api、/health 代理到後端，同源 cookie 與 CSRF 照常運作。兩個程序的輸出即時寫在 .local/logs/；
主控台加上時間顯示 stderr（啟動例外、dotnet watch 訊息）、編譯錯誤、建置與熱更新、就緒與重啟狀態，等待較久時定期回報。Ctrl+C 會一起停止；任一程序結束時另一個也會停止。

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
    # bufferSize 1＝不緩衝：程序執行中就能從紀錄檔看到輸出
    $outputLog = Join-Path $logs "$Name.log"
    $errorLog = Join-Path $logs "$Name.error.log"
    $out = [IO.FileStream]::new($outputLog, 'Create', 'Write', 'ReadWrite', 1)
    $err = [IO.FileStream]::new($errorLog, 'Create', 'Write', 'ReadWrite', 1)
    @{
        Name = $Name; Process = $process; Out = $out; Err = $err
        OutputReader = [IO.StreamReader]::new([IO.FileStream]::new($outputLog, 'Open', 'Read', 'ReadWrite'))
        ErrorReader = [IO.StreamReader]::new([IO.FileStream]::new($errorLog, 'Open', 'Read', 'ReadWrite'))
        OutputPending = ''; ErrorPending = ''
        CopyOut = $process.StandardOutput.BaseStream.CopyToAsync($out)
        CopyErr = $process.StandardError.BaseStream.CopyToAsync($err)
    }
}

function Read-ChildLines([hashtable]$Child, [string]$Stream) {
    $lines = ($Child["${Stream}Pending"] + $Child["${Stream}Reader"].ReadToEnd()) -split '\r?\n'
    $Child["${Stream}Pending"] = $lines[-1]
    $lines | Select-Object -SkipLast 1 | ForEach-Object { $_ -replace '\x1b\[[0-9;]*m' } | Where-Object { $_.Trim() }
}

function Write-Status([string]$Name, [string]$Message) { "$(Get-Date -Format 'HH:mm:ss') [$Name] $Message" }

# stdout 只挑出狀態變化與錯誤，其餘（chunk 清單、建置細節）留在紀錄檔。
function Format-ChildOutput([string]$Name, [string]$Line) {
    if ($Name -eq 'frontend') {
        switch -Regex ($Line) {
            # 完成時 Angular 會以 ✔ 重印同一行，只取開始的那一次
            '^([❯>]\s*)?Changes detected' { return '偵測到變更，重新建置…' }
            '^([❯>]\s*)?Building\.\.\.$' { return '建置中…' }
            'bundle generation complete\.\s*\[([\d.]+) seconds\]' { return "建置完成（$($Matches[1]) 秒），監看變更中。" }
            'bundle generation failed\.\s*\[([\d.]+) seconds\]' { return "建置失敗（$($Matches[1]) 秒）；修正後存檔會再建置。" }
            'Component update sent' { return '已熱更新元件。' }
            'Stylesheet update sent' { return '已熱更新樣式。' }
            'Page reload sent' { return '已通知瀏覽器重新整理。' }
            '\[(ERROR|WARNING)\]|^\s+\S+:\d+:\d+:\s*$' { return $Line.Trim() }
        }
    } elseif ($Line -match ':\s*(error|warning)\s+[A-Z]+\d+|建置失敗|Build FAILED') {
        return $Line.Trim()
    }
}

# dotnet watch 在程式啟動失敗後會等待存檔而不結束，所以 stderr（啟動例外、dotnet watch 訊息）全部同步轉印到主控台。
function Write-ChildOutput([hashtable]$Child) {
    foreach ($line in @(Read-ChildLines $Child 'Output')) {
        $message = Format-ChildOutput $Child.Name $line
        if ($message) { Write-Status $Child.Name $message }
    }
    foreach ($line in @(Read-ChildLines $Child 'Error')) { Write-Status $Child.Name $line.Trim() }
}

function Test-Listening([int]$Port) {
    [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains $Port
}

$children = [Collections.Generic.List[hashtable]]::new()
try {
    $children.Add((Start-Child 'backend' $dotnet $root @(
        'watch', '--project', 'backend/src/AiNexus.Host/AiNexus.Host.csproj', 'run', '--no-launch-profile', '--',
        '--urls', "${scheme}://localhost:$BackendPort", '--Security:AllowInsecureLocalhost', "$($Http.IsPresent)",
        '--LocalConfigPath', $local.Settings, '--SecretsConfigPath', $local.Secrets)))
    Write-Output "開發模式：${scheme}://localhost:$FrontendPort/chat（Angular + API）。儲存原始碼後自動更新。"
    Write-Output "啟動與錯誤紀錄：$logs。Ctrl+C 同時停止兩個服務。"
    Write-Status 'backend' "編譯並啟動 API（${scheme}://localhost:$BackendPort）…"
    # 等 API 開始監聽才啟動 Angular，避免頁面先載入、代理連不到後端而刷出 ECONNREFUSED。
    # 之後持續追蹤監聽狀態：dotnet watch 遇到無法熱更新的修改會重啟 API，期間代理錯誤屬預期。
    $ports = @{ backend = $BackendPort; frontend = $FrontendPort }
    $ready = @{ backend = $false; frontend = $false }
    $waitingSince = @{ backend = [DateTime]::Now; frontend = $null }
    $reported = @{ backend = 0; frontend = 0 }
    while ($true) {
        foreach ($child in @($children)) {
            $name = $child.Name
            $exited = $child.Process.HasExited
            if ($exited) { [Threading.Tasks.Task]::WaitAll(@($child.CopyOut, $child.CopyErr), 3000) | Out-Null }
            Write-ChildOutput $child
            if ($exited) { throw "$name 已結束（$($child.Process.ExitCode)）；請查看 .local/logs/$name.error.log 與 .log。" }
            $listening = Test-Listening $ports[$name]
            if ($listening -and !$ready[$name]) {
                $elapsed = ([DateTime]::Now - $waitingSince[$name]).TotalSeconds.ToString('0')
                Write-Status $name $(if ($name -eq 'backend') { "API 已就緒（$elapsed 秒）。" }
                    else { "已就緒（$elapsed 秒）：${scheme}://localhost:$FrontendPort/chat" })
                $waitingSince[$name] = $null
            } elseif (!$listening -and $ready[$name]) {
                Write-Status $name $(if ($name -eq 'backend') { 'API 停止監聽，重新啟動中…' } else { 'dev server 停止監聽…' })
                $waitingSince[$name] = [DateTime]::Now; $reported[$name] = 0
            }
            $ready[$name] = $listening
        }
        if ($ready.backend -and $children.Count -eq 1) {
            $children.Add((Start-Child 'frontend' $node (Join-Path $root 'frontend') (@(
                $ng, 'serve', '--host', 'localhost', '--port', "$FrontendPort", '--proxy-config', $proxy) + $sslArguments)))
            $waitingSince.frontend = [DateTime]::Now
            Write-Status 'frontend' "啟動 Angular dev server（${scheme}://localhost:$FrontendPort）…"
        }
        # 啟動或重啟較久時每 15 秒回報一次，主控台才不會看起來像當機
        foreach ($name in @('backend', 'frontend')) {
            if (!$waitingSince[$name]) { continue }
            $elapsed = [int]([DateTime]::Now - $waitingSince[$name]).TotalSeconds
            if ($elapsed -ge ($reported[$name] + 1) * 15) {
                $reported[$name] = [Math]::Floor($elapsed / 15)
                Write-Status $name "仍在啟動（已 $elapsed 秒）…"
            }
        }
        Start-Sleep -Milliseconds 500
    }
} finally {
    foreach ($child in $children) {
        if (!$child.Process.HasExited) { $child.Process.Kill($true) }
        $child.Process.WaitForExit()
        try { [Threading.Tasks.Task]::WaitAll(@($child.CopyOut, $child.CopyErr), 3000) | Out-Null }
        finally {
            $child.Out.Dispose(); $child.Err.Dispose(); $child.OutputReader.Dispose(); $child.ErrorReader.Dispose()
            $child.Process.Dispose()
        }
    }
}
