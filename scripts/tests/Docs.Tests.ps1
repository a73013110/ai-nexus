# Documentation rules from .claude/rules/docs.md: links and repository paths in Markdown resolve, and docs/ files stay short and
# follow the naming table.
BeforeDiscovery {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $markdown = @(git -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard -- '*.md' |
        Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf } |
        ForEach-Object { @{ Name = $_; Path = Join-Path $root $_ } })
    $docs = @($markdown | Where-Object { $_.Name -like 'docs/*' })
}

BeforeAll {
    Set-StrictMode -Version Latest
    $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

    # Lines outside fenced code blocks; links and paths inside examples are not references.
    function Get-ProseLines([string]$Path) {
        $fenced = $false
        foreach ($line in [IO.File]::ReadAllLines($Path)) {
            if ($line -match '^\s*(```|~~~)') { $fenced = !$fenced; continue }
            if (!$fenced) { $line }
        }
    }

    # GitHub heading anchors: lower case, punctuation removed except - and _, spaces become -, repeats get -1, -2.
    function Get-Anchors([string]$Path) {
        $seen = @{}
        foreach ($line in Get-ProseLines $Path) {
            if ($line -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }
            $slug = [regex]::Replace($Matches[1].Trim().ToLowerInvariant(), '[^\p{L}\p{Nd}\p{Mn}\p{Pc} -]', '').Replace(' ', '-')
            $count = $seen[$slug] ?? 0
            $seen[$slug] = $count + 1
            if ($count) { "$slug-$count" } else { $slug }
        }
    }
}

Describe '<Name>' -ForEach $markdown {
    It 'has relative links that resolve' {
        $directory = Split-Path -Parent $Path
        $broken = foreach ($line in Get-ProseLines $Path) {
            foreach ($match in [regex]::Matches($line, '\]\(([^)\s]+)\)')) {
                $target = $match.Groups[1].Value
                if ($target -match '^[a-z][a-z0-9+.-]*:') { continue }
                $file, $anchor = $target -split '#', 2
                $resolved = if ($file) { Join-Path $directory ([Uri]::UnescapeDataString($file)) } else { $Path }
                if (!(Test-Path -LiteralPath $resolved)) { $target; continue }
                if ($anchor -and $resolved -like '*.md' -and [Uri]::UnescapeDataString($anchor) -notin @(Get-Anchors $resolved)) { $target }
            }
        }
        $broken | Should -BeNullOrEmpty
    }

    It 'names repository paths that exist' {
        $missing = foreach ($line in Get-ProseLines $Path) {
            foreach ($match in [regex]::Matches($line, '`((?:backend|frontend|scripts|docs|deploy|contracts|tooling|tests|\.github)/[^`\s]+)`')) {
                $candidate = $match.Groups[1].Value
                # Placeholders and patterns (<Module>, *.spec.ts, {id}, …) are not concrete paths.
                if ($candidate -match '[<>*{}…]') { continue }
                if (!(Test-Path -LiteralPath (Join-Path $root $candidate.TrimEnd('/')))) { $candidate }
            }
        }
        $missing | Should -BeNullOrEmpty
    }
}

Describe '<Name>' -ForEach $docs {
    It 'follows the docs naming rule' {
        $leaf = Split-Path -Leaf $Path
        if ($Name -like 'docs/decisions/*') { $leaf | Should -MatchExactly '^\d{4}(-[a-z0-9]+)+\.md$' }
        else { $leaf | Should -MatchExactly '^[a-z0-9]+(-[a-z0-9]+)*\.md$' }
    }

    It 'stays within about 8 KB' {
        (Get-Item -LiteralPath $Path).Length | Should -BeLessOrEqual 8192
    }
}
