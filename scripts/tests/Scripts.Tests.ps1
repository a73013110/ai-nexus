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
            'Build', 'Configure-Local', 'Export-Contracts', 'Export-Database', 'Initialize-Database', 'Publish-IIS', 'Restore', 'Start-Dev',
            'Show-Scripts', 'Start-Local', 'Test-Environment', 'Test-Repository', 'Verify') | Sort-Object)
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

    # 變數不分大小寫：在函式裡指派 $credential 會蓋掉外層的 -Credential，靜默改用 Windows 驗證。
    It 'never assigns a variable that differs from a visible parameter only by case' {
        $ast = (Get-Ast $Path).Ast
        $shadows = foreach ($assignment in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] }, $true)) {
            $name = $assignment.Left.VariablePath.UserPath
            for ($scope = $assignment.Parent; $scope; $scope = $scope.Parent) {
                $parameters = if ($scope -is [Management.Automation.Language.ScriptBlockAst] -and $scope.ParamBlock) { $scope.ParamBlock.Parameters }
                    elseif ($scope -is [Management.Automation.Language.FunctionDefinitionAst]) { $scope.Parameters }
                foreach ($parameter in @($parameters | Where-Object { $_ })) {
                    $declared = $parameter.Name.VariablePath.UserPath
                    if ($declared -ieq $name -and $declared -cne $name) { "第 $($assignment.Extent.StartLineNumber) 行 `$$name 與參數 `$$declared" }
                }
            }
        }
        @($shadows) | Should -BeNullOrEmpty
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

    It 'ships the dacpac and the numbered release SQL, but no script the production host would have to run' {
        @($calls | Where-Object { $_.Script -eq 'Publish-IIS.ps1' }) | Should -BeNullOrEmpty
        $text = [IO.File]::ReadAllText((Join-Path $root 'scripts/Publish-IIS.ps1'))
        $text | Should -Match 'dotnet build database/App'
        $text | Should -Match 'App\.dacpac'
        $text | Should -Match 'Get-RepoReleaseScript'
        $text | Should -Not -Match 'Copy-Item .*\.ps1|migrations'
    }
}

