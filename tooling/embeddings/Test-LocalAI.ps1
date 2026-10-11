#requires -Version 7.4

<#
.SYNOPSIS
以合成資料實測本地 Ollama：聊天模型、批次 embedding、可選的 rerank，以及載入與輸出速度，研究用。

.DESCRIPTION
只送合成文字，不送私人資料，也不呼叫 Google。這是短的單次量測，不代表多人負載或檢索品質；
GPU／CPU 配置在 GPU 主機上以 ollama ps 查看。說明見 docs/research/local-ai.md。

.PARAMETER Endpoint
Ollama 網址。

.PARAMETER ChatModel
要測的聊天模型，必須已安裝。

.PARAMETER EmbeddingModel
要測的 embedding 模型。

.PARAMETER Dimensions
embedding 維度，對應資料庫的 VECTOR(768) 或 VECTOR(1024)。

.PARAMETER EmbeddingEndpoint
embedding 服務網址；預設與 Endpoint 相同。

.PARAMETER RerankProvider
rerank 服務種類；none 表示不測。

.PARAMETER RerankEndpoint
rerank 服務網址。

.PARAMETER RerankModel
rerank 模型（openai-compatible 時使用）。

.PARAMETER ContextTokens
聊天測試的 context 長度。

.EXAMPLE
./tooling/embeddings/Test-LocalAI.ps1 -Endpoint 'http://localhost:11434/'
#>
[CmdletBinding()]
param(
    [string]$Endpoint = 'http://localhost:11434/',
    [string]$ChatModel = 'qwen3:8b',
    [string]$EmbeddingModel = 'bge-m3',
    [ValidateSet(768,1024)][int]$Dimensions = 1024,
    [string]$EmbeddingEndpoint = '',
    [ValidateSet('none','tei','openai-compatible')][string]$RerankProvider = 'none',
    [string]$RerankEndpoint = '',
    [string]$RerankModel = 'bge-reranker-v2-m3',
    [ValidateRange(1024,32768)][int]$ContextTokens = 8192
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$uri = [Uri]$Endpoint
if (!$uri.IsAbsoluteUri -or $uri.Scheme -notin @('http','https') -or $uri.UserInfo) { throw 'Endpoint must be a server-controlled HTTP URL without embedded credentials.' }
$base = $Endpoint.TrimEnd('/')
$version = Invoke-RestMethod -Uri "$base/api/version" -TimeoutSec 10
$catalog = Invoke-RestMethod -Uri "$base/api/tags" -TimeoutSec 10
foreach ($model in @($ChatModel)) {
    if ($model -notin $catalog.models.name) { throw "Model not installed: $model. Pull it on the Ollama host before running this probe." }
}
$embeddingBase = if ($EmbeddingEndpoint) { $EmbeddingEndpoint.TrimEnd('/') } else { $base }
$embeddingUri = [Uri]$embeddingBase
if (!$embeddingUri.IsAbsoluteUri -or $embeddingUri.Scheme -notin @('http','https') -or $embeddingUri.UserInfo -or $embeddingUri.Query -or $embeddingUri.Fragment) { throw 'EmbeddingEndpoint must be an HTTP URL without embedded credentials, query or fragment.' }
$vectors = Invoke-RestMethod -Uri "$embeddingBase/api/embed" -Method Post -ContentType 'application/json' -TimeoutSec 180 `
    -Body (@{ model = $EmbeddingModel; input = @('合成索引測試 › 採購規範：主管核准後辦理採購。','合成索引測試 › 請假規範：請假申請應完成簽核。'); dimensions = $Dimensions; truncate = $false; keep_alive = '1m' } | ConvertTo-Json -Depth 4)
if (@($vectors.embeddings).Count -ne 2) { throw 'Embedding batch response count does not match input count.' }
foreach ($vector in $vectors.embeddings) {
    if (@($vector).Count -ne $Dimensions -or @($vector | Where-Object { ![double]::IsFinite([double]$_) }).Count -gt 0 -or @($vector | Where-Object { $_ -ne 0 }).Count -eq 0) { throw "Embedding dimension or values do not match VECTOR($Dimensions)." }
}
if ($RerankProvider -ne 'none') {
    $rerankUri = [Uri]$RerankEndpoint
    if (!$rerankUri.IsAbsoluteUri -or $rerankUri.Scheme -notin @('http','https') -or $rerankUri.UserInfo -or $rerankUri.Query -or $rerankUri.Fragment) { throw 'RerankEndpoint must be a server-controlled HTTP URL.' }
    $candidates = @('主管核准後辦理採購。','請假申請應完成簽核。')
    $payload = if ($RerankProvider -eq 'tei') { @{ query = '採購如何核准？'; texts = $candidates; truncate = $false; return_text = $false } } else { @{ query = '採購如何核准？'; documents = $candidates; model = $RerankModel; top_n = 2 } }
    $route = if ($RerankProvider -eq 'tei') { '/rerank' } else { '/v1/rerank' }
    $ranking = Invoke-RestMethod -Uri ($RerankEndpoint.TrimEnd('/') + $route) -Method Post -ContentType 'application/json' -TimeoutSec 30 -Body ($payload | ConvertTo-Json -Depth 4)
    $scores = if ($RerankProvider -eq 'tei') { @($ranking) } else { @($ranking.results) }
    if ($scores.Count -ne 2 -or @($scores.index | Sort-Object -Unique).Count -ne 2) { throw 'Rerank response count or indexes are invalid.' }
    foreach ($score in $scores) {
        $value = if ($RerankProvider -eq 'tei') { $score.score } else { $score.relevance_score }
        if ($score.index -notin @(0,1) -or ![double]::IsFinite([double]$value)) { throw 'Rerank response score is invalid.' }
    }
}
$timer = [Diagnostics.Stopwatch]::StartNew()
$response = Invoke-RestMethod -Uri "$base/api/chat" -Method Post -ContentType 'application/json' -TimeoutSec 300 `
    -Body (@{ model = $ChatModel; messages = @(@{ role = 'user'; content = '以繁體中文寫三個建立文件索引的步驟，每個步驟一句。' }); stream = $false; think = $false; options = @{ num_ctx = $ContextTokens; num_predict = 128; temperature = 0.2 } } | ConvertTo-Json -Depth 6)
$timer.Stop()
if (!$response.done -or !$response.message.content) { throw 'Local model did not complete the synthetic response.' }
$rate = if ($response.eval_duration -gt 0) { [Math]::Round($response.eval_count / ($response.eval_duration / 1000000000), 2) } else { $null }
[pscustomobject]@{ OllamaVersion = $version.version; ChatModel = $ChatModel; EmbeddingModel = $EmbeddingModel; Dimensions = $Dimensions; RerankProvider = $RerankProvider; ContextTokens = $ContextTokens; WallSeconds = [Math]::Round($timer.Elapsed.TotalSeconds,2); LoadSeconds = [Math]::Round($response.load_duration / 1000000000,2); OutputTokensPerSecond = $rate; Completed = $response.done } | Format-List
Write-Output 'Local-only probe complete. No Google call or personal data was sent. Run ollama ps on the GPU host to check GPU/CPU placement; this is a short synthetic benchmark, not a production-load guarantee.'
