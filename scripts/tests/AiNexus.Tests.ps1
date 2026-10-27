BeforeAll {
    Set-StrictMode -Version Latest
    Import-Module (Join-Path $PSScriptRoot '../AiNexus') -Force
}

Describe 'Get-RepoRoot' {
    It 'is the repository root' {
        Test-Path -LiteralPath (Join-Path (Get-RepoRoot) 'backend/AiNexus.slnx') | Should -BeTrue
    }
}

Describe 'Resolve-RepoArtifactPath' {
    It 'resolves a path inside artifacts/ against the repository root' {
        Resolve-RepoArtifactPath 'artifacts/publish' | Should -Be ([IO.Path]::GetFullPath((Join-Path (Get-RepoRoot) 'artifacts/publish')))
    }

    It 'rejects <Path>' -ForEach @(@{ Path = 'artifacts' }, @{ Path = 'artifacts-old/publish' }, @{ Path = 'artifacts/../frontend' }, @{ Path = 'frontend/dist' }) {
        { Resolve-RepoArtifactPath $Path } | Should -Throw '*artifacts/*'
    }
}

Describe 'Get-RepoReleaseScript' {
    BeforeAll {
        $project = Join-Path $TestDrive 'App'
        foreach ($folder in 'Data/2026.11.0', 'Data/2026.12.0', 'FullText/Catalogs', 'FullText/Indexes', 'Seed') { New-Item -ItemType Directory -Path (Join-Path $project $folder) -Force | Out-Null }
        Set-Content -LiteralPath (Join-Path $project 'Data/2026.11.0/pre-move.sql') -Value 'SELECT 1;'
        Set-Content -LiteralPath (Join-Path $project 'Data/2026.11.0/post-fill.sql') -Value 'SELECT 2;'
        Set-Content -LiteralPath (Join-Path $project 'Data/2026.12.0/post-later.sql') -Value 'SELECT 3;'
        Set-Content -LiteralPath (Join-Path $project 'FullText/Catalogs/Search.sql') -Value "CREATE FULLTEXT CATALOG [Search's]`n    WITH ACCENT_SENSITIVITY = ON;`nGO"
        Set-Content -LiteralPath (Join-Path $project 'FullText/Indexes/knowledge.Chunks.sql') -Value "CREATE FULLTEXT INDEX ON [knowledge].[Chunks]`n    ([Text] LANGUAGE 1028)`n    KEY INDEX [IX_Chunks]`n    ON [Search's];`nGO"
        Set-Content -LiteralPath (Join-Path $project 'Seed/accesscontrol.sql') -Value 'SELECT 4;'
    }

    It 'names the scripts in run order and takes Data/ only from the given release' {
        @(Get-RepoReleaseScript $project -Release 2026.11.0).Name | Should -Be @(
            '1-01-pre-move.sql', '3-01-fulltext-Search.sql', '3-02-fulltext-knowledge.Chunks.sql', '4-01-post-fill.sql', '5-01-seed-accesscontrol.sql')
        @(Get-RepoReleaseScript $project).Name | Should -Be @('3-01-fulltext-Search.sql', '3-02-fulltext-knowledge.Chunks.sql', '5-01-seed-accesscontrol.sql')
    }

    It 'wraps full-text scripts so they skip without the component or when the object exists' {
        $scripts = @(Get-RepoReleaseScript $project)
        $catalog = $scripts[0].Text
        $catalog | Should -Match ([regex]::Escape("FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')"))
        $catalog | Should -Match ([regex]::Escape("sys.fulltext_catalogs WHERE name = N'Search''s'"))
        $catalog | Should -Match ([regex]::Escape("EXEC (N'CREATE FULLTEXT CATALOG [Search''s]"))
        $scripts[1].Text | Should -Match ([regex]::Escape("OBJECT_ID(N'[knowledge].[Chunks]')"))
        $scripts[2].Text | Should -Match '^-- 發版步驟 5：Seed/accesscontrol.sql'
    }

    It 'refuses SQLCMD directives, because the scripts run in plain SSMS' {
        $folder = Join-Path $TestDrive 'Sqlcmd'
        New-Item -ItemType Directory -Path (Join-Path $folder 'Seed') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $folder 'Seed/a.sql') -Value ":setvar Name x`nSELECT 1;"
        { Get-RepoReleaseScript $folder } | Should -Throw '*SQLCMD*'
    }
}

Describe 'New-RepoSqlConnection' {
    It 'uses the SQL login when a credential is given' {
        $credential = [pscredential]::new('demo', (ConvertTo-SecureString 'pw' -AsPlainText -Force))
        $connection = New-RepoSqlConnection -Server 'db.test' -Credential $credential -TrustServerCertificate
        try {
            $connection.Credential.UserId | Should -Be 'demo'
            $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
            $builder.IntegratedSecurity | Should -BeFalse
            $builder.InitialCatalog | Should -Be 'master'
            $builder.TrustServerCertificate | Should -BeTrue
        } finally { $connection.Dispose() }
    }

    It 'uses the Windows identity without a credential' {
        $connection = New-RepoSqlConnection -Server '(localdb)\MSSQLLocalDB' -Database '沙盒庫'
        try {
            [Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString).InitialCatalog | Should -Be '沙盒庫'
            $connection.Credential | Should -BeNullOrEmpty
            $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
            $builder.IntegratedSecurity | Should -BeTrue
            $builder.Encrypt | Should -BeFalse
        } finally { $connection.Dispose() }
    }
}

