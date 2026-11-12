#requires -Version 7.4

<#
.SYNOPSIS
把原始碼打包成源碼掃描用的 ZIP，放在 repo 根目錄。

.DESCRIPTION
預設打包工作目錄的現況：版本控制內的檔案（含未提交的修改）加上未被 .gitignore 忽略的新檔，
所以 node_modules、bin/obj、artifacts、.local 與秘密檔不會進去。-Committed 改為只打包 HEAD（git archive）。
另外排除文件（docs、Markdown）、AI 規則（.claude）、圖示與測試（後端 tests、e2e、*.spec.ts、腳本測試），測試裡的假帳密只會變成誤報。
lockfile（package-lock.json、packages.lock.json）保留，供相依套件弱點分析（SCA）使用。
檔名帶日期與 commit，含未提交的變更時加 -dirty；上傳後請自行刪除（.gitignore 已忽略）。

.PARAMETER Committed
只打包已提交的 HEAD，不含工作目錄的變更。

.PARAMETER IncludeTests
連測試程式一起打包。

.COMPONENT
發布

.EXAMPLE
./scripts/Export-SourceArchive.ps1

.EXAMPLE
./scripts/Export-SourceArchive.ps1 -Committed

.EXAMPLE
./scripts/Export-SourceArchive.ps1 -IncludeTests
#>
[CmdletBinding()]
param([switch]$Committed, [switch]$IncludeTests)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$excludes = @('docs', '*.md', '.claude', '*.ico', '*.svg', '*.png', '*.jpg', '*.gif', '*.woff', '*.woff2')
if (!$IncludeTests) { $excludes += @('backend/tests', 'frontend/e2e', 'scripts/tests', '*.spec.ts') }
# 「:(exclude,glob)」不帶斜線的樣式（*.md）只比對檔名，帶斜線的比對整段路徑；資料夾要寫成 <dir>/** 才會排除底下所有檔案。
$pathspecs = @('.') + @($excludes | ForEach-Object { if ($_.Contains('*')) { ":(exclude,glob)**/$_" } else { ":(exclude,glob)$_/**" } })

Push-Location -LiteralPath $root
try {
    $commit = git rev-parse --short HEAD
    if ($LASTEXITCODE -ne 0) { throw '讀不到目前的 commit。' }
    $changes = @(git status --porcelain -- @pathspecs)
    $suffix = if (!$Committed -and $changes.Count) { '-dirty' } else { '' }
    $path = Join-Path $root "ai-nexus-source-$(Get-Date -Format 'yyyyMMdd-HHmm')-$commit$suffix.zip"

    if ($Committed) {
        git archive --format=zip --output=$path HEAD -- @pathspecs
        if ($LASTEXITCODE -ne 0) { throw 'git archive 打包失敗。' }
    } else {
        # --cached 列出已追蹤的檔案（含工作目錄已刪除的，下面略過），--others --exclude-standard 補上未被忽略的新檔。
        $files = @(git -c core.quotePath=false ls-files --cached --others --exclude-standard -- @pathspecs | Sort-Object -Unique)
        if ($LASTEXITCODE -ne 0) { throw '列出檔案失敗。' }
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $files) {
                $source = Join-Path $root $file
                if (Test-Path -LiteralPath $source -PathType Leaf) {
                    $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $source, $file, [IO.Compression.CompressionLevel]::Optimal)
                }
            }
        } finally { $zip.Dispose() }
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try { $count = @($zip.Entries | Where-Object { !$_.FullName.EndsWith('/') }).Count } finally { $zip.Dispose() }
    $size = '{0:N1} MB' -f ((Get-Item -LiteralPath $path).Length / 1MB)
    $source = if ($Committed) { "commit $commit" } elseif ($changes.Count) { "工作目錄，commit $commit 加 $($changes.Count) 個未提交的變更" } else { "工作目錄，與 commit $commit 相同" }
    Write-Output "已產生 $(Split-Path $path -Leaf)（$source，$count 個檔案，$size）。"
    if (!$IncludeTests) { Write-Output '未含測試程式；需要時加 -IncludeTests。' }
    if ($Committed -and $changes.Count) { Write-Warning "工作目錄有 $($changes.Count) 個未提交的變更，沒有包含在 ZIP 裡。" }
} finally { Pop-Location }
