BeforeAll {
    Set-StrictMode -Version Latest
    Import-Module (Join-Path $PSScriptRoot '../AiNexus') -Force
}

Describe 'Get-NexusRoot' {
    It 'is the repository root' {
        Test-Path -LiteralPath (Join-Path (Get-NexusRoot) 'backend/AiNexus.slnx') | Should -BeTrue
    }
}

Describe 'Resolve-NexusArtifactPath' {
    It 'resolves a path inside artifacts/ against the repository root' {
        Resolve-NexusArtifactPath 'artifacts/publish' | Should -Be ([IO.Path]::GetFullPath((Join-Path (Get-NexusRoot) 'artifacts/publish')))
    }

    It 'rejects <Path>' -ForEach @(@{ Path = 'artifacts' }, @{ Path = 'artifacts-old/publish' }, @{ Path = 'artifacts/../frontend' }, @{ Path = 'frontend/dist' }) {
        { Resolve-NexusArtifactPath $Path } | Should -Throw '*artifacts/*'
    }
}

Describe 'Get-NexusSetting and Set-NexusSetting' {
    It 'creates missing sections and replaces non-section values' {
        $settings = [ordered]@{ Database = 'invalid' }
        Set-NexusSetting $settings 'Database.Server' 'sql'
        Set-NexusSetting $settings 'Inference.Providers.Google.ApiKey' 'key'
        Set-NexusSetting $settings 'AllowedHosts' 'host'
        Get-NexusSetting $settings 'Database.Server' | Should -Be 'sql'
        Get-NexusSetting $settings 'Inference.Providers.Google.ApiKey' | Should -Be 'key'
        Get-NexusSetting $settings 'AllowedHosts' | Should -Be 'host'
    }

    It 'returns $null for a missing path in strict mode' {
        Set-StrictMode -Version Latest
        $settings = @{ Attachments = @{} }
        Get-NexusSetting $settings 'Attachments.StoragePath' | Should -BeNullOrEmpty
        Get-NexusSetting $settings 'Diagnostics.Directory' | Should -BeNullOrEmpty
        Get-NexusSetting $settings 'Attachments.StoragePath.Deeper' | Should -BeNullOrEmpty
    }
}

Describe 'Save-NexusJson' {
    It 'writes UTF-8 without BOM, replaces the file and leaves no temporary file' {
        $path = Join-Path $TestDrive 'settings.json'
        Save-NexusJson $path ([ordered]@{ Name = '舊' })
        Save-NexusJson $path ([ordered]@{ Name = '新'; Nested = [ordered]@{ Value = 1 } })
        $bytes = [IO.File]::ReadAllBytes($path)
        $bytes[0] | Should -Not -Be 0xEF
        (Read-NexusJson $path)['Nested']['Value'] | Should -Be 1
        (Read-NexusJson $path)['Name'] | Should -Be '新'
        @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.tmp').Count | Should -Be 0
    }

    It 'keeps the permissions of an existing secrets file' -Skip:$IsWindows {
        $path = Join-Path $TestDrive 'secrets.json'
        Save-NexusJson $path @{}
        Protect-NexusSecrets $path
        Save-NexusJson $path @{ Database = @{ Password = 'changed' } }
        [IO.File]::GetUnixFileMode($path) | Should -Be ([IO.UnixFileMode]'UserRead, UserWrite')
    }
}

Describe 'Get-NexusSecretValues' {
    BeforeAll {
        $settings = [ordered]@{
            ConnectionStrings = [ordered]@{ Nexus = 'Server=x;Password=y' }
            Database = [ordered]@{ Server = 'sql'; User = 'login'; Password = 'secret-1' }
            Identity = [ordered]@{ ActiveDirectory = [ordered]@{ DnUser = 'CN=reader'; DnPass = 'secret-2' } }
            Inference = [ordered]@{ Providers = [ordered]@{ Google = [ordered]@{ ApiKey = 'secret-3' }; Ollama = [ordered]@{ ApiKey = '' } } }
        }
    }

    It 'returns non-empty passwords, keys and connection strings' {
        @(Get-NexusSecretValues $settings) | Should -Be @('Server=x;Password=y', 'secret-1', 'secret-2', 'secret-3')
    }

    It 'adds SQL logins with -IncludeUser' {
        @(Get-NexusSecretValues $settings -IncludeUser) | Should -Contain 'login'
    }
}

Describe 'Test-NexusForbiddenPath' {
    It 'forbids <Path>' -ForEach @(
        @{ Path = 'artifacts/publish/AiNexus.Host.dll' }, @{ Path = '.local/secrets/appsettings.Secrets.json' },
        @{ Path = 'frontend/node_modules/x/index.js' }, @{ Path = 'backend/src/AiNexus.Host/bin/Debug/a.dll' },
        @{ Path = 'backend/src/AiNexus.Host/appsettings.Local.json' }, @{ Path = 'config/appsettings.Production.json' },
        @{ Path = '.env' }, @{ Path = 'tooling/.env.local' }) {
        Test-NexusForbiddenPath $Path | Should -BeTrue
    }

    It 'allows <Path>' -ForEach @(
        @{ Path = 'backend/src/AiNexus.Features/Artifacts/ExportArtifact.cs' }, @{ Path = 'backend/src/AiNexus.Host/appsettings.json' },
        @{ Path = 'backend/src/AiNexus.Host/appsettings.Local.example.json' }, @{ Path = 'backend/src/AiNexus.Host/appsettings.Production.example.json' },
        @{ Path = '.env.example' }, @{ Path = 'docs/development/LOCAL_FOLDERS.md' }) {
        Test-NexusForbiddenPath $Path | Should -BeFalse
    }
}
