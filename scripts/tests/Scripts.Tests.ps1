# Conventions every PowerShell file in scripts/ and tooling/ follows; see the comment-based help of each entry script.
BeforeDiscovery {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $entries = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1' | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
    $tools = @(Get-ChildItem -LiteralPath (Join-Path $root 'tooling') -Filter '*.ps1' -Recurse | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
    $all = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts'), (Join-Path $root 'tooling') -Include '*.ps1', '*.psm1' -Recurse | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
}

BeforeAll {
    Set-StrictMode -Version Latest
    function Get-Ast([string]$Path) {
        $tokens = $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
        @{ Ast = $ast; Errors = $errors; Text = [IO.File]::ReadAllText($Path) }
    }
}

Describe 'scripts/ entry list' {
    It 'contains only the documented entry scripts' {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $names = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1').BaseName | Sort-Object
        # Start-BrowserTest moves to frontend/e2e together with the Playwright tests.
        $names | Should -Be (@(
            'Build', 'Configure-Local', 'Export-Contracts', 'Initialize-Database', 'Publish-IIS', 'Restore', 'Start-BrowserTest',
            'Start-Dev', 'Start-Local', 'Test-Environment', 'Test-Repository', 'Verify', 'Verify-IIS') | Sort-Object)
    }
}

Describe '<Name>' -ForEach $all {
    It 'parses' {
        (Get-Ast $Path).Errors | Should -BeNullOrEmpty
    }

    It 'has no hard-coded deployment path or $task-prefixed variable' {
        $text = (Get-Ast $Path).Text
        $text | Should -Not -Match ('Core' + 'Project')
        $text | Should -Not -Match '\$task[A-Z]'
    }
}

Describe '<Name>' -ForEach ($entries + $tools) {
    It 'is an advanced script in strict mode with help' {
        $parsed = Get-Ast $Path
        $parsed.Ast.ParamBlock.Attributes.TypeName.Name | Should -Contain 'CmdletBinding'
        $parsed.Text | Should -Match '(?m)^Set-StrictMode -Version Latest$'
        $parsed.Text | Should -Match '(?m)^#requires -Version 7\.4$'
        $help = $parsed.Ast.GetHelpContent()
        $help | Should -Not -BeNullOrEmpty
        $help.Synopsis | Should -Not -BeNullOrEmpty
        $help.Examples | Should -Not -BeNullOrEmpty
    }

    It 'documents every parameter' {
        $parsed = Get-Ast $Path
        $documented = @($parsed.Ast.GetHelpContent().Parameters.Keys)
        foreach ($parameter in @($parsed.Ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })) {
            $documented | Should -Contain $parameter.ToUpperInvariant() -Because "$Name 的 -$parameter 需要 .PARAMETER 說明"
        }
    }
}

Describe 'Verify-IIS.ps1' {
    It 'runs on its own, because it is copied into the release package' {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        [IO.File]::ReadAllText((Join-Path $root 'scripts/Verify-IIS.ps1')) | Should -Not -Match 'Import-Module|\$PSScriptRoot'
    }
}