Describe 'Get-RepoSetting and Set-RepoSetting' {
    It 'creates missing sections and replaces non-section values' {
        $settings = [ordered]@{ Databases = 'invalid' }
        Set-RepoSetting $settings 'Databases.App.Server' 'sql'
        Set-RepoSetting $settings 'Inference.Providers.Google.ApiKey' 'key'
        Set-RepoSetting $settings 'AllowedHosts' 'host'
        Get-RepoSetting $settings 'Databases.App.Server' | Should -Be 'sql'
        Get-RepoSetting $settings 'Inference.Providers.Google.ApiKey' | Should -Be 'key'
        Get-RepoSetting $settings 'AllowedHosts' | Should -Be 'host'
    }

    It 'returns $null for a missing path in strict mode' {
        Set-StrictMode -Version Latest
        $settings = @{ Files = @{} }
        Get-RepoSetting $settings 'Files.StoragePath' | Should -BeNullOrEmpty
        Get-RepoSetting $settings 'Diagnostics.Directory' | Should -BeNullOrEmpty
        Get-RepoSetting $settings 'Files.StoragePath.Deeper' | Should -BeNullOrEmpty
    }
}

Describe 'Save-RepoJson' {
    It 'writes UTF-8 without BOM, replaces the file and leaves no temporary file' {
        $path = Join-Path $TestDrive 'settings.json'
        Save-RepoJson $path ([ordered]@{ Name = '舊' })
        Save-RepoJson $path ([ordered]@{ Name = '新'; Nested = [ordered]@{ Value = 1 } })
        $bytes = [IO.File]::ReadAllBytes($path)
        $bytes[0] | Should -Not -Be 0xEF
        (Read-RepoJson $path)['Nested']['Value'] | Should -Be 1
        (Read-RepoJson $path)['Name'] | Should -Be '新'
        @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.tmp').Count | Should -Be 0
    }

    It 'keeps the permissions of an existing secrets file' -Skip:$IsWindows {
        $path = Join-Path $TestDrive 'secrets.json'
        Save-RepoJson $path @{}
        Protect-RepoSecrets $path
        Save-RepoJson $path @{ Databases = @{ App = @{ Password = 'changed' } } }
        [IO.File]::GetUnixFileMode($path) | Should -Be ([IO.UnixFileMode]'UserRead, UserWrite')
    }
}

Describe 'Get-RepoSecretValues' {
    BeforeAll {
        $settings = [ordered]@{
            Databases = [ordered]@{ App = [ordered]@{ Server = 'sql'; User = 'login'; Password = 'secret-1' }; Gd = [ordered]@{ Server = 'sql'; Password = 'secret-0' } }
            Identity = [ordered]@{ ActiveDirectory = [ordered]@{ BindUser = 'reader'; BindPassword = 'secret-2' } }
            Inference = [ordered]@{ Providers = [ordered]@{ Google = [ordered]@{ ApiKey = 'secret-3' }; Ollama = [ordered]@{ ApiKey = '' } } }
        }
    }

    It 'returns non-empty passwords and keys' {
        @(Get-RepoSecretValues $settings) | Should -Be @('secret-1', 'secret-0', 'secret-2', 'secret-3')
    }

    It 'adds SQL logins with -IncludeUser' {
        @(Get-RepoSecretValues $settings -IncludeUser) | Should -Contain 'login'
    }
}

Describe 'Test-RepoForbiddenPath' {
    It 'forbids <Path>' -ForEach @(
        @{ Path = 'artifacts/publish/AiNexus.Host.dll' }, @{ Path = '.local/secrets/appsettings.Secrets.json' },
        @{ Path = 'frontend/node_modules/x/index.js' }, @{ Path = 'backend/src/AiNexus.Host/bin/Debug/a.dll' },
        @{ Path = 'backend/src/AiNexus.Host/appsettings.Local.json' }, @{ Path = 'config/appsettings.Production.json' },
        @{ Path = '.env' }, @{ Path = 'tooling/.env.local' }) {
        Test-RepoForbiddenPath $Path | Should -BeTrue
    }

    It 'allows <Path>' -ForEach @(
        @{ Path = 'backend/src/AiNexus.Features/Work/Artifacts/ExportArtifact.cs' }, @{ Path = 'backend/src/AiNexus.Host/appsettings.json' },
        @{ Path = 'backend/src/AiNexus.Host/appsettings.Local.example.json' }, @{ Path = 'backend/src/AiNexus.Host/appsettings.Production.example.json' },
        @{ Path = '.env.example' }, @{ Path = 'docs/development/local-folders.md' }) {
        Test-RepoForbiddenPath $Path | Should -BeFalse
    }
}

