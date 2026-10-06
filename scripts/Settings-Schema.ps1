# Shared by setup, migration, formatting and publishing; never writes setting values to stdout.
$script:taskNexusSettingsLayout = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'settings-layout.json')) | ConvertFrom-Json -AsHashtable
function Get-NexusSetting([System.Collections.IDictionary]$Value, [string]$Path) {
    $taskNode = $Value
    foreach ($taskKey in $Path.Split('.')) {
        if ($taskNode -isnot [System.Collections.IDictionary] -or !$taskNode.Contains($taskKey)) { return $null }
        $taskNode = $taskNode[$taskKey]
    }
    return ,$taskNode
}

function Set-NexusSetting([System.Collections.IDictionary]$Value, [string]$Path, $Setting) {
    $taskKeys = $Path.Split('.')
    $taskNode = $Value
    for ($taskIndex = 0; $taskIndex -lt $taskKeys.Length - 1; $taskIndex++) {
        $taskKey = $taskKeys[$taskIndex]
        if ($taskNode[$taskKey] -isnot [System.Collections.IDictionary]) { $taskNode[$taskKey] = [ordered]@{} }
        $taskNode = $taskNode[$taskKey]
    }
    $taskNode[$taskKeys[-1]] = $Setting
}

function Move-NexusSetting([System.Collections.IDictionary]$Value, [string]$From, [string]$To) {
    $taskKeys = $From.Split('.')
    $taskNode = $Value
    for ($taskIndex = 0; $taskIndex -lt $taskKeys.Length - 1; $taskIndex++) {
        if ($taskNode[$taskKeys[$taskIndex]] -isnot [System.Collections.IDictionary]) { return }
        $taskNode = $taskNode[$taskKeys[$taskIndex]]
    }
    if (!$taskNode.Contains($taskKeys[-1])) { return }
    # An explicitly supplied v2 value wins over a legacy field.
    if ($null -eq (Get-NexusSetting $Value $To)) { Set-NexusSetting $Value $To $taskNode[$taskKeys[-1]] }
    $taskNode.Remove($taskKeys[-1]) | Out-Null
}

function Remove-NexusSetting([System.Collections.IDictionary]$Value, [string]$Path) {
    $taskKeys = $Path.Split('.')
    $taskNode = $Value
    for ($taskIndex = 0; $taskIndex -lt $taskKeys.Length - 1; $taskIndex++) {
        if ($taskNode[$taskKeys[$taskIndex]] -isnot [System.Collections.IDictionary]) { return }
        $taskNode = $taskNode[$taskKeys[$taskIndex]]
    }
    $taskNode.Remove($taskKeys[-1]) | Out-Null
}

