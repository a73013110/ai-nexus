#requires -Version 7.4
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Settings-Schema.ps1')
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskFixture = Join-Path $taskRoot ('artifacts/settings-test/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null
$taskGeneralPath = Join-Path $taskFixture 'appsettings.Production.json'
$taskSecretsPath = Join-Path $taskFixture 'appsettings.Secrets.json'
$taskGeneral = @{ AllowedHosts = 'fixture.test'; Database = @{ Server = 'fixture'; Password = 'fixture-only-password'; TrustServerCertificate = $true }; Inference = @{ Provider = 'ollama'; Models = @(@{ Id = 'qwen3:8b'; ContextTokens = 8192 }); DefaultModelId = 'qwen3:8b'; BaseUrl = 'http://fixture.test:11434/'; SystemPrompt = 'fixture instruction' }; CustomExtension = @{ Preserve = 42 }; Knowledge = @{ EmbeddingProvider = 'none'; Dimensions = 768 } }
$taskPrivate = @{ AdAuthentication = @{ DnPass = 'fixture-only-ad-password' }; Inference = @{ GoogleApiKey = 'fixture-only-google-key' } }
Save-NexusJson $taskGeneralPath $taskGeneral
Save-NexusJson $taskSecretsPath $taskPrivate
function Get-TestAcl([string]$Path) {
    $taskAccess = Get-Acl -LiteralPath $Path
    return (@{ Owner = $taskAccess.Owner; Group = $taskAccess.Group; Protected = $taskAccess.AreAccessRulesProtected; Rules = @($taskAccess.Access | Select-Object IdentityReference,FileSystemRights,AccessControlType,IsInherited,InheritanceFlags,PropagationFlags) } | ConvertTo-Json -Depth 6 -Compress)
}
$taskAcl = Get-TestAcl $taskSecretsPath
& (Join-Path $PSScriptRoot 'Migrate-Settings.ps1') -SettingsPath $taskGeneralPath -SecretsPath $taskSecretsPath
$taskGeneral = [IO.File]::ReadAllText($taskGeneralPath) | ConvertFrom-Json -AsHashtable
$taskPrivate = [IO.File]::ReadAllText($taskSecretsPath) | ConvertFrom-Json -AsHashtable
if (@(Get-NexusSecretValues $taskGeneral -IncludeUser).Count -or $taskPrivate.Database.Password -cne 'fixture-only-password' -or $taskPrivate.Inference.Providers.Google.ApiKey -cne 'fixture-only-google-key') { throw 'Secret migration did not preserve separation.' }
if ($taskGeneral.Inference.Providers.Ollama.Models.default.Id -ne 'qwen3:8b' -or $taskGeneral.Prompts.DefaultSystemInstruction -ne 'fixture instruction' -or $taskGeneral.CustomExtension.Preserve -ne 42 -or $taskGeneral.Knowledge.Embedding.Provider -ne 'none') { throw 'Provider, prompt or custom-extension migration failed.' }
if ((Get-TestAcl $taskSecretsPath) -cne $taskAcl) { throw 'Secret file ACL was changed.' }
$taskBefore = [IO.File]::ReadAllText($taskGeneralPath) + [IO.File]::ReadAllText($taskSecretsPath)
& (Join-Path $PSScriptRoot 'Migrate-Settings.ps1') -SettingsPath $taskGeneralPath -SecretsPath $taskSecretsPath
if ($taskBefore -cne ([IO.File]::ReadAllText($taskGeneralPath) + [IO.File]::ReadAllText($taskSecretsPath))) { throw 'Migration is not idempotent.' }
foreach ($taskFile in @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'backend/src/AiNexus.Api') -Filter 'appsettings*.json' | Where-Object { $_.Name -eq 'appsettings.json' -or $_.Name -like '*.example.json' })) {
    $taskSettings = [IO.File]::ReadAllText($taskFile.FullName) | ConvertFrom-Json -AsHashtable
    if (@(Get-NexusSecretValues $taskSettings -IncludeUser).Count) { throw 'A public settings template contains a non-empty secret.' }
    $taskFormatted = ConvertTo-NexusOrdered $taskSettings
    if (($taskSettings | ConvertTo-Json -Depth 40 -Compress) -cne ($taskFormatted | ConvertTo-Json -Depth 40 -Compress)) { throw "Template has inconsistent field order: $($taskFile.Name)" }
}
foreach ($taskScript in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $taskErrors = $null; $taskTokens = $null
    [void][Management.Automation.Language.Parser]::ParseFile($taskScript.FullName, [ref]$taskTokens, [ref]$taskErrors)
    if ($taskErrors.Count) { throw "PowerShell syntax error in $($taskScript.Name)" }
}
Write-Output 'Settings checks passed: migration, isolated secrets, retained ACLs, idempotence, extensions, canonical template order and script syntax.'