Describe 'Show-Scripts.ps1' {
    BeforeAll {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $script = Join-Path $root 'scripts/Show-Scripts.ps1'
        Import-Module (Join-Path $root 'scripts/AiNexus') -Force
        # 腳本本體會進入互動選單，只載入函式，並以無色的樣式準備它們用到的變數。
        $ast = [Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$null)
        foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
            . ([scriptblock]::Create($function.Extent.Text))
        }
        $catalog = @(Get-RepoScriptCatalog)
        $categories = Get-RepoScriptCategory
        $historyPath = Join-Path $TestDrive 'script-history.json'
        $nameWidth = ($catalog | ForEach-Object { Get-RepoDisplayWidth $_.Name } | Measure-Object -Maximum).Maximum
        $theme = @{ Reset = ''; Bold = ''; Dim = ''; Reverse = ''; Accent = ''; Warn = ''; Good = ''; Error = '' }
        $settingsPath = Join-Path $TestDrive 'appsettings.Local.json'
        $secretsPath = Join-Path $TestDrive 'appsettings.Secrets.json'
        function New-State([switch]$SavedLogin) {
            Save-RepoJson $settingsPath ([ordered]@{ Databases = [ordered]@{ App = [ordered]@{ Server = '192.0.2.5'; Name = '開發庫' } } })
            Save-RepoJson $secretsPath ([ordered]@{ Databases = [ordered]@{ App = [ordered]@{ User = $(if ($SavedLogin) { 'sa' } else { '' }); Password = $(if ($SavedLogin) { 'p@ss' } else { '' }) } } })
            @{ Screen = 'List'; Filter = ''; Rows = $null; Selected = -1; Top = 0; Entry = $null; Values = @{}; Field = 0; FieldTop = 0; Example = -1; Run = $null; Command = ''
                Back = 'List'; Message = ''; MessageStyle = ''; Shared = (Get-RepoSharedSetting -Path $settingsPath -SecretsPath $secretsPath)
                SettingsPath = $settingsPath; SecretsPath = $secretsPath; SettingsValues = $null; SettingsField = 0 }
        }
        function Send-Key($State, [string]$Key, [string]$Text = '') {
            $press = [pscustomobject]@{ Key = $Key; Ctrl = $false; Text = $Text }
            switch ($State.Screen) {
                'List' { Update-ListState $State $press }
                'Parameters' { Update-ParameterState $State $press }
                'Confirm' { Update-ConfirmState $State $press }
                'Settings' { Update-SettingsState $State $press }
            }
        }
        function Open-Script($State, [string]$Name) {
            Set-Filter $State $Name
            $State.Selected = @(for ($i = 0; $i -lt $State.Rows.Count; $i++) { if ($State.Rows[$i]['Item'] -and $State.Rows[$i]['Item'].Name -eq $Name) { $i } })[0]
            Send-Key $State 'Enter' | Out-Null
        }
        function Send-Text($State, [string]$Text) { foreach ($character in $Text.ToCharArray()) { Send-Key $State 'A' ([string]$character) | Out-Null } }
    }

    It 'fits every screen line in <Width> columns, counting Chinese as two' -ForEach @(@{ Width = 41 }, @{ Width = 60 }, @{ Width = 119 }) {
        $state = New-State
        Set-Filter $state ''
        $frames = [Collections.Generic.List[string]]::new()
        $frames.AddRange((Get-ListFrame $state $Width 24))
        Open-Script $state 'Export-Database'
        Send-Key $state 'Tab' | Out-Null
        $frames.AddRange((Get-ParameterFrame $state $Width 24))
        Send-Key $state 'Enter' | Out-Null
        $state.Screen | Should -Be 'Confirm'
        $frames.AddRange((Get-ConfirmFrame $state $Width))
        Send-Key $state 'Escape' | Out-Null
        Send-Key $state 'Escape' | Out-Null
        Send-Key $state 'F2' | Out-Null
        $frames.AddRange((Get-SettingsFrame $state $Width))
        foreach ($line in $frames) { Get-RepoDisplayWidth $line | Should -BeLessOrEqual $Width -Because $line }
    }

    It 'filters by Chinese text typed one character at a time and deletes whole characters' {
        $state = New-State
        Set-Filter $state ''
        Send-Text $state '資料庫'
        @($state.Rows | Where-Object { $_['Item'] } | ForEach-Object { $_['Item'].Name }) | Should -Be @('Export-Contracts', 'Export-Database', 'Initialize-Database', 'Publish-IIS')
        Send-Key $state 'Backspace' | Out-Null
        $state.Filter | Should -Be '資料'
    }

    It 'turns fullwidth digits into numbers and accepts a fullwidth comma between list items' {
        $state = New-State
        Open-Script $state 'Start-Dev'
        while (@($state.Entry.Parameters)[$state.Field].Name -ne 'BackendPort') { Send-Key $state 'DownArrow' | Out-Null }
        Send-Text $state '５０８１'
        (ConvertTo-RunParameters $state.Entry $state.Values)['BackendPort'] | Should -Be 5081
        $export = @($catalog | Where-Object Name -EQ 'Export-Database')[0]
        $values = New-EditorValues $export ([ordered]@{ Server = 'sql'; ExcludeTable = 'dbo.暫存，dbo.Backup, ' })
        (ConvertTo-RunParameters $export $values)['ExcludeTable'] | Should -Be @('dbo.暫存', 'dbo.Backup')
    }

    It 'fills the shared server, database and saved login, and shows the target on every screen' {
        $state = New-State -SavedLogin
        Set-Filter $state ''
        (Get-ListFrame $state 100 24) -join "`n" | Should -Match '共用設定：資料庫 192\.0\.2\.5 / 開發庫，帳號 sa（已儲存）'
        Open-Script $state 'Export-Database'
        $state.Values['Server'] | Should -Be '192.0.2.5'
        $state.Values['Database'] | Should -Be '開發庫'
        $state.Values['Credential'] | Should -Be 'Saved'
        (Get-ParameterFrame $state 100 30) -join "`n" | Should -Match '操作對象：192\.0\.2\.5 / 開發庫（共用設定，帳號 sa（已儲存））'
        Send-Key $state 'Enter' | Out-Null
        $state.Command | Should -Match '-Credential \(Get-Credential\)'
        $state.Command | Should -Not -Match 'p@ss'
        (Get-ConfirmFrame $state 100) -join "`n" | Should -Match '操作對象：192\.0\.2\.5 / 開發庫'
    }

    It 'warns when the target differs from the shared settings and keeps the shared target when applying an example' {
        $state = New-State
        Open-Script $state 'Export-Database'
        $state.Values['Credential'] | Should -Be ''
        Send-Key $state 'Tab' | Out-Null
        $state.Values['Server'] | Should -Be '192.0.2.5'
        $state.Values['Server'] = 'sql-other'
        (Get-Target $state $state.Entry $state.Values).Text | Should -Match 'sql-other / 開發庫（與共用設定不同）'
    }

    It 'leaves -Server empty for a script that reads the settings itself, and still shows its target' {
        $state = New-State
        Open-Script $state 'Initialize-Database'
        $state.Values['Server'] | Should -BeNullOrEmpty
        (Get-Target $state $state.Entry $state.Values).Text | Should -Match '192\.0\.2\.5 / 開發庫（共用設定）'
    }

    It 'saves the shared settings and login from the settings screen, masking the password' {
        $state = New-State
        Set-Filter $state ''
        Send-Key $state 'F2' | Out-Null
        Send-Key $state 'Delete' | Out-Null
        Send-Text $state 'sql-new'
        foreach ($step in 1..3) { Send-Key $state 'DownArrow' | Out-Null }
        Send-Text $state 'reader'
        Send-Key $state 'DownArrow' | Out-Null
        Send-Text $state 's3cret'
        $frame = (Get-SettingsFrame $state 100) -join "`n"
        $frame | Should -Not -Match 's3cret'
        $frame | Should -Match '\*{6}'
        Send-Key $state 'Enter' | Out-Null
        $state.Screen | Should -Be 'List'
        $state.Shared.Values['Server'] | Should -Be 'sql-new'
        $state.Shared.SavedUser | Should -Be 'reader'
        (Get-RepoSavedCredential -SecretsPath $secretsPath).GetNetworkCredential().Password | Should -Be 's3cret'
        $state.SettingsValues['Password'] | Should -BeNullOrEmpty
    }

    It 'asks for mandatory parameters before confirming' {
        $state = New-State
        Open-Script $state 'Export-Database'
        Send-Key $state 'Delete' | Out-Null
        Send-Key $state 'Enter' | Out-Null
        $state.Screen | Should -Be 'Parameters'
        $state.Message | Should -Match '-Server'
    }

    It 'prints a plain list grouped by category when not interactive' {
        $text = pwsh -NoProfile -File $script -List | Out-String
        $LASTEXITCODE | Should -Be 0
        $text | Should -Match '開發（日常啟動與預覽）'
        $text | Should -Match '發布（產生 IIS 發布套件）'
        foreach ($item in $catalog) { $text | Should -Match ([regex]::Escape($item.Name)) }
    }
}

Describe 'Initialize-Database.ps1' {
    It 'creates the database with the release parameters of ADR 0010 and the same hand-written SQL as the IIS package' {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $text = [IO.File]::ReadAllText((Join-Path $root 'scripts/Initialize-Database.ps1'))
        foreach ($property in 'BlockOnPossibleDataLoss=True', 'GenerateSmartDefaults=False', 'IgnorePermissions=True', 'ScriptDatabaseOptions=True') {
            $text | Should -Match ([regex]::Escape($property))
        }
        foreach ($type in "'Users'", "'Logins'", "'Permissions'", "'Filegroups'", "'FullTextCatalogs'") { $text | Should -Match ([regex]::Escape($type)) }
        $text | Should -Match 'Get-RepoReleaseScript'
    }
}