function ConvertTo-NexusV3([System.Collections.IDictionary]$Value) {
    $taskProvider = if ($Value.Inference.Provider -eq 'ollama') { 'Ollama' } else { 'Google' }
    foreach ($taskKey in @('QueueCapacity', 'TimeoutSeconds', 'MaxInputCharacters', 'MaxOutputCharacters')) { Move-NexusSetting $Value "Inference.$taskKey" "Inference.Execution.$taskKey" }
    foreach ($taskKey in @('AllowModelSelection', 'ShowModelNames')) { Move-NexusSetting $Value "Inference.$taskKey" "Inference.ModelPolicy.$taskKey" }
    Move-NexusSetting $Value 'Inference.DefaultModelId' "Inference.Providers.$taskProvider.DefaultModelId"
    Move-NexusSetting $Value 'Inference.BaseUrl' 'Inference.Providers.Ollama.Endpoint'
    Move-NexusSetting $Value 'Inference.GoogleApiKey' 'Inference.Providers.Google.ApiKey'
    Move-NexusSetting $Value 'Inference.SystemPrompt' 'Prompts.DefaultSystemInstruction'
    if ($null -ne $Value.Inference -and $Value.Inference.Contains('Models')) {
        $taskModels = [ordered]@{}
        $taskIndex = 0
        foreach ($taskModel in $Value.Inference.Models) {
            $taskIndex++
            $taskAlias = if ($taskIndex -eq 1) { 'default' } else { 'model{0:00}' -f $taskIndex }
            $taskModels[$taskAlias] = $taskModel
        }
        if ($null -eq (Get-NexusSetting $Value "Inference.Providers.$taskProvider.Models")) { Set-NexusSetting $Value "Inference.Providers.$taskProvider.Models" $taskModels }
        $Value.Inference.Remove('Models') | Out-Null
    }
    foreach ($taskPair in @(@('EmbeddingProvider','Provider'), @('EmbeddingModel','Model'), @('Dimensions','Dimensions'), @('MaxDailyEmbeddingRequests','MaxDailyRequests'))) {
        Move-NexusSetting $Value ("Knowledge." + $taskPair[0]) ("Knowledge.Embedding." + $taskPair[1])
    }
    foreach ($taskKey in @('MaxCollections','MaxDocumentsPerCollection','ChunkCharacters','ChunkOverlap')) { Move-NexusSetting $Value "Knowledge.$taskKey" "Knowledge.Indexing.$taskKey" }
    foreach ($taskKey in @('UseNativeVector','PortableCandidateLimit','TopK','ContextCharacters')) { Move-NexusSetting $Value "Knowledge.$taskKey" "Knowledge.Retrieval.$taskKey" }
    $taskOldCharacters = Get-NexusSetting $Value 'Knowledge.Indexing.ChunkCharacters'
    $taskOldOverlap = Get-NexusSetting $Value 'Knowledge.Indexing.ChunkOverlap'
    if ($null -ne $taskOldCharacters) {
        if ($null -eq (Get-NexusSetting $Value 'Knowledge.Indexing.ChunkTargetTokens')) { Set-NexusSetting $Value 'Knowledge.Indexing.ChunkTargetTokens' ([Math]::Max(80, [Math]::Ceiling($taskOldCharacters / 2))) }
        if ($null -eq (Get-NexusSetting $Value 'Knowledge.Indexing.ChunkMaxTokens')) { Set-NexusSetting $Value 'Knowledge.Indexing.ChunkMaxTokens' ([Math]::Max(700, [Math]::Ceiling($taskOldCharacters / 2))) }
        if ($null -eq (Get-NexusSetting $Value 'Knowledge.Indexing.ChunkMinTokens')) { Set-NexusSetting $Value 'Knowledge.Indexing.ChunkMinTokens' 80 }
        if ($null -ne $taskOldOverlap -and $null -eq (Get-NexusSetting $Value 'Knowledge.Indexing.ChunkOverlapRatio')) { Set-NexusSetting $Value 'Knowledge.Indexing.ChunkOverlapRatio' ([Math]::Min(0.3, $taskOldOverlap / [Math]::Max(1, $taskOldCharacters))) }
    }
    $taskOldContext = Get-NexusSetting $Value 'Knowledge.Retrieval.ContextCharacters'
    if ($null -ne $taskOldContext -and $null -eq (Get-NexusSetting $Value 'Knowledge.Retrieval.ContextTokens')) { Set-NexusSetting $Value 'Knowledge.Retrieval.ContextTokens' ([Math]::Max(100, [Math]::Ceiling($taskOldContext / 2))) }
    foreach ($taskPath in @('Knowledge.Indexing.ChunkCharacters','Knowledge.Indexing.ChunkOverlap','Knowledge.Retrieval.ContextCharacters','Knowledge.Retrieval.UseNativeVector','Knowledge.Retrieval.PortableCandidateLimit')) { Remove-NexusSetting $Value $taskPath }
    foreach ($taskKey in @('Gdweb','Meiho')) { Move-NexusSetting $Value "Integrations.$taskKey" "Integrations.Sources.$taskKey" }
    if ($Value.Database) { $Value.Database.Remove('AllowUntrustedCertificateInProduction') | Out-Null }
    if ($Value.Inference) {
        foreach ($taskProviderKey in @('Google','Ollama')) {
            $taskSection = Get-NexusSetting $Value "Inference.Providers.$taskProviderKey"
            if ($taskSection -and $taskSection.Contains('Models')) {
                if (!$taskSection.Contains('Enabled')) { $taskSection.Enabled = $taskProviderKey -eq $taskProvider }
                if (!$taskSection.Contains('MaxConcurrency')) { $taskSection.MaxConcurrency = 1 }
                if ($taskSection.DefaultModelId -and !(Get-NexusSetting $Value 'Inference.ModelPolicy.DefaultModelId') -and $taskProviderKey -eq $taskProvider) {
                    Set-NexusSetting $Value 'Inference.ModelPolicy.DefaultModelId' ($taskProviderKey.ToLowerInvariant() + '/' + $taskSection.DefaultModelId)
                }
                $taskSection.Remove('DefaultModelId') | Out-Null
            }
        }
        $Value.Inference.Remove('Provider') | Out-Null
    }
    if ($Value.Attachments) {
        $Value.Attachments.Remove('MaxOwnerBytes') | Out-Null
        if (!$Value.Attachments.Contains('DefaultOwnerLimitBytes')) { $Value.Attachments.DefaultOwnerLimitBytes = 5000000000 }
    }
    $Value.ConfigurationVersion = 3
    return $Value
}

