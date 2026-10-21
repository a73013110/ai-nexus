# 前端形狀與線條規範（docs/frontend/shapes.md、docs/frontend/lines.md）：圓角與外框顏色都依元件類型取
# tokens.scss 的語意角色，不直接用原始值、不寫死，也不在元件裡改寫角色（那會讓同類元件因頁面而異）。
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

Describe 'Frontend lines' {
    BeforeAll {
        # 每個外框宣告（border*、outline、outline-color）與線條自訂屬性（--line、--*-line）的位置與值。
        $outlines = foreach ($name in $styles) {
            $number = 0
            foreach ($line in [IO.File]::ReadAllLines((Join-Path $root $name))) {
                $number++
                if ($line -match '(?:^|[\s{;])(border(?:-[a-z]+)*|outline(?:-color)?|--(?:[a-z]+-)*line)\s*:\s*([^;]+);') {
                    [pscustomobject]@{ At = "${name}:$number"; Property = $Matches[1]; Value = $Matches[2].Trim() }
                }
            }
        }
    }

    It 'never write a raw color into a border or outline' {
        # forced-colors 的系統色（CanvasText、Highlight）不是寫死的顏色，照常允許。
        $raw = foreach ($outline in $outlines) {
            if ($outline.Value -match '#[0-9a-fA-F]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(') {
                "$($outline.At) $($outline.Property): $($outline.Value)"
            }
        }
        $raw | Should -BeNullOrEmpty
    }

    It 'declare --line and --*-line roles only in tokens.scss' {
        # 例如 --search-field-line：只為了讓某頁的控制項換外框色的旋鈕。
        $knobs = foreach ($outline in $outlines) {
            if ($outline.Property -like '--*') { "$($outline.At) $($outline.Property)" }
        }
        $knobs | Should -BeNullOrEmpty
    }

    # 外框是辨識元件邊界的控制項：外框只取 --control-line，不經旋鈕、不因 appearance 換色。
    It '<Selector> outlines with --control-line' -ForEach @(
        @{ File = 'frontend/src/styles/controls.scss'; Selector = '.secondary-button' }
        @{ File = 'frontend/src/app/shared/ui/search-field.scss'; Selector = '.search-field' }
        @{ File = 'frontend/src/app/shared/ui/select.scss'; Selector = '.select-trigger' }
        @{ File = 'frontend/src/app/shared/ui/select.scss'; Selector = '.select-check' }
        @{ File = 'frontend/src/styles/data-workspace.scss'; Selector = 'select:where(.ui-input)' }
        @{ File = 'frontend/src/styles/data-workspace.scss'; Selector = '.ui-pagination-group' }
        @{ File = 'frontend/src/app/shared/ui/date-time-picker.scss'; Selector = '.ui-date-control' }
        @{ File = 'frontend/src/app/shared/ui/time-range-picker.scss'; Selector = '.ui-time-range-trigger' }
        @{ File = 'frontend/src/app/shared/ui/checkbox.scss'; Selector = '.selection-indicator' }
        @{ File = 'frontend/src/app/features/work/dashboard/dashboard-toolbar.scss'; Selector = '.toolbar-pill' }
    ) {
        $text = [IO.File]::ReadAllText((Join-Path $root $File))
        $rules = [regex]::Matches($text, '(?m)^[ \t]*' + [regex]::Escape($Selector) + '\s*\{(?<body>[^{}]*)\}')
        $rules.Count | Should -BeGreaterThan 0 -Because "$File 應有 $Selector 規則"
        $borders = foreach ($rule in $rules) {
            foreach ($match in [regex]::Matches($rule.Groups['body'].Value, '(?:^|[\s;])(border(?:-(?:top|right|bottom|left|block|inline)(?:-(?:start|end))?)?(?:-color)?)\s*:\s*([^;]+);')) {
                $match.Groups[2].Value.Trim()
            }
        }
        @($borders).Count | Should -BeGreaterThan 0 -Because "$Selector 應宣告外框"
        $borders | Where-Object { $_ -notmatch 'var\(--control-line\)' -or $_ -match 'var\(--[a-z-]+,' } | Should -BeNullOrEmpty
        $text | Should -Not -Match '--(?:tile|material)-line' -Because "$File 是控制項，外框不取表面或材質的線條"
    }
}
