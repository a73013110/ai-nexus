#requires -Version 7.4
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
Initialize-NexusLocalSettings
$taskPaths = Get-NexusLocalPaths
$taskConfig = [System.IO.File]::ReadAllText($taskPaths.Settings) | ConvertFrom-Json -AsHashtable
$taskSecrets = [System.IO.File]::ReadAllText($taskPaths.Secrets) | ConvertFrom-Json -AsHashtable
function Read-NexusSecret([string]$Prompt) {
    $taskSecure = Read-Host $Prompt -AsSecureString
    try { return [System.Net.NetworkCredential]::new('', $taskSecure).Password }
    finally { $taskSecure.Dispose() }
}
Write-Output 'Enter 保留現有值。密碼與 key 以遮蔽輸入，儲存於 .local/secrets。'
foreach ($taskField in @(
    @{ Section = 'Database'; Name = 'Server'; Prompt = 'SQL server／instance' },
    @{ Section = 'Database'; Name = 'Name'; Prompt = '專用資料庫名稱' },
    @{ Section = 'AdAuthentication'; Name = 'Url'; Prompt = 'AD LDAP URL 與 Base DN' },
    @{ Section = 'AdAuthentication'; Name = 'DnUser'; Prompt = 'AD 服務帳號 DN' },
    @{ Section = 'AdAuthentication'; Name = 'Domain'; Prompt = 'AD 網域名稱' }
)) {
    $taskValue = Read-Host ($taskField.Prompt + '（Enter 保留）')
    if ($taskValue) { $taskConfig[$taskField.Section][$taskField.Name] = $taskValue.Trim() }
}
foreach ($taskField in @(
    @{ Section = 'Database'; Name = 'User'; Prompt = '既有 SQL 登入帳號' },
    @{ Section = 'Database'; Name = 'Password'; Prompt = 'SQL 密碼' },
    @{ Section = 'AdAuthentication'; Name = 'DnPass'; Prompt = 'AD 服務帳號密碼' },
    @{ Section = 'Inference.Providers.Google'; Name = 'ApiKey'; Prompt = 'Google AI API key' }
)) {
    $taskValue = Read-NexusSecret ($taskField.Prompt + '（遮蔽輸入，Enter 保留）')
    if ($taskValue) { Set-NexusSetting $taskSecrets ($taskField.Section + '.' + $taskField.Name) $taskValue }
}
Save-NexusJson $taskPaths.Settings $taskConfig
Save-NexusJson $taskPaths.Secrets $taskSecrets
Protect-NexusSecrets $taskPaths.Secrets
$taskValue = $taskSecrets = $null
Write-Output '設定已保存。執行 scripts/Initialize-Database.ps1 初始化資料庫，再重新啟動專案。'