Describe 'Split-RepoSqlBatch and Join-RepoSqlBatch' {
    It 'splits on GO lines only and writes the SqlPackage layout with LF' {
        $text = "CREATE TABLE [a].[T] ([GOAL] INT);`r`n`r`n`r`nGO`r`nPRINT 'GO';`r`n go `r`n"
        $batches = Split-RepoSqlBatch $text
        $batches | Should -Be @('CREATE TABLE [a].[T] ([GOAL] INT);', "PRINT 'GO';")
        Join-RepoSqlBatch $batches | Should -Be "CREATE TABLE [a].[T] ([GOAL] INT);`n`n`nGO`n`nPRINT 'GO';`n`n`nGO`n`n"
    }

    It 'returns an array for a single batch' {
        , (Split-RepoSqlBatch "SELECT 1;`r`nGO") | Should -BeOfType [string[]]
    }
}

Describe 'Format-RepoSqlBatchOrder' {
    BeforeAll {
        function Get-Property([string]$Column, [string]$Value = 'x') {
            $level2 = if ($Column) { ", @level2type = N'COLUMN', @level2name = N'$Column'" } else { '' }
            "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'$Value', @level0type = N'SCHEMA', @level0name = N'k', @level1type = N'TABLE', @level1name = N'T'$level2;"
        }
    }

    It 'keeps the object first, then sorts indexes, then the table and column descriptions' {
        $batches = @('CREATE TABLE [k].[T] ([B] INT, [A] INT);', (Get-Property 'B'), 'CREATE INDEX [ix_T_B] ON [k].[T]([B]);', (Get-Property ''), (Get-Property 'A'), 'CREATE INDEX [ix_T_A] ON [k].[T]([A]);')
        $sorted = Format-RepoSqlBatchOrder $batches
        $sorted | Should -Be @('CREATE TABLE [k].[T] ([B] INT, [A] INT);', 'CREATE INDEX [ix_T_A] ON [k].[T]([A]);', 'CREATE INDEX [ix_T_B] ON [k].[T]([B]);', (Get-Property ''), (Get-Property 'A'), (Get-Property 'B'))
    }

    It 'gives the same order whatever order the database returned' {
        $one = @('CREATE TABLE [k].[T] ([Id] INT);', (Get-Property 'Id' '主鍵'), (Get-Property 'Name' '名稱'), (Get-Property '' '表'), (Get-Property 'name' '小寫'), 'CREATE INDEX [ix] ON [k].[T]([Id]);')
        $two = @($one[0], $one[5], $one[4], $one[2], $one[1], $one[3])
        $sorted = Format-RepoSqlBatchOrder $two
        $sorted | Should -Be (Format-RepoSqlBatchOrder $one)
    }
}

