#requires -Version 7.4
# Checks staged files before a commit. Never prints a credential or file contents.
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $taskRoot
try {
    $taskFiles = @(git -c core.quotepath=false ls-files --cached)
    if ($LASTEXITCODE -ne 0 -or !$taskFiles.Count) { throw 'Stage the intended source files first.' }
    $taskForbidden = @($taskFiles | Where-Object { $_ -match '(^|/)(\.local|artifacts|node_modules|bin|obj)/|(^|/)appsettings\.(.*\.)?(Local|Secrets)\.json$|(^|/)\.env($|\.(?!example$))' })
    if ($taskForbidden.Count) { throw 'The Git index contains a local/generated/secret path. Unstage it before committing.' }
    $taskPrivatePath = Join-Path $taskRoot '.local/secrets/appsettings.Secrets.json'
    $taskPrivateValues = @()
    if (Test-Path -LiteralPath $taskPrivatePath) {
        try { $taskSecrets = [System.IO.File]::ReadAllText($taskPrivatePath) | ConvertFrom-Json -AsHashtable }
        catch { throw 'Local secrets JSON is invalid; repair it in your editor. Values were not printed.' }
        $taskPrivateValues = @($taskSecrets.Database.Password, $taskSecrets.AdAuthentication.DnPass, $taskSecrets.Inference.GoogleApiKey, $taskSecrets.ConnectionStrings.Nexus) | Where-Object { $_ -is [string] -and $_.Length -ge 6 }
    }
    foreach ($taskFile in $taskFiles) {
        $taskContent = (git show (':' + $taskFile)) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "Cannot inspect staged file: $taskFile" }
        if ($taskContent -match 'AIza[0-9A-Za-z_-]{35}|-{5}BEGIN (?:[A-Z]+ )?PRIVATE KEY-{5}') { throw "Possible live key in staged file: $taskFile" }
        foreach ($taskValue in $taskPrivateValues) {
            if ($taskContent.Contains($taskValue, [System.StringComparison]::Ordinal) -or $taskContent.Contains(($taskValue | ConvertTo-Json -Compress).Trim('"'), [System.StringComparison]::Ordinal)) { throw "Local credential found in staged file: $taskFile" }
        }
        if ($taskFile -match '(^|/)appsettings[^/]*\.json$') {
            try { $taskJson = $taskContent | ConvertFrom-Json -AsHashtable }
            catch { throw "Public configuration JSON is invalid: $taskFile" }
            if ($taskJson.Database.User -or $taskJson.Database.Password -or $taskJson.AdAuthentication.DnPass -or $taskJson.Inference.GoogleApiKey -or $taskJson.ConnectionStrings.Nexus) { throw "Public configuration must use empty secrets: $taskFile" }
        }
    }
    git -c core.whitespace=-blank-at-eof diff --cached --check -- . ':(exclude)backend/src/AiNexus.Api/Database/EDoc/**'
    if ($LASTEXITCODE -ne 0) { throw 'Staged source has whitespace errors.' }
    Write-Output "Staged repository checks passed: $($taskFiles.Count) files; local/generated paths excluded, public settings empty, no detected live keys or local password/key values."
    Write-Output 'This is a targeted check; review git diff --cached before publishing.'
} finally { $taskPrivateValues = $taskSecrets = $taskContent = $null; Pop-Location }
