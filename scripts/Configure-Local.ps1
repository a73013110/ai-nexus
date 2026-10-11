#requires -Version 7.4

<#
.SYNOPSIS
互動輸入本機的 SQL、AD 與 Google AI 設定。

.DESCRIPTION
一般參數寫入 .local/config/appsettings.Local.json，密碼與 key 以遮蔽輸入寫入 .local/secrets/appsettings.Secrets.json
（只有目前使用者可讀）。每一項按 Enter 保留現有值。其餘設定直接編輯這兩個檔案，見 docs/development/configuration.md。

.EXAMPLE
./scripts/Configure-Local.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

function Read-Secret([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    try { [Net.NetworkCredential]::new('', $secure).Password }
    finally { $secure.Dispose() }
}

$local = Initialize-NexusLocalSettings
$settings = Read-NexusJson $local.Settings
$secrets = Read-NexusJson $local.Secrets
Write-Output 'Enter 保留現有值。密碼與 key 以遮蔽輸入，儲存於 .local/secrets。'
foreach ($field in @(
    @{ Path = 'Database.Server'; Prompt = 'SQL server／instance' },
    @{ Path = 'Database.Name'; Prompt = '專用資料庫名稱' },
    @{ Path = 'Identity.ActiveDirectory.Domain'; Prompt = 'AD 網域，例如 company.internal' },
    @{ Path = 'Identity.ActiveDirectory.BindUser'; Prompt = 'AD 服務帳號名稱' }
)) {
    $value = Read-Host "$($field.Prompt)（Enter 保留）"
    if ($value) { Set-NexusSetting $settings $field.Path $value.Trim() }
}
foreach ($field in @(
    @{ Path = 'Database.User'; Prompt = '既有 SQL 登入帳號' },
    @{ Path = 'Database.Password'; Prompt = 'SQL 密碼' },
    @{ Path = 'Identity.ActiveDirectory.BindPassword'; Prompt = 'AD 服務帳號密碼' },
    @{ Path = 'Inference.Providers.Google.ApiKey'; Prompt = 'Google AI API key' }
)) {
    $value = Read-Secret "$($field.Prompt)（遮蔽輸入，Enter 保留）"
    if ($value) { Set-NexusSetting $secrets $field.Path $value }
}
Save-NexusJson $local.Settings $settings
Save-NexusJson $local.Secrets $secrets
Protect-NexusSecrets $local.Secrets
$value = $secrets = $null
Write-Output '設定已保存。執行 scripts/Initialize-Database.ps1 初始化資料庫，再重新啟動專案。'