Describe 'Invoke-RepoExtractPostProcess' {
    BeforeAll {
        function Write-Sql([string]$Path, [string[]]$Batch) {
            $file = Join-Path $extract $Path
            New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
            # SqlPackage 的原始格式：UTF-8 BOM、CRLF。
            [IO.File]::WriteAllText($file, (($Batch | ForEach-Object { "$_`r`n`r`n`r`nGO`r`n`r`n" }) -join ''), [Text.UTF8Encoding]::new($true))
        }
        function Read-Sql([string]$Path) { [IO.File]::ReadAllText((Join-Path $extract $Path)) }
        function Get-Description([string]$Column) {
            "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'說明', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'$Column';"
        }
        function Write-Chunks([string[]]$Columns) {
            Write-Sql 'knowledge/Tables/Chunks.sql' (@(
                'CREATE TABLE [knowledge].[Chunks] ([Id] INT NOT NULL, [Text] NVARCHAR (4000) NOT NULL, CONSTRAINT [pk_Chunks] PRIMARY KEY ([Id]));',
                "CREATE FULLTEXT INDEX ON [knowledge].[Chunks]`r`n    ([Text] LANGUAGE 1028)`r`n    KEY INDEX [pk_Chunks]`r`n    ON [KnowledgeSearch];") + @($Columns | ForEach-Object { Get-Description $_ }))
        }
        $exclude = @('dbo.__EFMigrationsHistory', 'identity.Users_bak', 'identity.Users_bak2026*')
    }

    BeforeEach {
        $extract = Join-Path $TestDrive ([Guid]::NewGuid().ToString('N'))
        Write-Sql 'Storage/KnowledgeSearch.sql' "CREATE FULLTEXT CATALOG [KnowledgeSearch]`r`n    WITH ACCENT_SENSITIVITY = ON`r`n    AUTHORIZATION [dbo];"
        Write-Chunks 'Text', 'Id'
        Write-Sql 'identity/Tables/Users.sql' 'CREATE TABLE [identity].[Users] ([Id] INT NOT NULL);'
        Write-Sql 'identity/Tables/Users_bak.sql' 'CREATE TABLE [identity].[Users_bak] ([Id] INT NOT NULL);'
        Write-Sql 'identity/Tables/Users_bak20261018.sql' 'CREATE TABLE [identity].[Users_bak20261018] ([Id] INT NOT NULL);'
        Write-Sql 'dbo/Tables/__EFMigrationsHistory.sql' 'CREATE TABLE [dbo].[__EFMigrationsHistory] ([MigrationId] NVARCHAR (150) NOT NULL);'
        Write-Sql 'Security/identity.sql' "CREATE SCHEMA [identity]`r`n    AUTHORIZATION [dbo];"
        Write-Sql 'Security/app_user.sql' 'CREATE USER [app_user] FOR LOGIN [app_user];'
        Write-Sql 'Security/RoleMemberships.sql' 'ALTER ROLE [db_owner] ADD MEMBER [app_user];'
    }

    It 'moves full-text catalogs and indexes to FullText/ and removes the emptied files' {
        Invoke-RepoExtractPostProcess $extract $exclude | Out-Null
        Test-Path -LiteralPath (Join-Path $extract 'Storage') | Should -BeFalse
        Read-Sql 'FullText/Catalogs/KnowledgeSearch.sql' | Should -Be "CREATE FULLTEXT CATALOG [KnowledgeSearch]`n    WITH ACCENT_SENSITIVITY = ON`n    AUTHORIZATION [dbo];`n`n`nGO`n`n"
        Read-Sql 'FullText/Indexes/knowledge.Chunks.sql' | Should -Match '^CREATE FULLTEXT INDEX ON \[knowledge\]\.\[Chunks\]'
        Read-Sql 'knowledge/Tables/Chunks.sql' | Should -Not -Match 'FULLTEXT'
    }

    It 'sorts column descriptions and writes UTF-8 without BOM and LF' {
        Invoke-RepoExtractPostProcess $extract $exclude | Out-Null
        $text = Read-Sql 'knowledge/Tables/Chunks.sql'
        $text.IndexOf("N'Id'") | Should -BeLessThan $text.IndexOf("N'Text'")
        $text | Should -Not -Match "`r"
        foreach ($file in Get-ChildItem -LiteralPath $extract -Filter '*.sql' -Recurse) { [IO.File]::ReadAllBytes($file.FullName)[0] | Should -Not -Be 0xEF }
        Read-Sql 'identity/Tables/Users.sql' | Should -Be "CREATE TABLE [identity].[Users] ([Id] INT NOT NULL);`n`n`nGO`n`n"
    }

    It 'removes excluded tables, users and role memberships but keeps schemas, and reports them' {
        $removed = Invoke-RepoExtractPostProcess $extract $exclude
        $removed | Sort-Object | Should -Be (@('dbo/Tables/__EFMigrationsHistory.sql', 'identity/Tables/Users_bak.sql', 'identity/Tables/Users_bak20261018.sql', 'Security/app_user.sql', 'Security/RoleMemberships.sql') | Sort-Object)
        Test-Path -LiteralPath (Join-Path $extract 'dbo') | Should -BeFalse
        Read-Sql 'Security/identity.sql' | Should -Match 'CREATE SCHEMA \[identity\]'
        Test-Path -LiteralPath (Join-Path $extract 'identity/Tables/Users.sql') | Should -BeTrue
    }

    It 'gives byte-identical output for extracts that differ only in description order' {
        $first = Join-Path $TestDrive ([Guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $extract -Destination $first -Recurse
        Write-Chunks 'Id', 'Text'
        Invoke-RepoExtractPostProcess $first $exclude | Out-Null
        Invoke-RepoExtractPostProcess $extract $exclude | Out-Null
        $files = @(Get-ChildItem -LiteralPath $first -Filter '*.sql' -Recurse)
        $files.Count | Should -Be @(Get-ChildItem -LiteralPath $extract -Filter '*.sql' -Recurse).Count
        foreach ($file in $files) {
            $other = Join-Path $extract ([IO.Path]::GetRelativePath($first, $file.FullName))
            [IO.File]::ReadAllBytes($other) | Should -Be ([IO.File]::ReadAllBytes($file.FullName))
        }
    }

    It 'rejects a table pattern without a schema' {
        { Invoke-RepoExtractPostProcess $extract @('*_bak') } | Should -Throw '*schema.name*'
    }

    It 'rejects a schema named like a hand-written folder' {
        Write-Sql 'Seed/Tables/Values.sql' 'CREATE TABLE [Seed].[Values] ([Id] INT NOT NULL);'
        { Invoke-RepoExtractPostProcess $extract $exclude } | Should -Throw '*Seed*'
    }
}

Describe 'Read-RepoExclusionList' {
    It 'reads one pattern per line, dropping comments and blank lines' {
        $file = Join-Path $TestDrive 'export-exclusions.txt'
        [IO.File]::WriteAllText($file, "# 說明`n`ndbo.__EFMigrationsHistory   # migration 殘留`r`n  sales.Orders_bak*  `n")
        Read-RepoExclusionList $file | Should -Be @('dbo.__EFMigrationsHistory', 'sales.Orders_bak*')
    }

    It 'excludes nothing when the file is missing' {
        @(Read-RepoExclusionList (Join-Path $TestDrive 'missing.txt')).Count | Should -Be 0
    }
}

Describe 'Get-RepoUnusedExclusion' {
    It 'returns the patterns that removed no table' {
        $removed = @('identity/Tables/Users_bak.sql', 'Security/app_user.sql')
        Get-RepoUnusedExclusion @('identity.Users_bak', 'identity.*_old', 'Security.app_user') $removed | Should -Be @('identity.*_old', 'Security.app_user')
    }
}

Describe 'Update-RepoDatabaseProject' {
    It 'replaces exported files and keeps the project file, hand-written folders and build output' {
        $project = Join-Path $TestDrive 'App'
        $extract = Join-Path $TestDrive 'extract'
        $files = @{
            'App.sqlproj' = '<Project />'; 'old/Tables/Gone.sql' = 'old'; 'identity/Tables/Users.sql' = 'old'; 'FullText/Catalogs/Old.sql' = 'old'
            'FullText/README.sql' = 'hand'; 'Seed/identity.Roles.sql' = 'hand'; 'Data/1.2.0/pre-move.sql' = 'hand'; 'bin/Debug/App.sql' = 'build'
        }
        foreach ($name in $files.Keys) {
            New-Item -ItemType Directory -Path (Split-Path (Join-Path $project $name)) -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $project $name) -Value $files[$name] -NoNewline
        }
        foreach ($name in 'identity/Tables/Users.sql', 'FullText/Catalogs/KnowledgeSearch.sql') {
            New-Item -ItemType Directory -Path (Split-Path (Join-Path $extract $name)) -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $extract $name) -Value 'new' -NoNewline
        }
        Update-RepoDatabaseProject $extract $project
        $left = @(Get-ChildItem -LiteralPath $project -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($project, $_.FullName).Replace('\', '/') }) | Sort-Object
        $left | Should -Be (@('App.sqlproj', 'bin/Debug/App.sql', 'Data/1.2.0/pre-move.sql', 'FullText/Catalogs/KnowledgeSearch.sql', 'FullText/README.sql', 'identity/Tables/Users.sql', 'Seed/identity.Roles.sql') | Sort-Object)
        Get-Content -LiteralPath (Join-Path $project 'identity/Tables/Users.sql') -Raw | Should -Be 'new'
        Test-Path -LiteralPath (Join-Path $project 'old') | Should -BeFalse
    }
}

Describe 'Get-RepoSqlProjectSetting and Compare-RepoDatabaseSetting' {
    BeforeAll {
        $projectFile = Join-Path $TestDrive 'App.sqlproj'
        Set-Content -LiteralPath $projectFile -Value @'
<Project Sdk="Microsoft.Build.Sql/2.3.0">
  <PropertyGroup>
    <DefaultCollation>Chinese_Taiwan_Stroke_CI_AS</DefaultCollation>
    <CompatibilityMode>170</CompatibilityMode>
  </PropertyGroup>
</Project>
'@
    }

    It 'reads the database options of the project and leaves missing ones empty' {
        $settings = Get-RepoSqlProjectSetting $projectFile
        $settings['Collation'] | Should -Be 'Chinese_Taiwan_Stroke_CI_AS'
        $settings['CompatibilityLevel'] | Should -Be '170'
        $settings['ReadCommittedSnapshot'] | Should -BeNullOrEmpty
    }

    It 'reports nothing when the database agrees with the project' {
        $project = @{ Collation = 'Chinese_Taiwan_Stroke_CI_AS'; CompatibilityLevel = '170'; ReadCommittedSnapshot = 'True' }
        @(Compare-RepoDatabaseSetting @{ Collation = 'Chinese_Taiwan_Stroke_CI_AS'; CompatibilityLevel = 170; ReadCommittedSnapshot = $true } $project).Count | Should -Be 0
    }

    It 'reports each differing or missing option' {
        $messages = @(Compare-RepoDatabaseSetting @{ Collation = 'SQL_Latin1_General_CP1_CI_AS'; CompatibilityLevel = 160; ReadCommittedSnapshot = $true } (Get-RepoSqlProjectSetting $projectFile))
        $messages.Count | Should -Be 3
        $messages[0] | Should -Match 'DefaultCollation.*SQL_Latin1_General_CP1_CI_AS.*Chinese_Taiwan_Stroke_CI_AS'
        $messages[1] | Should -Match 'CompatibilityMode.*160.*170'
        $messages[2] | Should -Match 'ReadCommittedSnapshot'
    }
}

Describe 'Get-RepoDisplayWidth and Format-RepoDisplayText' {
    It 'counts <Text> as <Width> columns' -ForEach @(
        @{ Text = 'Verify'; Width = 6 }
        @{ Text = '資料庫'; Width = 6 }
        @{ Text = '全形：（）、。'; Width = 14 }
        @{ Text = "a`u{3000}b"; Width = 4 }
        @{ Text = "e`u{301}"; Width = 1 }
        @{ Text = "`u{20000}"; Width = 2 }
    ) {
        Get-RepoDisplayWidth $Text | Should -Be $Width
    }

    It 'cuts by columns without splitting a wide character and pads to the exact width' {
        Format-RepoDisplayText '這是一段很長的中文說明' 9 | Should -Be '這是一..'
        Format-RepoDisplayText '這是一段很長的中文說明' 9 -Pad | Should -Be '這是一.. '
        Format-RepoDisplayText '中文' 4 | Should -Be '中文'
        Format-RepoDisplayText 'Verify' 10 -Pad | Should -Be 'Verify    '
    }
}

Describe 'Split-RepoDisplayLine' {
    It 'keeps every line within the width' {
        foreach ($line in Split-RepoDisplayLine ('以 App.dacpac 部署資料庫結構，並依序執行一次性資料修正、全文物件與種子資料。' * 3) 17) {
            Get-RepoDisplayWidth $line | Should -BeLessOrEqual 17
        }
    }

    It 'breaks at spaces so words and parameters stay whole' {
        Split-RepoDisplayLine './scripts/Verify.ps1 -Browser -Performance' 24 | Should -Be @('./scripts/Verify.ps1', '-Browser -Performance')
    }

    It 'never starts a line with Chinese closing punctuation' {
        Split-RepoDisplayLine '一二三四，五六' 8 | Should -Be @('一二三', '四，五六')
    }

    It 'keeps existing line breaks' {
        Split-RepoDisplayLine "第一行`n第二行" 40 | Should -Be @('第一行', '第二行')
    }
}

Describe 'Get-RepoScriptCatalog' {
    BeforeAll {
        $catalog = @(Get-RepoScriptCatalog)
        $categories = @((Get-RepoScriptCategory).Keys)
    }

    It 'lists every entry script except the menu, each in a known category, ordered by category' {
        $catalog.Name | Should -Not -Contain 'Show-Scripts'
        $catalog.Count | Should -Be (@(Get-ChildItem -LiteralPath (Join-Path (Get-RepoRoot) 'scripts') -Filter '*.ps1').Count - 1)
        foreach ($item in $catalog) { $categories | Should -Contain $item.Category -Because "$($item.Name).ps1 的 help 需要 .COMPONENT（分類見 Get-RepoScriptCategory）" }
        $order = @($catalog | ForEach-Object { $categories.IndexOf($_.Category) })
        $order | Should -Be @($order | Sort-Object)
    }

    It 'reads parameter kinds, defaults and choices from the param block' {
        $export = @($catalog | Where-Object Name -EQ 'Export-Database')[0].Parameters
        ($export | Where-Object Name -EQ 'Server').Mandatory | Should -BeTrue
        ($export | Where-Object Name -EQ 'Credential').Kind | Should -Be 'Credential'
        $port = @($catalog | Where-Object Name -EQ 'Start-Dev')[0].Parameters | Where-Object Name -EQ 'BackendPort'
        $port.Kind | Should -Be 'Number'
        $port.Default | Should -Be '5080'
        (@($catalog | Where-Object Name -EQ 'Start-Local')[0].Parameters | Where-Object Name -EQ 'PublishDirectory').DynamicDefault | Should -BeTrue
        (@($catalog | Where-Object Name -EQ 'Export-Database')[0].Parameters | Where-Object Name -EQ 'ExcludeTable').Kind | Should -Be 'List'
    }

    It 'parses examples into parameter values, reading (Get-Credential) as ask before running' {
        $examples = @($catalog | Where-Object Name -EQ 'Start-Dev')[0].Examples
        $examples[1].Parameters['BackendPort'] | Should -Be 5081
        $examples[1].Parameters['FrontendPort'] | Should -Be 4201
        $folder = Join-Path $TestDrive 'choices'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $folder 'Sample.ps1') -Value @'
<#
.SYNOPSIS
範例

.EXAMPLE
./scripts/Sample.ps1 -Action Post -Credential (Get-Credential) -Trust
#>
[CmdletBinding()]
param([ValidateSet('Script', 'Post')][string]$Action = 'Script', [pscredential]$Credential, [switch]$Trust)
'@
        $sample = @(Get-RepoScriptCatalog $folder)[0]
        ($sample.Parameters | Where-Object Name -EQ 'Action').Choices | Should -Be @('Script', 'Post')
        $sample.Examples[0].Parameters['Action'] | Should -Be 'Post'
        $sample.Examples[0].Parameters['Credential'] | Should -BeTrue
        $sample.Examples[0].Parameters['Trust'] | Should -BeTrue
    }

    It 'joins source line breaks without spaces between Chinese and keeps list items on their own lines' {
        $folder = Join-Path $TestDrive 'scripts'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $folder 'Sample.ps1') -Value @'
<#
.SYNOPSIS
第一行
接著第二行

.DESCRIPTION
English words
continue here.
- 第一項
- 第二項

.COMPONENT
開發

.EXAMPLE
./scripts/Sample.ps1
#>
[CmdletBinding()]
param()
'@
        $sample = @(Get-RepoScriptCatalog $folder)[0]
        $sample.Synopsis | Should -Be '第一行接著第二行'
        $sample.Description | Should -Be "English words continue here.`n- 第一項`n- 第二項"
        $sample.Category | Should -Be '開發'
    }
}

