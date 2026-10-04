#requires -Version 7.4
param(
    [string]$Endpoint = 'http://localhost:11434/',
    [string]$ChatModel = 'qwen3:8b',
    [string]$EmbeddingModel = 'qwen3-embedding:0.6b',
    [ValidateRange(768,768)][int]$Dimensions = 768,
    [ValidateRange(1024,32768)][int]$ContextTokens = 8192
)
$ErrorActionPreference = 'Stop'
$taskUri = [Uri]$Endpoint
if (!$taskUri.IsAbsoluteUri -or $taskUri.Scheme -notin @('http','https') -or $taskUri.UserInfo) { throw 'Endpoint must be a server-controlled HTTP URL without embedded credentials.' }
$taskBase = $Endpoint.TrimEnd('/')
$taskVersion = Invoke-RestMethod -Uri "$taskBase/api/version" -TimeoutSec 10
$taskCatalog = Invoke-RestMethod -Uri "$taskBase/api/tags" -TimeoutSec 10
foreach ($taskModel in @($ChatModel, $EmbeddingModel)) {
    if ($taskModel -notin $taskCatalog.models.name) { throw "Model not installed: $taskModel. Pull it on the Ollama host before running this probe." }
}
$taskVectors = Invoke-RestMethod -Uri "$taskBase/api/embed" -Method Post -ContentType 'application/json' -TimeoutSec 180 `
    -Body (@{ model = $EmbeddingModel; input = @('公文簽核與文件版本的合成測試文字'); dimensions = $Dimensions; truncate = $false; keep_alive = '1m' } | ConvertTo-Json -Depth 4)
if (@($taskVectors.embeddings[0]).Count -ne $Dimensions) { throw 'Embedding dimension does not match VECTOR(768). Confirm model and Ollama dimensions support before indexing.' }
$taskTimer = [Diagnostics.Stopwatch]::StartNew()
$taskResponse = Invoke-RestMethod -Uri "$taskBase/api/chat" -Method Post -ContentType 'application/json' -TimeoutSec 300 `
    -Body (@{ model = $ChatModel; messages = @(@{ role = 'user'; content = '以繁體中文寫三個建立文件索引的步驟，每個步驟一句。' }); stream = $false; think = $false; options = @{ num_ctx = $ContextTokens; num_predict = 128; temperature = 0.2 } } | ConvertTo-Json -Depth 6)
$taskTimer.Stop()
if (!$taskResponse.done -or !$taskResponse.message.content) { throw 'Local model did not complete the synthetic response.' }
$taskRate = if ($taskResponse.eval_duration -gt 0) { [Math]::Round($taskResponse.eval_count / ($taskResponse.eval_duration / 1000000000), 2) } else { $null }
[pscustomobject]@{ OllamaVersion = $taskVersion.version; ChatModel = $ChatModel; EmbeddingModel = $EmbeddingModel; Dimensions = $Dimensions; ContextTokens = $ContextTokens; WallSeconds = [Math]::Round($taskTimer.Elapsed.TotalSeconds,2); LoadSeconds = [Math]::Round($taskResponse.load_duration / 1000000000,2); OutputTokensPerSecond = $taskRate; Completed = $taskResponse.done } | Format-List
Write-Output 'Local-only probe complete. No Google call or personal data was sent. Run ollama ps on the GPU host to check GPU/CPU placement; this is a short synthetic benchmark, not a production-load guarantee.'
