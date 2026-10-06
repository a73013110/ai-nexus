#requires -Version 7.4
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
$ErrorActionPreference = 'Stop'
$taskUri = [Uri]$Endpoint
if (!$taskUri.IsAbsoluteUri -or $taskUri.Scheme -notin @('http','https') -or $taskUri.UserInfo) { throw 'Endpoint must be a server-controlled HTTP URL without embedded credentials.' }
$taskBase = $Endpoint.TrimEnd('/')
$taskVersion = Invoke-RestMethod -Uri "$taskBase/api/version" -TimeoutSec 10
$taskCatalog = Invoke-RestMethod -Uri "$taskBase/api/tags" -TimeoutSec 10
foreach ($taskModel in @($ChatModel)) {
    if ($taskModel -notin $taskCatalog.models.name) { throw "Model not installed: $taskModel. Pull it on the Ollama host before running this probe." }
}
$taskEmbeddingBase = if ($EmbeddingEndpoint) { $EmbeddingEndpoint.TrimEnd('/') } else { $taskBase }
$taskEmbeddingUri = [Uri]$taskEmbeddingBase
if (!$taskEmbeddingUri.IsAbsoluteUri -or $taskEmbeddingUri.Scheme -notin @('http','https') -or $taskEmbeddingUri.UserInfo -or $taskEmbeddingUri.Query -or $taskEmbeddingUri.Fragment) { throw 'EmbeddingEndpoint must be an HTTP URL without embedded credentials, query or fragment.' }
$taskVectors = Invoke-RestMethod -Uri "$taskEmbeddingBase/api/embed" -Method Post -ContentType 'application/json' -TimeoutSec 180 `
    -Body (@{ model = $EmbeddingModel; input = @('合成索引測試 › 採購規範：主管核准後辦理採購。','合成索引測試 › 請假規範：請假申請應完成簽核。'); dimensions = $Dimensions; truncate = $false; keep_alive = '1m' } | ConvertTo-Json -Depth 4)
if (@($taskVectors.embeddings).Count -ne 2) { throw 'Embedding batch response count does not match input count.' }
foreach ($taskVector in $taskVectors.embeddings) {
    if (@($taskVector).Count -ne $Dimensions -or @($taskVector | Where-Object { ![double]::IsFinite([double]$_) }).Count -gt 0 -or @($taskVector | Where-Object { $_ -ne 0 }).Count -eq 0) { throw "Embedding dimension or values do not match VECTOR($Dimensions)." }
}
if ($RerankProvider -ne 'none') {
    $taskRerankUri = [Uri]$RerankEndpoint
    if (!$taskRerankUri.IsAbsoluteUri -or $taskRerankUri.Scheme -notin @('http','https') -or $taskRerankUri.UserInfo -or $taskRerankUri.Query -or $taskRerankUri.Fragment) { throw 'RerankEndpoint must be a server-controlled HTTP URL.' }
    $taskCandidates = @('主管核准後辦理採購。','請假申請應完成簽核。')
    $taskPayload = if ($RerankProvider -eq 'tei') { @{ query = '採購如何核准？'; texts = $taskCandidates; truncate = $false; return_text = $false } } else { @{ query = '採購如何核准？'; documents = $taskCandidates; model = $RerankModel; top_n = 2 } }
    $taskRoute = if ($RerankProvider -eq 'tei') { '/rerank' } else { '/v1/rerank' }
    $taskRanking = Invoke-RestMethod -Uri ($RerankEndpoint.TrimEnd('/') + $taskRoute) -Method Post -ContentType 'application/json' -TimeoutSec 30 -Body ($taskPayload | ConvertTo-Json -Depth 4)
    $taskScores = if ($RerankProvider -eq 'tei') { @($taskRanking) } else { @($taskRanking.results) }
    if ($taskScores.Count -ne 2 -or @($taskScores.index | Sort-Object -Unique).Count -ne 2) { throw 'Rerank response count or indexes are invalid.' }
    foreach ($taskScore in $taskScores) {
        $taskValue = if ($RerankProvider -eq 'tei') { $taskScore.score } else { $taskScore.relevance_score }
        if ($taskScore.index -notin @(0,1) -or ![double]::IsFinite([double]$taskValue)) { throw 'Rerank response score is invalid.' }
    }
}
$taskTimer = [Diagnostics.Stopwatch]::StartNew()
$taskResponse = Invoke-RestMethod -Uri "$taskBase/api/chat" -Method Post -ContentType 'application/json' -TimeoutSec 300 `
    -Body (@{ model = $ChatModel; messages = @(@{ role = 'user'; content = '以繁體中文寫三個建立文件索引的步驟，每個步驟一句。' }); stream = $false; think = $false; options = @{ num_ctx = $ContextTokens; num_predict = 128; temperature = 0.2 } } | ConvertTo-Json -Depth 6)
$taskTimer.Stop()
if (!$taskResponse.done -or !$taskResponse.message.content) { throw 'Local model did not complete the synthetic response.' }
$taskRate = if ($taskResponse.eval_duration -gt 0) { [Math]::Round($taskResponse.eval_count / ($taskResponse.eval_duration / 1000000000), 2) } else { $null }
[pscustomobject]@{ OllamaVersion = $taskVersion.version; ChatModel = $ChatModel; EmbeddingModel = $EmbeddingModel; Dimensions = $Dimensions; RerankProvider = $RerankProvider; ContextTokens = $ContextTokens; WallSeconds = [Math]::Round($taskTimer.Elapsed.TotalSeconds,2); LoadSeconds = [Math]::Round($taskResponse.load_duration / 1000000000,2); OutputTokensPerSecond = $taskRate; Completed = $taskResponse.done } | Format-List
Write-Output 'Local-only probe complete. No Google call or personal data was sent. Run ollama ps on the GPU host to check GPU/CPU placement; this is a short synthetic benchmark, not a production-load guarantee.'
