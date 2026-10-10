#requires -Version 7.4

<#
.SYNOPSIS
以合成語料比較 embedding 模型的檢索表現（Recall／MRR），研究用。

.DESCRIPTION
呼叫 compare.py，對 Ollama 端點上的模型做獨立的 dense cosine 比較，結果寫在 artifacts/embeddings。
不安裝或移除模型。需要 Python 3。說明見 docs/research/EMBEDDING_MODELS.md。

.PARAMETER Endpoint
Ollama 網址。

.PARAMETER CorpusPath
語料 JSON；預設 sample-corpus.json。

.PARAMETER ProfilesPath
模型與 query profile JSON；預設 profiles.json。

.PARAMETER TopK
每個查詢取前幾筆。

.PARAMETER ValidateOnly
只檢查語料與 profile 格式，不呼叫模型。

.EXAMPLE
./tooling/embeddings/Compare-Embeddings.ps1 -ValidateOnly
#>
[CmdletBinding()]
param(
    [string]$Endpoint = 'http://localhost:11434/',
    [string]$CorpusPath,
    [string]$ProfilesPath,
    [ValidateRange(1, 20)][int]$TopK = 3,
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!(Get-Command python -ErrorAction SilentlyContinue)) { throw '需要 Python 3。' }
$arguments = @((Join-Path $PSScriptRoot 'compare.py'), '--url', $Endpoint, '--top-k', $TopK)
if ($CorpusPath) { $arguments += @('--corpus', (Resolve-Path -LiteralPath $CorpusPath).Path) }
if ($ProfilesPath) { $arguments += @('--profiles', (Resolve-Path -LiteralPath $ProfilesPath).Path) }
if ($ValidateOnly) { $arguments += '--validate-only' }
python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Embedding 比較未完成；沒有安裝或移除任何模型。' }
