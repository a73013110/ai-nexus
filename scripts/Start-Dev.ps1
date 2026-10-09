#requires -Version 7.4
param([switch]$Restore, [switch]$Http, [ValidateRange(1024,65535)][int]$BackendPort = 5080, [ValidateRange(1024,65535)][int]$FrontendPort = 4200)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
. (Join-Path $PSScriptRoot 'Local-Https.ps1')
if (!$Http) { Assert-NexusHttpsCertificate }
Initialize-NexusLocalSettings
$taskPaths = Get-NexusLocalPaths
if ($Restore) { & (Join-Path $PSScriptRoot 'Restore.ps1') }
if ($BackendPort -eq $FrontendPort) { throw 'FrontendPort and BackendPort must differ.' }
$taskListeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
foreach ($taskPort in @($BackendPort, $FrontendPort)) {
    if ($taskListeners.Port -contains $taskPort) { throw "Port $taskPort is in use. Stop the existing preview or choose another port." }
}
$taskNode = (Get-Command node -CommandType Application | Select-Object -First 1).Source
$taskDotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$taskNg = Join-Path $taskRoot 'frontend/node_modules/@angular/cli/bin/ng.js'
if (!(Test-Path -LiteralPath $taskNg)) { throw 'Run scripts/Restore.ps1 first.' }
$taskProxy = Join-Path $taskRoot '.local/config/dev-proxy.json'
$taskScheme = if ($Http) { 'http' } else { 'https' }
Save-NexusJson $taskProxy @{ '/api' = @{ target = "${taskScheme}://localhost:$BackendPort"; secure = $true; changeOrigin = $false }; '/health' = @{ target = "${taskScheme}://localhost:$BackendPort"; secure = $true; changeOrigin = $false } }
$taskSslArguments = @()
if (!$Http) {
    $taskCertDirectory = Join-Path $taskRoot '.local/certs'
    New-Item -ItemType Directory -Path $taskCertDirectory -Force | Out-Null
    $taskCert = Join-Path $taskCertDirectory 'nexus-dev.pem'
    dotnet dev-certs https --export-path $taskCert --format PEM --no-password --quiet
    if ($LASTEXITCODE -ne 0) { throw '匯出本機 HTTPS 憑證失敗。' }
    $taskSslArguments = @('--ssl', '--ssl-cert', $taskCert, '--ssl-key', (Join-Path $taskCertDirectory 'nexus-dev.key'))
}
$taskLogs = Join-Path $taskRoot '.local/logs'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
$taskChildren = [System.Collections.Generic.List[object]]::new()

function Start-NexusDevChild([string]$Name, [string]$Executable, [string]$Directory, [string[]]$Arguments) {
    $taskInfo = [System.Diagnostics.ProcessStartInfo]::new($Executable)
    $taskInfo.WorkingDirectory = $Directory
    $taskInfo.UseShellExecute = $false
    $taskInfo.CreateNoWindow = $true
    $taskInfo.RedirectStandardOutput = $true
    $taskInfo.RedirectStandardError = $true
    $taskInfo.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $taskInfo.Environment['NG_CLI_ANALYTICS'] = 'false'
    $taskInfo.Environment['DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER'] = '1'
    $taskInfo.Environment['DOTNET_WATCH_SUPPRESS_EMOJIS'] = '1'
    if (!$Http) { $taskInfo.Environment['NODE_EXTRA_CA_CERTS'] = $taskCert }
    foreach ($taskArgument in $Arguments) { $taskInfo.ArgumentList.Add($taskArgument) }
    $taskChild = [System.Diagnostics.Process]::new()
    $taskChild.StartInfo = $taskInfo
    if (!$taskChild.Start()) { throw "Cannot start $Name." }
    $taskOut = [System.IO.File]::Open((Join-Path $taskLogs "$Name.log"), 'Create', 'Write', 'ReadWrite')
    $taskErr = [System.IO.File]::Open((Join-Path $taskLogs "$Name.error.log"), 'Create', 'Write', 'ReadWrite')
    $taskChildren.Add(@{ Process = $taskChild; Out = $taskOut; Err = $taskErr; CopyOut = $taskChild.StandardOutput.BaseStream.CopyToAsync($taskOut); CopyErr = $taskChild.StandardError.BaseStream.CopyToAsync($taskErr); Name = $Name })
}

try {
    Start-NexusDevChild 'backend' $taskDotnet $taskRoot @('watch', '--project', 'backend/src/AiNexus.Host/AiNexus.Host.csproj', 'run', '--no-launch-profile', '--', '--urls', "${taskScheme}://localhost:$BackendPort", '--Security:AllowInsecureLocalhost', "$($Http.IsPresent)", '--LocalConfigPath', $taskPaths.Settings, '--SecretsConfigPath', $taskPaths.Secrets)
    Start-NexusDevChild 'frontend' $taskNode (Join-Path $taskRoot 'frontend') (@($taskNg, 'serve', '--host', 'localhost', '--port', "$FrontendPort", '--proxy-config', $taskProxy) + $taskSslArguments)
    Write-Output "開發模式：${taskScheme}://localhost:$FrontendPort/chat（Angular + API）。儲存原始碼後自動更新。"
    Write-Output "啟動與錯誤紀錄：$taskLogs。Ctrl+C 同時停止兩個服務。"
    while ($true) {
        foreach ($taskChild in $taskChildren) {
            if ($taskChild.Process.HasExited) { throw "$($taskChild.Name) exited ($($taskChild.Process.ExitCode)). Inspect .local/logs/$($taskChild.Name).error.log and .log." }
        }
        Start-Sleep -Milliseconds 500
    }
} finally {
    foreach ($taskChild in $taskChildren) {
        if (!$taskChild.Process.HasExited) { $taskChild.Process.Kill($true) }
        $taskChild.Process.WaitForExit()
        try { [System.Threading.Tasks.Task]::WaitAll(@($taskChild.CopyOut, $taskChild.CopyErr), 3000) | Out-Null }
        finally { $taskChild.Out.Dispose(); $taskChild.Err.Dispose(); $taskChild.Process.Dispose() }
    }
}