function ConvertTo-NexusOrdered($Value, [string]$Path = '') {
    if ($Value -is [System.Collections.IDictionary]) {
        $taskLayout = $script:taskNexusSettingsLayout
        $taskOrder = if ($Path -match '^Inference\.Providers\.(Google|Ollama)\.Models\.[^.]+$') { $taskLayout['*.ModelProfile'] } else { $taskLayout[$Path] }
        $taskKnown = @($taskOrder | Where-Object { $_ -is [string] -and $Value.Contains($_) })
        $taskOther = @($Value.Keys | Where-Object { $_ -notin $taskKnown } | Sort-Object -CaseSensitive)
        $taskOrdered = [ordered]@{}
        foreach ($taskKey in @($taskKnown) + @($taskOther)) {
            $taskNext = if ($Path) { "$Path.$taskKey" } else { $taskKey }
            $taskOrdered[$taskKey] = ConvertTo-NexusOrdered $Value[$taskKey] $taskNext
        }
        return $taskOrdered
    }
    if ($Value -is [array]) {
        $taskItems = @($Value | ForEach-Object { ConvertTo-NexusOrdered $_ $Path })
        return ,$taskItems
    }
    return $Value
}

function Save-NexusJson([string]$Path, [System.Collections.IDictionary]$Value) {
    $taskOrdered = ConvertTo-NexusOrdered $Value
    $taskText = ($taskOrdered | ConvertTo-Json -Depth 40) + "`n"
    $taskAbsolute = [IO.Path]::GetFullPath($Path)
    $taskTemporary = $taskAbsolute + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($taskTemporary, '')
        if (Test-Path -LiteralPath $taskAbsolute) { Set-Acl -LiteralPath $taskTemporary -AclObject (Get-Acl -LiteralPath $taskAbsolute) }
        [IO.File]::WriteAllText($taskTemporary, $taskText, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $taskAbsolute) { [IO.File]::Replace($taskTemporary, $taskAbsolute, [NullString]::Value) }
        else { [IO.File]::Move($taskTemporary, $taskAbsolute) }
    } finally {
        if (Test-Path -LiteralPath $taskTemporary) { Remove-Item -LiteralPath $taskTemporary }
        $taskText = $null
    }
}

function Get-NexusSecretValues([System.Collections.IDictionary]$Value, [switch]$IncludeUser, [string]$Path = '') {
    foreach ($taskKey in $Value.Keys) {
        $taskNext = if ($Path) { "$Path.$taskKey" } else { $taskKey }
        if ($Value[$taskKey] -is [System.Collections.IDictionary]) { Get-NexusSecretValues $Value[$taskKey] -IncludeUser:$IncludeUser -Path $taskNext }
        elseif ($Value[$taskKey] -is [string] -and $Value[$taskKey].Length -gt 0 -and ($taskKey -match '^(Password|DnPass|ApiKey|GoogleApiKey)$' -or $Path -eq 'ConnectionStrings' -or ($IncludeUser -and $taskKey -eq 'User'))) { $Value[$taskKey] }
    }
}