Describe 'Format-RepoCommandLine' {
    BeforeAll {
        New-Item -ItemType Directory -Path (Join-Path $TestDrive 'scripts') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $TestDrive 'scripts/Echo.ps1') -Value 'param([string]$Text, [string[]]$Items, [int]$Port, [switch]$Flag) $PSBoundParameters'
    }

    It 'quotes <Text> so PowerShell binds the same value back' -ForEach @(
        @{ Text = '(localdb)\MSSQLLocalDB' }
        @{ Text = '中文資料庫' }
        @{ Text = "王`u{2019}s 資料夾" }
        @{ Text = "`u{2018}單引號`u{2019}與`u{201C}雙引號`u{201D}" }
        @{ Text = "`u{2014}破折號開頭" }
        @{ Text = "全形`u{3000}空白" }
        @{ Text = '08' }
        @{ Text = '1kb' }
        @{ Text = "it's" }
        @{ Text = '$env:PATH' }
        @{ Text = 'a;b' }
        @{ Text = '' }
    ) {
        $line = Format-RepoCommandLine 'Echo' ([ordered]@{ Text = $Text; Items = [string[]]@('a b', '中文'); Port = 5081; Flag = $true })
        Push-Location -LiteralPath $TestDrive
        try { $bound = Invoke-Expression $line } finally { Pop-Location }
        $bound['Text'] | Should -BeExactly $Text
        $bound['Items'] | Should -Be @('a b', '中文')
        $bound['Port'] | Should -Be 5081
        $bound['Flag'] | Should -BeTrue
    }

    It 'never writes a credential, only (Get-Credential)' {
        $credential = [pscredential]::new('sa', (ConvertTo-SecureString 'p@ss' -AsPlainText -Force))
        $line = Format-RepoCommandLine 'Export-Database' ([ordered]@{ Server = 'sql'; Credential = $credential })
        $line | Should -Be './scripts/Export-Database.ps1 -Server sql -Credential (Get-Credential)'
        Format-RepoCommandLine 'Export-Database' ([ordered]@{ Credential = [pscredential]::Empty }) | Should -Be './scripts/Export-Database.ps1 -Credential (Get-Credential)'
    }

    It 'leaves out unset values and false switches' {
        Format-RepoCommandLine 'Verify' ([ordered]@{ Browser = $false; BrowserTarget = $null; Performance = $true }) | Should -Be './scripts/Verify.ps1 -Performance'
    }
}

