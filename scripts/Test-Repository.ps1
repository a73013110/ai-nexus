#requires -Version 7.4
# Checks staged files before a commit. Never prints a credential or file contents.
param([switch]$WorkingTree)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Local-Settings.ps1')
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $taskRoot
try {
    $taskFiles = if ($WorkingTree) { @(git -c core.quotepath=false ls-files --cached --others --exclude-standard | Where-Object { Test-Path -LiteralPath (Join-Path $taskRoot $_) -PathType Leaf } | Select-Object -Unique) } else { @(git -c core.quotepath=false ls-files --cached) }
    if ($LASTEXITCODE -ne 0 -or !$taskFiles.Count) { throw 'Stage the intended source files first.' }
    # artifacts/ is the root build-output folder; feature source modules may also be named Artifacts.
    $taskForbidden = @($taskFiles | Where-Object { $_ -match '^artifacts/|(^|/)(\.local|node_modules|bin|obj)/|(^|/)appsettings\.(.*\.)?(Local|Secrets|Production|Staging)\.json$|(^|/)\.env($|\.(?!example$))' })
    if ($taskForbidden.Count) { throw 'The Git index contains a local/generated/secret path. Unstage it before committing.' }
    $taskPrivatePath = Join-Path $taskRoot '.local/secrets/appsettings.Secrets.json'
    $taskPrivateValues = @()
    if (Test-Path -LiteralPath $taskPrivatePath) {
        try { $taskSecrets = [System.IO.File]::ReadAllText($taskPrivatePath) | ConvertFrom-Json -AsHashtable }
        catch { throw 'Local secrets JSON is invalid; repair it in your editor. Values were not printed.' }
        $taskPrivateValues = @(Get-NexusSecretValues $taskSecrets) | Where-Object { $_.Length -ge 6 }
    }
    foreach ($taskFile in $taskFiles) {
        if ($WorkingTree) { $taskContent = [IO.File]::ReadAllText((Join-Path $taskRoot $taskFile)) }
        else { $taskContent = (git show (':' + $taskFile)) -join "`n"; if ($LASTEXITCODE -ne 0) { throw "Cannot inspect staged file: $taskFile" } }
        if ($taskContent -match 'AIza[0-9A-Za-z_-]{35}|-{5}BEGIN (?:[A-Z]+ )?PRIVATE KEY-{5}') { throw "Possible live key in staged file: $taskFile" }
        foreach ($taskValue in $taskPrivateValues) {
            if ($taskContent.Contains($taskValue, [System.StringComparison]::Ordinal) -or $taskContent.Contains(($taskValue | ConvertTo-Json -Compress).Trim('"'), [System.StringComparison]::Ordinal)) { throw "Local credential found in staged file: $taskFile" }
        }
        if ($taskFile -match '(^|/)appsettings[^/]*\.json$') {
            try { $taskJson = $taskContent | ConvertFrom-Json -AsHashtable }
            catch { throw "Public configuration JSON is invalid: $taskFile" }
            if (@(Get-NexusSecretValues $taskJson -IncludeUser).Count) { throw "Public configuration must use empty secrets: $taskFile" }
        }
    }
    if ($WorkingTree) { git -c core.whitespace=-blank-at-eof diff --check }
    else { git -c core.whitespace=-blank-at-eof diff --cached --check }
    if ($LASTEXITCODE -ne 0) { throw 'Staged source has whitespace errors.' }
    $taskScope = if ($WorkingTree) { 'Working-tree' } else { 'Staged' }
    Write-Output "$taskScope repository checks passed: $($taskFiles.Count) files; local/generated paths excluded, public settings empty, no detected live keys or local password/key values."
    Write-Output 'This is a targeted check; review git diff --cached before publishing.'
} finally { $taskPrivateValues = $taskSecrets = $taskContent = $null; Pop-Location }
