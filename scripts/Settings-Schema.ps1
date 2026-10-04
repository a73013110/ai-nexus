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

function ConvertTo-NexusV2([System.Collections.IDictionary]$Value) {
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
    foreach ($taskKey in @('Gdweb','Meiho')) { Move-NexusSetting $Value "Integrations.$taskKey" "Integrations.Sources.$taskKey" }
    if ($Value.Database) { $Value.Database.Remove('AllowUntrustedCertificateInProduction') | Out-Null }
    $Value.ConfigurationVersion = 2
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