Describe 'Get-RepoScriptHistory and Add-RepoScriptHistory' {
    It 'keeps the latest run of each script, newest first, up to the limit' {
        $path = Join-Path $TestDrive 'local/script-history.json'
        Add-RepoScriptHistory $path 'Verify' ([ordered]@{ Browser = $true })
        Add-RepoScriptHistory $path 'Initialize-Database' ([ordered]@{ Database = '沙盒庫' })
        Add-RepoScriptHistory $path 'Verify' ([ordered]@{})
        $history = @(Get-RepoScriptHistory $path)
        $history.Name | Should -Be @('Verify', 'Initialize-Database')
        $history[0].Parameters.Count | Should -Be 0
        $history[1].Parameters['Database'] | Should -Be '沙盒庫'
        foreach ($name in 'A', 'B', 'C', 'D') { Add-RepoScriptHistory $path $name }
        @(Get-RepoScriptHistory $path).Name | Should -Be @('D', 'C', 'B', 'A', 'Verify')
    }

    It 'stores a credential only as ask before running' {
        $path = Join-Path $TestDrive 'credential-history.json'
        Add-RepoScriptHistory $path 'Export-Database' ([ordered]@{ Server = 'sql'; Credential = [pscredential]::new('sa', (ConvertTo-SecureString 'p@ss' -AsPlainText -Force)) })
        [IO.File]::ReadAllText($path) | Should -Not -Match 'sa"|p@ss'
        @(Get-RepoScriptHistory $path)[0].Parameters['Credential'] | Should -BeTrue
    }

    It 'treats a missing or broken file as no history' {
        @(Get-RepoScriptHistory (Join-Path $TestDrive 'missing.json')).Count | Should -Be 0
        $broken = Join-Path $TestDrive 'broken.json'
        Set-Content -LiteralPath $broken -Value '{'
        @(Get-RepoScriptHistory $broken).Count | Should -Be 0
    }
}

