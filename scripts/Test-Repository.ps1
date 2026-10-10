#requires -Version 7.4

<#
.SYNOPSIS
提交前檢查 Git index（或工作目錄）有沒有本機檔案、產物或秘密。

.DESCRIPTION
擋下 .local、artifacts、bin/obj、node_modules 與本機 appsettings 等路徑，常見的 live key 與私鑰，
這台機器 .local/secrets 中的密碼與 key，公開設定檔中非空的秘密欄位，以及 whitespace 錯誤。
只輸出檔名與結果，不輸出秘密或檔案內容；不能取代審閱 diff 或組織的秘密掃描。

.PARAMETER WorkingTree
檢查追蹤中與未忽略的新檔，而不是 staged 的內容；不會改動 Git index。

.EXAMPLE
git add -A; ./scripts/Test-Repository.ps1

.EXAMPLE
./scripts/Test-Repository.ps1 -WorkingTree
#>
[CmdletBinding()]
param([switch]$WorkingTree)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
$localSecrets = @()
$content = $null
Push-Location -LiteralPath $root
try {
    $files = if ($WorkingTree) {
        @(git -c core.quotepath=false ls-files --cached --others --exclude-standard | Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf } | Select-Object -Unique)
    } else { @(git -c core.quotepath=false ls-files --cached) }
    if ($LASTEXITCODE -ne 0 -or !$files.Count) { throw '請先 stage 要提交的檔案。' }
    if (@($files | Where-Object { Test-NexusForbiddenPath $_ }).Count) { throw 'Git index 含有本機、產物或秘密路徑；提交前請先 unstage。' }

    $secretsPath = (Get-NexusLocalPaths).Secrets
    if (Test-Path -LiteralPath $secretsPath) {
        try { $secrets = Read-NexusJson $secretsPath }
        catch { throw '本機秘密 JSON 格式錯誤；請用編輯器修正（未輸出任何值）。' }
        $localSecrets = @(Get-NexusSecretValues $secrets | Where-Object { $_.Length -ge 6 })
        $secrets = $null
    }
    foreach ($file in $files) {
        if ($WorkingTree) { $content = [IO.File]::ReadAllText((Join-Path $root $file)) }
        else {
            $content = (git show (':' + $file)) -join "`n"
            if ($LASTEXITCODE -ne 0) { throw "無法讀取 staged 檔案：$file" }
        }
        if ($content -match 'AIza[0-9A-Za-z_-]{35}|-{5}BEGIN (?:[A-Z]+ )?PRIVATE KEY-{5}') { throw "可能含有 live key：$file" }
        foreach ($value in $localSecrets) {
            if ($content.Contains($value, [StringComparison]::Ordinal) -or $content.Contains(($value | ConvertTo-Json -Compress).Trim('"'), [StringComparison]::Ordinal)) { throw "含有本機秘密值：$file" }
        }
        if ($file -match '(^|/)appsettings[^/]*\.json$') {
            try { $json = $content | ConvertFrom-Json -AsHashtable }
            catch { throw "公開設定 JSON 格式錯誤：$file" }
            if (@(Get-NexusSecretValues $json -IncludeUser).Count) { throw "公開設定的秘密欄位必須留空：$file" }
        }
    }
    if ($WorkingTree) { git -c core.whitespace=-blank-at-eof diff --check }
    else { git -c core.whitespace=-blank-at-eof diff --cached --check }
    if ($LASTEXITCODE -ne 0) { throw '有 whitespace 錯誤。' }
    $scope = if ($WorkingTree) { '工作目錄' } else { 'Staged' }
    Write-Output "$scope 檢查通過：$($files.Count) 個檔案；沒有本機／產物路徑，公開設定的秘密欄位為空，未發現 live key 或本機密碼／key。"
    Write-Output '這是針對此專案的檢查；提交前仍請審閱 git diff --cached。'
} finally {
    $localSecrets = $content = $null
    Pop-Location
}
