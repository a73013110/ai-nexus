#requires -Version 7.4

<#
.SYNOPSIS
把已提交的原始碼打包成源碼掃描用的 ZIP，放在 repo 根目錄。

.DESCRIPTION
以 git archive 打包 HEAD，只含版本控制內的檔案：node_modules、bin/obj、artifacts、.local 與秘密檔本來就不在裡面。
另外排除文件（docs、Markdown）、AI 規則（.claude）、圖示與測試（後端 tests、e2e、*.spec.ts、腳本測試），測試裡的假帳密只會變成誤報。
lockfile（package-lock.json、packages.lock.json）保留，供相依套件弱點分析（SCA）使用。
未提交的變更不會進 ZIP；工作目錄有變更時會提醒。檔名帶日期與 commit，上傳後請自行刪除（.gitignore 已忽略）。

.PARAMETER IncludeTests
連測試程式一起打包。

.COMPONENT
發布

.EXAMPLE
./scripts/Export-SourceArchive.ps1

.EXAMPLE
./scripts/Export-SourceArchive.ps1 -IncludeTests
#>
[CmdletBinding()]
param([switch]$IncludeTests)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-RepoRoot
$excludes = @('docs', '*.md', '.claude', '*.ico', '*.svg', '*.png', '*.jpg', '*.gif', '*.woff', '*.woff2')
if (!$IncludeTests) { $excludes += @('backend/tests', 'frontend/e2e', 'scripts/tests', '*.spec.ts') }

Push-Location -LiteralPath $root
try {
    $commit = git rev-parse --short HEAD
    if ($LASTEXITCODE -ne 0) { throw '讀不到目前的 commit。' }
    $dirty = @(git status --porcelain --untracked-files=no)
    $path = Join-Path $root "ai-nexus-source-$(Get-Date -Format 'yyyyMMdd-HHmm')-$commit.zip"
    # 「:(exclude,glob)」不帶斜線的樣式（*.md）只比對檔名，帶斜線的比對整段路徑；資料夾要寫成 <dir>/** 才會排除底下所有檔案。
    $pathspecs = @('.') + @($excludes | ForEach-Object { if ($_.Contains('*')) { ":(exclude,glob)**/$_" } else { ":(exclude,glob)$_/**" } })
    git archive --format=zip --output=$path HEAD -- @pathspecs
    if ($LASTEXITCODE -ne 0) { throw 'git archive 打包失敗。' }

    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try { $count = @($zip.Entries | Where-Object { !$_.FullName.EndsWith('/') }).Count } finally { $zip.Dispose() }
    $size = '{0:N1} MB' -f ((Get-Item -LiteralPath $path).Length / 1MB)
    Write-Output "已產生 $(Split-Path $path -Leaf)（commit $commit，$count 個檔案，$size）。"
    if (!$IncludeTests) { Write-Output '未含測試程式；需要時加 -IncludeTests。' }
    if ($dirty.Count) { Write-Warning "工作目錄有 $($dirty.Count) 個未提交的變更，ZIP 只含已提交的內容。" }
} finally { Pop-Location }