Describe 'Invoke-RepoSteps' {
    BeforeAll {
        # Verify.ps1 本身就在 Invoke-RepoSteps 的一步裡跑這些測試；先清掉巢狀深度，測試才看得到最外層的摘要。
        $outerDepth = Get-Variable RepoStepDepth -Scope Global -ValueOnly -ErrorAction Ignore
        Remove-Variable RepoStepDepth -Scope Global -ErrorAction Ignore
    }

    AfterAll {
        if ($null -ne $outerDepth) { $global:RepoStepDepth = $outerDepth }
    }

    It 'runs steps in order, skips with a reason and summarizes' {
        $ran = [Collections.Generic.List[string]]::new()
        $output = Invoke-RepoSteps '測試' @(
            @{ Name = '第一步'; Run = { $ran.Add('1') } }
            @{ Name = '瀏覽器測試'; Skip = '未加 -Browser'; Run = { $ran.Add('skipped') } }
            @{ Name = '第二步'; Run = { $ran.Add('2') } }
        ) 6>&1 | Out-String
        $ran | Should -Be @('1', '2')
        $output | Should -Match '\[1/3\] 第一步'
        $output | Should -Match '\[2/3\] 瀏覽器測試：略過（未加 -Browser）'
        $output | Should -Match '測試：通過'
    }

    It 'stops at the first failure, still prints the summary, then rethrows the original error' {
        $ran = [Collections.Generic.List[string]]::new()
        $lines = [Collections.Generic.List[string]]::new()
        $caught = $null
        try {
            Invoke-RepoSteps '測試' @(
                @{ Name = '後端測試'; Hint = '測試報告：artifacts/test-results/backend.trx'; Run = { throw '後端測試失敗。' } }
                @{ Name = '之後'; Run = { $ran.Add('after') } }
            ) 6>&1 | ForEach-Object { $lines.Add("$_") }
        } catch { $caught = $_ }
        $caught.Exception.Message | Should -Be '後端測試失敗。'
        $ran.Count | Should -Be 0
        $text = ($lines -join "`n") -replace "`e\[[0-9;]*m", ''
        $text | Should -Match '測試：失敗'
        $text | Should -Match '失敗.*後端測試失敗。'
        $text | Should -Match 'artifacts/test-results/backend\.trx'
        $text | Should -Match '\[2\] 之後\s+未執行'
    }

    It 'prints a single summary when a step runs another script that re-imports the module' {
        $module = Join-Path (Get-RepoRoot) 'scripts/AiNexus'
        $output = Invoke-RepoSteps '外層' @(@{ Name = '內層'; Run = {
                    Import-Module $module -Force
                    Invoke-RepoSteps '內層腳本' @(@{ Name = '小步驟'; Run = { } })
                }
            }) 6>&1 | Out-String
        $output | Should -Match '  \[1/1\] 小步驟'
        ([regex]::Matches($output, '：通過')).Count | Should -Be 1
        Get-Variable RepoStepDepth -Scope Global -ErrorAction Ignore | Should -BeNullOrEmpty
    }
}

