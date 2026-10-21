# 前端形狀規範（docs/frontend/shapes.md）：圓角依元件類型取 --radius-* 語意 token，
# 不直接用 --p-radius-* 原始值、不寫死長度，也不在元件裡改寫 --radius-*（那會讓同類元件因頁面而異）。
BeforeAll {
    Set-StrictMode -Version Latest
    $root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    # tokens.scss 是唯一定義原始值與角色的地方。
    $styles = @(git -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard -- 'frontend/src/*.scss' 'frontend/src/*.ts' |
        Where-Object { $_ -notlike '*.spec.ts' -and $_ -ne 'frontend/src/styles/tokens.scss' } |
        Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf })

    # 每個圓角宣告（border-*-radius 與 --*-radius 自訂屬性）的位置與值。
    $declarations = foreach ($name in $styles) {
        $number = 0
        foreach ($line in [IO.File]::ReadAllLines((Join-Path $root $name))) {
            $number++
            if ($line -match '(?:^|[\s{;])((?:border(?:-[a-z]+)*-radius)|--[a-z-]*radius)\s*:\s*([^;]+);') {
                [pscustomobject]@{ At = "${name}:$number"; Property = $Matches[1]; Value = $Matches[2].Trim() }
            }
        }
    }
}

Describe 'Frontend radii' {
    It 'take a --radius-* role instead of a primitive or a literal length' {
        $violations = foreach ($declaration in $declarations) {
            $value = $declaration.Value
            $literal = $value -notmatch 'var\(' -and $value -notin @('0', '50%', 'inherit')
            if ($value -match '--p-radius-' -or $literal) { "$($declaration.At) $($declaration.Property): $value" }
        }
        $violations | Should -BeNullOrEmpty
    }

    It 'never redefine a --radius-* role outside tokens.scss' {
        $redefined = foreach ($declaration in $declarations) {
            if ($declaration.Property -like '--radius-*') { "$($declaration.At) $($declaration.Property)" }
        }
        $redefined | Should -BeNullOrEmpty
    }

    It 'reference --p-radius-* only in tokens.scss' {
        $uses = foreach ($name in $styles) {
            if ((Get-Content -Raw -LiteralPath (Join-Path $root $name)) -match '--p-radius-') { $name }
        }
        $uses | Should -BeNullOrEmpty
    }
}
