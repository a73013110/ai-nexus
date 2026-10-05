#requires -Version 7.4
param(
    [string]$Endpoint = 'http://localhost:11434/',
    [string]$CorpusPath,
    [string]$ProfilesPath,
    [ValidateRange(1,20)][int]$TopK = 3,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (!(Get-Command python -ErrorAction SilentlyContinue)) { throw 'Python 3 is required for the local embedding comparison tool.' }
$taskArguments = @((Join-Path $taskRoot 'tooling/embeddings/compare.py'), '--url', $Endpoint, '--top-k', $TopK)
if ($CorpusPath) { $taskArguments += @('--corpus', (Resolve-Path -LiteralPath $CorpusPath).Path) }
if ($ProfilesPath) { $taskArguments += @('--profiles', (Resolve-Path -LiteralPath $ProfilesPath).Path) }
if ($ValidateOnly) { $taskArguments += '--validate-only' }
python @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Embedding comparison was not completed. No models were installed or removed.' }