Describe 'Shared settings and the saved SQL login' {
    BeforeEach {
        $settings = Join-Path $TestDrive 'config.json'
        $secrets = Join-Path $TestDrive 'secrets.json'
        $defaults = Join-Path $TestDrive 'defaults.json'
        Save-RepoJson $defaults ([ordered]@{ Databases = [ordered]@{ App = [ordered]@{ Server = 'sql-default'; Name = 'AiNexus'; TrustServerCertificate = $true; IntegratedSecurity = $false } } })
        Save-RepoJson $settings ([ordered]@{ Databases = [ordered]@{ App = [ordered]@{ Server = '' } } })
        Save-RepoJson $secrets ([ordered]@{ Databases = [ordered]@{ App = [ordered]@{ User = ''; Password = '' } } })
    }

    It 'falls back to appsettings.json for values the machine settings leave empty' {
        $shared = Get-RepoSharedSetting -Path $settings -DefaultsPath $defaults -SecretsPath $secrets
        $shared.Values['Server'] | Should -Be 'sql-default'
        $shared.Values['Database'] | Should -Be 'AiNexus'
        $shared.Values['TrustServerCertificate'] | Should -BeTrue
        $shared.SavedUser | Should -BeNullOrEmpty
        $shared.HasSavedPassword | Should -BeFalse
    }

    It 'writes the shared values into the machine settings and keeps other keys' {
        Set-RepoSharedSetting ([ordered]@{ Server = '192.0.2.5'; Database = '測試庫'; Unknown = 'x' }) -Path $settings
        $shared = Get-RepoSharedSetting -Path $settings -DefaultsPath $defaults -SecretsPath $secrets
        $shared.Values['Server'] | Should -Be '192.0.2.5'
        $shared.Values['Database'] | Should -Be '測試庫'
        (Read-RepoJson $settings).Contains('Unknown') | Should -BeFalse
    }

    It 'saves, keeps and clears the SQL login without ever returning the password in the settings' {
        Set-RepoSavedCredential -User 'sa' -Password 'p@ss' -SecretsPath $secrets
        $shared = Get-RepoSharedSetting -Path $settings -DefaultsPath $defaults -SecretsPath $secrets
        $shared.SavedUser | Should -Be 'sa'
        $shared.HasSavedPassword | Should -BeTrue
        ($shared | ConvertTo-Json -Depth 5) | Should -Not -Match 'p@ss'
        Set-RepoSavedCredential -User 'reader' -Password $null -SecretsPath $secrets
        $credential = Get-RepoSavedCredential -SecretsPath $secrets
        $credential.UserName | Should -Be 'reader'
        $credential.GetNetworkCredential().Password | Should -Be 'p@ss'
        Set-RepoSavedCredential -Clear -SecretsPath $secrets
        Get-RepoSavedCredential -SecretsPath $secrets | Should -BeNullOrEmpty
    }
}
