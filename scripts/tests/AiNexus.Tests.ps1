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
        $settings = @{ Attachments = @{} }
        Get-RepoSetting $settings 'Attachments.StoragePath' | Should -BeNullOrEmpty
        Get-RepoSetting $settings 'Diagnostics.Directory' | Should -BeNullOrEmpty
        Get-RepoSetting $settings 'Attachments.StoragePath.Deeper' | Should -BeNullOrEmpty
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
            Databases = [ordered]@{ App = [ordered]@{ Server = 'sql'; User = 'login'; Password = 'secret-1' }; Gdweb = [ordered]@{ Server = 'sql'; Password = 'secret-0' } }
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
        @{ Path = 'backend/src/AiNexus.Features/Artifacts/ExportArtifact.cs' }, @{ Path = 'backend/src/AiNexus.Host/appsettings.json' },
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
        $exclude = @('dbo.__EFMigrationsHistory', '*.*_bak', '*.*_bak[0-9_]*')
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
