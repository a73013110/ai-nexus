# Conventions every PowerShell file in scripts/, tooling/ and frontend/e2e follows; see the comment-based help of each entry script.
BeforeDiscovery {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $entries = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts'), (Join-Path $root 'frontend/e2e') -Filter '*.ps1' | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
    $tools = @(Get-ChildItem -LiteralPath (Join-Path $root 'tooling') -Filter '*.ps1' -Recurse | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
    $all = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts'), (Join-Path $root 'tooling'), (Join-Path $root 'frontend/e2e') -Include '*.ps1', '*.psm1' -Recurse | ForEach-Object { @{ Name = $_.Name; Path = $_.FullName } })
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
        $names | Should -Be (@(
            'Build', 'Configure-Local', 'Deploy-Database', 'Export-Contracts', 'Export-Database', 'Initialize-Database', 'Publish-IIS', 'Restore', 'Start-Dev',
            'Start-Local', 'Test-Environment', 'Test-Repository', 'Verify', 'Verify-Database', 'Verify-IIS') | Sort-Object)
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

Describe 'Host commands called by scripts' {
    BeforeAll {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $hostCommand = [IO.File]::ReadAllText((Join-Path $root 'backend/src/AiNexus.Host/Commands/HostCommand.cs'))
        $calls = foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1') {
            $ast = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$null)
            $ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-RepoHostCommand' }, $true) |
                ForEach-Object { @{ Script = $file.Name; Words = @($_.CommandElements[1].Elements | Where-Object { $_ -is [Management.Automation.Language.StringConstantExpressionAst] } | ForEach-Object Value) } }
        }
    }

    It 'name commands and options the host defines' {
        $calls | Should -Not -BeNullOrEmpty
        foreach ($call in $calls) {
            $name = @($call.Words | Where-Object { $_ -notlike '--*' } | Select-Object -First 2) -join ' '
            $hostCommand | Should -Match ([regex]::Escape("new(`"$name`"")) -Because "$($call.Script) 呼叫 $name"
            foreach ($option in @($call.Words | Where-Object { $_ -like '--*' })) {
                $hostCommand | Should -Match ([regex]::Escape("`"$option`"")) -Because "$($call.Script) 傳 $option 給 $name"
            }
        }
    }

    It 'ships the dacpac, the release scripts and the database scripts with every IIS package' {
        @($calls | Where-Object { $_.Script -eq 'Publish-IIS.ps1' }) | Should -BeNullOrEmpty
        $text = [IO.File]::ReadAllText((Join-Path $root 'scripts/Publish-IIS.ps1'))
        $text | Should -Match 'dotnet build database/App'
        $text | Should -Match 'App\.dacpac'
        foreach ($name in 'Deploy-Database\.ps1', 'Verify-Database\.ps1', "'FullText', 'Seed', 'Data'") { $text | Should -Match $name }
        $text | Should -Not -Match 'migrations'
    }
}

Describe 'Verify-IIS.ps1' {
    It 'runs on its own, because it is copied into the release package' {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        [IO.File]::ReadAllText((Join-Path $root 'scripts/Verify-IIS.ps1')) | Should -Not -Match 'Import-Module|\$PSScriptRoot'
    }
}

Describe 'Verify-Database.ps1' {
    BeforeAll {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $script = Join-Path $root 'scripts/Verify-Database.ps1'
    }

    It 'runs on its own, because it is copied into the release package' {
        [IO.File]::ReadAllText($script) | Should -Not -Match 'Import-Module|\$PSScriptRoot'
    }

    It 'names the SqlPackage version the tool manifest pins' {
        $manifest = [IO.File]::ReadAllText((Join-Path $root '.config/dotnet-tools.json')) | ConvertFrom-Json -AsHashtable
        $version = $manifest['tools']['microsoft.sqlpackage']['version']
        [IO.File]::ReadAllText($script) | Should -Match ([regex]::Escape("`$sqlPackageVersion = '$version'"))
    }

    It 'ignores column order and environment differences but reports objects missing from either side' {
        $text = [IO.File]::ReadAllText($script)
        foreach ($property in 'IgnoreColumnOrder=True', 'DropObjectsNotInSource=True', 'IgnorePermissions=True', 'ScriptDatabaseOptions=False') { $text | Should -Match ([regex]::Escape($property)) }
        foreach ($type in "'Users'", "'Logins'", "'Permissions'", "'Filegroups'", "'FullTextCatalogs'") { $text | Should -Match ([regex]::Escape($type)) }
    }

    It 'fails with installation steps instead of skipping when SqlPackage is missing' {
        $dacpac = Join-Path $TestDrive 'App.dacpac'
        Set-Content -LiteralPath $dacpac -Value 'x'
        { & $script -Server 'sql.invalid' -DacpacPath $dacpac -SqlPackagePath (Join-Path $TestDrive 'missing/SqlPackage.exe') } | Should -Throw '*SqlPackagePath*'
        $output = pwsh -NoProfile -Command "`$env:PATH = ''; `$env:ProgramFiles = '$TestDrive'; & '$script' -Server sql.invalid -DacpacPath '$dacpac'" 2>&1 | Out-String
        $LASTEXITCODE | Should -Not -Be 0
        $output | Should -Match 'microsoft\.sqlpackage'
        $output | Should -Match 'sqlpackage-download'
    }

    It 'fails clearly without the expected dacpac' {
        { & $script -Server 'sql.invalid' -DacpacPath (Join-Path $TestDrive 'none.dacpac') } | Should -Throw '*App.dacpac*'
    }
}

Describe 'Deploy-Database.ps1' {
    BeforeAll {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $script = Join-Path $root 'scripts/Deploy-Database.ps1'
        $text = [IO.File]::ReadAllText($script)
        # 腳本頂層會連線，只載入要測的函式。
        $ast = [Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$null)
        foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -in 'Get-SqlBatch' }, $true)) {
            . ([scriptblock]::Create($function.Extent.Text))
        }
    }

    It 'runs on its own, because it is copied into the release package' {
        $text | Should -Not -Match 'Import-Module'
    }

    It 'names the SqlPackage version the tool manifest pins' {
        $manifest = [IO.File]::ReadAllText((Join-Path $root '.config/dotnet-tools.json')) | ConvertFrom-Json -AsHashtable
        $text | Should -Match ([regex]::Escape("`$sqlPackageVersion = '$($manifest['tools']['microsoft.sqlpackage']['version'])'"))
    }

    It 'uses the release parameters of ADR 0010' {
        foreach ($property in 'BlockOnPossibleDataLoss=$(!$AllowDataLoss)', 'DropObjectsNotInSource=False', 'GenerateSmartDefaults=False', 'IgnoreColumnOrder=True', 'IgnorePermissions=True', 'ScriptDatabaseOptions=$NewDatabase') {
            $text | Should -Match ([regex]::Escape($property))
        }
        foreach ($type in "'Users'", "'Logins'", "'Permissions'", "'Filegroups'") { $text | Should -Match ([regex]::Escape($type)) }
    }

    It 'runs the hand-written scripts in release order' {
        $post = $text.Substring($text.IndexOf('function Invoke-Post'))
        $post.IndexOf('FullText/Catalogs') | Should -BeLessThan $post.IndexOf('FullText/Indexes')
        $post.IndexOf('FullText/Indexes') | Should -BeLessThan $post.IndexOf("Get-DataScript 'post'")
        $post.IndexOf("Get-DataScript 'post'") | Should -BeLessThan $post.IndexOf("'Seed'")
    }

    It 'splits batches on GO and refuses SQLCMD directives' {
        $file = Join-Path $TestDrive 'batches.sql'
        [IO.File]::WriteAllText($file, "SELECT 1;`nGO`n`n  go  `nSELECT N'GO';`n")
        Get-SqlBatch $file | ForEach-Object Trim | Should -Be @('SELECT 1;', "SELECT N'GO';")
        [IO.File]::WriteAllText($file, ":setvar Name x`nSELECT 1;")
        { Get-SqlBatch $file } | Should -Throw '*SQLCMD*'
    }

    It 'fails clearly without the dacpac' {
        { & $script -Server 'sql.invalid' -Path (Join-Path $TestDrive 'empty') } | Should -Throw '*App.dacpac*'
    }

    It 'fails with installation steps before connecting when SqlPackage is missing' {
        $folder = Join-Path $TestDrive 'package'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $folder 'App.dacpac') -Value 'x'
        $output = pwsh -NoProfile -Command "`$env:PATH = ''; `$env:ProgramFiles = '$TestDrive'; & '$script' -Server sql.invalid -Path '$folder'" 2>&1 | Out-String
        $LASTEXITCODE | Should -Not -Be 0
        $output | Should -Match 'microsoft\.sqlpackage'
        $output | Should -Match 'sqlpackage-download'
    }
}
