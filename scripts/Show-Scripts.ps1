#requires -Version 7.4

<#
.SYNOPSIS
互動選單：依分類瀏覽入口腳本，填好參數、確認指令後執行。

.DESCRIPTION
上下鍵選腳本，直接打字篩選（名稱、用途或分類，中英文皆可，空白分隔多個關鍵字），Enter 進入參數畫面。
說明、參數與範例都讀自各腳本的 comment-based help，分類是 help 的 .COMPONENT。執行前顯示完整指令，可以複製下來直接使用。
最近執行的腳本與參數記在 .local/script-history.json，下次排在最上面並帶入上次的參數。
F2 設定共用的資料庫伺服器、名稱與是否信任憑證（寫入 .local/config/appsettings.Local.json，與網站共用）；有 -Server、-Database 參數的腳本會帶入，
每個畫面都顯示目前的操作對象。SQL 帳密可以在 F2 儲存（.local/secrets，只有目前使用者可讀，也是網站用的那組），或每次執行前輸入；
-Credential 選「使用已儲存」或「執行前輸入」，離開選單後才取用或以 Get-Credential 詢問，密碼不會出現在畫面、指令與歷程。
輸入或輸出被重導（CI、管線）時改印純文字清單。需要 PowerShell 7（pwsh）；在 Windows PowerShell 5.1 執行會被 #requires 擋下。

.PARAMETER List
只印出依分類排列的腳本清單，不進入互動選單。

.PARAMETER Filter
預先填入的篩選文字，例如 Verify 或 資料庫。

.EXAMPLE
./scripts/Show-Scripts.ps1

.EXAMPLE
./scripts/Show-Scripts.ps1 -Filter 資料庫

.EXAMPLE
./scripts/Show-Scripts.ps1 -List
#>
[CmdletBinding()]
param([switch]$List, [string]$Filter = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$catalog = @(Get-RepoScriptCatalog)
$categories = Get-RepoScriptCategory
$historyPath = Join-Path (Get-RepoRoot) '.local/script-history.json'
$nameWidth = ($catalog | ForEach-Object { Get-RepoDisplayWidth $_.Name } | Measure-Object -Maximum).Maximum
$plain = [bool]$env:NO_COLOR
$theme = @{
    Reset = $PSStyle.Reset
    Bold = $PSStyle.Bold
    Dim = $PSStyle.Dim
    Reverse = $PSStyle.Reverse
    Accent = if ($plain) { '' } else { $PSStyle.Foreground.Cyan }
    Warn = if ($plain) { '' } else { $PSStyle.Foreground.Yellow }
    Good = if ($plain) { '' } else { $PSStyle.Foreground.Green }
    Error = if ($plain) { '' } else { $PSStyle.Foreground.Red }
}
$serverCategory = 'IIS 主機'

function Test-Match($Item, [string[]]$Terms) {
    $text = "$($Item.Name) $($Item.Synopsis) $($Item.Category)"
    !@($Terms | Where-Object { $text.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 }).Count
}

function Get-Terms([string]$Text) { @($Text -split '\s+' | Where-Object { $_ }) }

function Get-CategoryTitle([string]$Category) {
    $name = if ($Category) { $Category } else { '其他' }
    $note = if ($Category -and $categories.Contains($Category)) { $categories[$Category] } else { '' }
    @($name, $note)
}

function Write-ScriptList {
    $terms = @(Get-Terms $Filter)
    $current = $null
    foreach ($item in $catalog | Where-Object { Test-Match $_ $terms }) {
        if ($item.Category -ne $current) {
            $current = $item.Category
            $name, $note = Get-CategoryTitle $current
            Write-Output ''
            Write-Output $(if ($note) { "$name（$note）" } else { $name })
        }
        Write-Output ('  ' + (Format-RepoDisplayText $item.Name $nameWidth -Pad) + '  ' + $item.Synopsis)
    }
    Write-Output ''
    $shared = (Get-RepoSharedSetting).Values
    Write-Output "共用設定：資料庫 $($shared.Server) / $($shared.Database)（互動選單按 F2 修改）"
    Write-Output '完整說明與範例：Get-Help ./scripts/<名稱>.ps1 -Detailed'
}

# ---- 畫面組裝：只產生字串，不碰主控台，方便測試 ----

function Format-Line([object[]]$Segments, [int]$Width, [string]$LineStyle = '') {
    # Segments 是「文字, 樣式」成對排列。每段依剩餘寬度截斷（中文算 2 格），整行不超過 Width，
    # 終端機就不會自動換行而把版面往下推。LineStyle（反白）套在每段前面並補滿到行尾。
    $line = ''
    $left = $Width
    for ($i = 0; $i -lt $Segments.Count; $i += 2) {
        if ($left -le 0) { break }
        $text = Format-RepoDisplayText ("$($Segments[$i])" -replace '[\x00-\x1F\x7F]', ' ') $left
        $left -= Get-RepoDisplayWidth $text
        $line += $LineStyle + $Segments[$i + 1] + $text + $theme.Reset
    }
    if ($LineStyle -and $left -gt 0) { $line += $LineStyle + (' ' * $left) + $theme.Reset }
    $line
}

function Format-Rule([int]$Width) { Format-Line @(('-' * $Width), $theme.Dim) $Width }

function Add-Wrapped([Collections.Generic.List[string]]$Lines, [string]$Text, [string]$Style, [int]$Width, [string]$Prefix = ' ') {
    foreach ($part in Split-RepoDisplayLine $Text ($Width - (Get-RepoDisplayWidth $Prefix))) { $Lines.Add((Format-Line @("$Prefix$part", $Style) $Width)) }
}

function Add-FullwidthWarning([Collections.Generic.List[string]]$Lines, $Value, [int]$Width) {
    # 中文輸入法在全形模式會打出全形英數與全形空白，路徑或伺服器名稱多半是誤打，只提醒、不自動轉換（中文資料夾名稱可能本來就有全形標點）。
    if ("$Value" -match '[\uFF01-\uFF5E\u3000]') { Add-Wrapped $Lines '提醒：值裡有全形英數或全形空白，若不是故意的，請切換成半形再輸入。' $theme.Warn $Width }
}

function Get-CheckboxSegments([bool]$Checked) {
    if ($Checked) { @('[x]', "$($theme.Bold)$($theme.Good)") } else { @('[ ]', $theme.Dim) }
}

function Get-ListRows([string]$Text) {
    $terms = @(Get-Terms $Text)
    $rows = [Collections.Generic.List[hashtable]]::new()
    if (!$terms.Count) {
        $recent = @(Get-RepoScriptHistory $historyPath | ForEach-Object {
                $name = $_.Name
                $item = @($catalog | Where-Object Name -EQ $name)
                if ($item.Count) { @{ Item = $item[0]; Values = $_.Parameters } }
            })
        if ($recent.Count) {
            $rows.Add(@{ Header = '最近使用'; Note = 'Enter 帶入上次的參數' })
            foreach ($row in $recent) { $rows.Add($row) }
        }
    }
    $current = $null
    foreach ($item in $catalog | Where-Object { Test-Match $_ $terms }) {
        if ($item.Category -ne $current -or !$rows.Count) {
            $current = $item.Category
            $name, $note = Get-CategoryTitle $current
            $rows.Add(@{ Header = $name; Note = $note })
        }
        $rows.Add(@{ Item = $item; Values = $null })
    }
    , $rows
}

function Get-Target([hashtable]$State, $Item, [hashtable]$Values) {
    # 有 -Server 參數的腳本才有操作對象。參數空白時：必填或有預設值的用預設，否則是腳本自己讀 .local 設定（Initialize-Database），也就是共用設定。
    $parameters = @($Item.Parameters)
    if (!@($parameters | Where-Object Name -EQ 'Server').Count) { return $null }
    $shared = $State.Shared.Values
    $server = "$($Values['Server'])".Trim()
    $readsSettings = !$server
    if (!$server) { $server = "$($shared.Server)" }
    $database = "$($Values['Database'])".Trim()
    if (!$database) {
        $parameter = @($parameters | Where-Object Name -EQ 'Database')
        $database = if (!$readsSettings -and $parameter.Count -and $parameter[0].Default) { $parameter[0].Default } else { "$($shared.Database)" }
    }
    $same = $server -eq "$($shared.Server)" -and $database -eq "$($shared.Database)"
    $note = if ($same) { '共用設定' } else { '與共用設定不同' }
    foreach ($parameter in @($parameters | Where-Object Kind -EQ 'Credential')) {
        if ($Values[$parameter.Name] -eq 'Saved') { $note += "，帳號 $($State.Shared.SavedUser)（已儲存）" }
        elseif ($Values[$parameter.Name] -eq 'Prompt') { $note += '，執行前輸入帳密' }
    }
    [pscustomobject]@{ Text = "操作對象：$server / $database（$note）"; Server = $server; Database = $database; Same = $same }
}

function Test-SavedCredential([hashtable]$State) { $State.Shared.SavedUser -and $State.Shared.HasSavedPassword }

function Get-SharedLine([hashtable]$State) {
    $shared = $State.Shared.Values
    $login = if (Test-SavedCredential $State) { "，帳號 $($State.Shared.SavedUser)（已儲存）" } else { '，帳密執行時輸入' }
    @(" 共用設定：資料庫 $($shared.Server) / $($shared.Database)$login", '', '   F2 修改', $theme.Dim)
}

function Get-ListFrame([hashtable]$State, [int]$Width, [int]$Height) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add((Format-Line @(' AI Nexus 腳本', "$($theme.Bold)$($theme.Accent)", '   上下鍵選擇  Enter 開啟  打字篩選  F2 共用設定  Esc 清除或離開', $theme.Dim) $Width))
    $lines.Add((Format-Line (Get-SharedLine $State) $Width))
    $lines.Add((Format-Line $(if ($State.Filter) { @(' 篩選 ', $theme.Dim, $State.Filter, $theme.Bold, ' ', $theme.Reverse) }
                else { @(' 篩選 ', $theme.Dim, ' ', $theme.Reverse, ' 輸入名稱、用途或分類', $theme.Dim) }) $Width))
    $lines.Add((Format-Rule $Width))
    $headerCount = $lines.Count
    $detailHeight = [Math]::Min(8, [Math]::Max(3, [int]($Height / 3)))
    $listHeight = [Math]::Max(1, $Height - $headerCount - 1 - $detailHeight)
    $rows = $State.Rows
    # 捲動：選取列一定看得到；選到分類下第一支時，連分類標題一起露出。
    if ($State.Selected -ge 0) {
        $first = if ($State.Selected -gt 0 -and $rows[$State.Selected - 1]['Header']) { $State.Selected - 1 } else { $State.Selected }
        if ($first -lt $State.Top) { $State.Top = $first }
        if ($State.Selected -ge $State.Top + $listHeight) { $State.Top = $State.Selected - $listHeight + 1 }
    }
    $State.Top = [Math]::Max(0, [Math]::Min($State.Top, $rows.Count - $listHeight))
    for ($i = $State.Top; $i -lt [Math]::Min($rows.Count, $State.Top + $listHeight); $i++) {
        $row = $rows[$i]
        $lines.Add($(if ($row['Header']) { Format-Line @(" $($row['Header'])", "$($theme.Bold)$($theme.Accent)", "  $($row['Note'])", $theme.Dim) $Width }
                elseif ($i -eq $State.Selected) { Format-Line @(' > ', '', (Format-RepoDisplayText $row['Item'].Name $nameWidth -Pad), $theme.Bold, "  $($row['Item'].Synopsis)", '') $Width $theme.Reverse }
                else { Format-Line @('   ', '', (Format-RepoDisplayText $row['Item'].Name $nameWidth -Pad), $theme.Bold, "  $($row['Item'].Synopsis)", $theme.Dim) $Width }))
    }
    if (!$rows.Count) { $lines.Add((Format-Line @("   沒有符合「$($State.Filter)」的腳本。", $theme.Dim) $Width)) }
    while ($lines.Count -lt $headerCount + $listHeight) { $lines.Add('') }
    $lines.Add((Format-Rule $Width))

    $detail = [Collections.Generic.List[string]]::new()
    if ($State.Selected -ge 0) {
        $row = $rows[$State.Selected]
        $item = $row['Item']
        if ($item.Category -eq $serverCategory) { Add-Wrapped $detail "注意：$($categories[$serverCategory])，開發時通常不需要執行。" $theme.Warn $Width }
        if ($null -ne $row['Values']) { Add-Wrapped $detail "上次：$(Format-RepoCommandLine $item.Name (ConvertTo-RunParameters $item (New-EditorValues $item $row['Values'])))" $theme.Bold $Width }
        Add-Wrapped $detail $(if ($item.Description) { $item.Description } else { $item.Synopsis }) '' $Width
    }
    if ($detail.Count -gt $detailHeight) {
        $detail.RemoveRange($detailHeight - 1, $detail.Count - $detailHeight + 1)
        $detail.Add((Format-Line @(" 完整說明：Get-Help ./scripts/$($rows[$State.Selected]['Item'].Name).ps1 -Detailed", $theme.Dim) $Width))
    }
    $lines.AddRange($detail)
    , $lines
}

function Get-Placeholder($Parameter) {
    if ($Parameter.Mandatory) { return '必填' }
    if ($Parameter.DynamicDefault) { return '依環境決定' }
    if ($Parameter.Default) { return "預設 $($Parameter.Default)" }
    if ($Parameter.Kind -eq 'List') { return '多個值用逗號分隔' }
    '未指定'
}

function Get-ValueSegments($Parameter, $Value, [bool]$Selected) {
    switch ($Parameter.Kind) {
        'Switch' { return Get-CheckboxSegments $Value }
        'Unsupported' { return @('選單無法填寫這個參數，需要時請手動下指令', $theme.Dim) }
        'Credential' {
            $segments = switch ($Value) {
                'Saved' { @('使用已儲存的帳密', "$($theme.Bold)$($theme.Good)") }
                'Prompt' { @('執行前以 Get-Credential 輸入，不記錄', "$($theme.Bold)$($theme.Good)") }
                default { @('未指定', $theme.Dim) }
            }
            if ($Selected) { $segments += @('   左右鍵切換', $theme.Dim) }
            return $segments
        }
        'Choice' {
            $segments = if ($Value) { @($Value, $theme.Bold) } else { @((Get-Placeholder $Parameter), $theme.Dim) }
            if ($Selected) { $segments += @("   左右鍵切換：$($Parameter.Choices -join ' / ')", $theme.Dim) }
            return $segments
        }
    }
    $segments = @()
    if ($Value) { $segments += @($Value, $theme.Bold) }
    if ($Selected) { $segments += @(' ', $theme.Reverse) }
    if (!$Value) { $segments += @(($(if ($Selected) { ' ' } else { '' }) + (Get-Placeholder $Parameter)), $theme.Dim) }
    $segments
}

function Get-ParameterFrame([hashtable]$State, [int]$Width, [int]$Height) {
    $item = $State.Entry
    $parameters = @($item.Parameters)
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add((Format-Line @(" $($item.Name)", "$($theme.Bold)$($theme.Accent)", "  $($item.Synopsis)", $theme.Dim) $Width))
    $lines.Add((Format-Line @(' 上下鍵選參數  空白鍵或左右鍵切換  直接打字輸入  Tab 套用範例  Enter 下一步  Esc 返回', $theme.Dim) $Width))
    $target = Get-Target $State $item $State.Values
    if ($target) { $lines.Add((Format-Line @(" $($target.Text)", $(if ($target.Same) { $theme.Bold } else { "$($theme.Bold)$($theme.Warn)" })) $Width)) }
    $lines.Add((Format-Rule $Width))
    $headerCount = $lines.Count
    $labelWidth = ($parameters | ForEach-Object { Get-RepoDisplayWidth "-$($_.Name)*" } | Measure-Object -Maximum).Maximum
    $detailHeight = [Math]::Min(9, [Math]::Max(4, [int]($Height / 3)))
    $listHeight = [Math]::Max(1, $Height - $headerCount - 1 - $detailHeight)
    if ($State.Field -lt $State.FieldTop) { $State.FieldTop = $State.Field }
    if ($State.Field -ge $State.FieldTop + $listHeight) { $State.FieldTop = $State.Field - $listHeight + 1 }
    for ($i = $State.FieldTop; $i -lt [Math]::Min($parameters.Count, $State.FieldTop + $listHeight); $i++) {
        $parameter = $parameters[$i]
        $selected = $i -eq $State.Field
        $label = Format-RepoDisplayText ("-$($parameter.Name)" + $(if ($parameter.Mandatory) { '*' } else { '' })) $labelWidth -Pad
        $segments = @($(if ($selected) { ' > ' } else { '   ' }), '', $label, $(if ($selected) { "$($theme.Bold)$($theme.Accent)" } else { '' }), '  ', '') +
            (Get-ValueSegments $parameter $State.Values[$parameter.Name] $selected)
        $lines.Add((Format-Line $segments $Width))
    }
    while ($lines.Count -lt $headerCount + $listHeight) { $lines.Add('') }
    $lines.Add((Format-Rule $Width))

    $detail = [Collections.Generic.List[string]]::new()
    $current = $parameters[$State.Field]
    # 訊息與提醒排在參數說明前面，空間不夠時被截掉的是說明。
    if ($State.Message) { Add-Wrapped $detail $State.Message $State.MessageStyle $Width }
    Add-FullwidthWarning $detail $State.Values[$current.Name] $Width
    if ($current.Help) { Add-Wrapped $detail $current.Help '' $Width }
    $command = Format-RepoCommandLine $item.Name (ConvertTo-RunParameters $item $State.Values)
    $preview = [Collections.Generic.List[string]]::new()
    Add-Wrapped $preview "指令：$command" $theme.Bold $Width
    $room = [Math]::Max(0, $detailHeight - $preview.Count)
    if ($detail.Count -gt $room) { $detail.RemoveRange($room, $detail.Count - $room) }
    $lines.AddRange($detail)
    $lines.AddRange($preview)
    , $lines
}

function Get-ConfirmFrame([hashtable]$State, [int]$Width) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add((Format-Line @(' 確認執行', "$($theme.Bold)$($theme.Accent)", '   Enter 執行  C 複製指令  Esc 返回修改', $theme.Dim) $Width))
    $lines.Add((Format-Rule $Width))
    $lines.Add('')
    $target = Get-Target $State $State.Entry $State.Values
    if ($target) {
        Add-Wrapped $lines $target.Text "$($theme.Bold)$($theme.Warn)" $Width
        $lines.Add('')
    }
    Add-Wrapped $lines $State.Command $theme.Bold $Width '   '
    $lines.Add('')
    if ($State.Entry.Category -eq $serverCategory) {
        Add-Wrapped $lines "注意：這支腳本$($categories[$serverCategory])。在這台機器執行前，請確認連到的伺服器與資料庫。" $theme.Warn $Width
        $lines.Add('')
    }
    if ($State.Message) { Add-Wrapped $lines $State.Message $State.MessageStyle $Width }
    , $lines
}

function Get-SettingsKeys { @('Server', 'Database', 'TrustServerCertificate', 'User', 'Password') }

function Get-SettingsFrame([hashtable]$State, [int]$Width) {
    $labels = [ordered]@{}
    foreach ($key in $State.Shared.Labels.Keys) { $labels[$key] = $State.Shared.Labels[$key] }
    $labels['User'] = 'SQL 帳號'
    $labels['Password'] = 'SQL 密碼'
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add((Format-Line @(' 共用設定', "$($theme.Bold)$($theme.Accent)", '   上下鍵選欄位  直接打字輸入  空白鍵切換  Delete 清除  Enter 儲存  Esc 取消', $theme.Dim) $Width))
    $lines.Add((Format-Rule $Width))
    $keys = @(Get-SettingsKeys)
    $labelWidth = ($labels.Values | ForEach-Object { Get-RepoDisplayWidth $_ } | Measure-Object -Maximum).Maximum
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $key = $keys[$i]
        $selected = $i -eq $State.SettingsField
        $value = $State.SettingsValues[$key]
        $segments = @($(if ($selected) { ' > ' } else { '   ' }), '', (Format-RepoDisplayText $labels[$key] $labelWidth -Pad), $(if ($selected) { "$($theme.Bold)$($theme.Accent)" } else { '' }), '  ', '')
        if ($key -eq 'Password') {
            # 密碼只顯示同樣長度的星號，不顯示內容；沒有改動時只標示是否已儲存。
            if ($null -eq $value) { $segments += @($(if ($State.Shared.HasSavedPassword) { '已儲存，打字以更換' } else { '未儲存' }), $theme.Dim) }
            elseif ($value -eq '') { $segments += @('儲存時清除', $theme.Warn) }
            else { $segments += @(('*' * $value.Length), $theme.Bold) }
        } elseif ($value -is [bool]) { $segments += Get-CheckboxSegments $value }
        else { $segments += @("$value", $theme.Bold) }
        if ($selected -and $value -isnot [bool]) { $segments += @(' ', $theme.Reverse) }
        $lines.Add((Format-Line $segments $Width))
    }
    $lines.Add((Format-Rule $Width))
    if ($State.Message) { Add-Wrapped $lines $State.Message $State.MessageStyle $Width }
    $current = $keys[$State.SettingsField]
    if ($current -ne 'Password') { Add-FullwidthWarning $lines $State.SettingsValues[$current] $Width }
    Add-Wrapped $lines "伺服器與資料庫寫入 $($State.SettingsPath)；帳密寫入 $($State.SecretsPath)，只有目前使用者可讀。兩者都和網站共用。" $theme.Dim $Width
    Add-Wrapped $lines '帳號或密碼留空就不儲存，-Credential 改成執行前輸入。' $theme.Dim $Width
    , $lines
}

# ---- 參數值：選單內一律存成字串或布林，執行前才轉成腳本的型別 ----

function New-EditorValues($Item, [Collections.IDictionary]$Source) {
    $values = @{}
    foreach ($parameter in $Item.Parameters) {
        $value = if ($Source -and $Source.Contains($parameter.Name)) { $Source[$parameter.Name] } else { $null }
        $values[$parameter.Name] = switch ($parameter.Kind) {
            'Switch' { [bool]$value }
            # 「使用已儲存」或「執行前輸入」；舊歷程與範例的 $true 代表執行前輸入。
            'Credential' { if ("$value" -in 'Saved', 'Prompt') { "$value" } elseif ($value -eq $true) { 'Prompt' } else { '' } }
            'List' { if ($null -ne $value) { (@($value) | ForEach-Object { "$_" }) -join ', ' } }
            default { if ($null -ne $value -and "$value" -ne '') { "$value" } }
        }
    }
    $values
}

function ConvertTo-RunParameters($Item, [hashtable]$Values) {
    $result = [ordered]@{}
    foreach ($parameter in $Item.Parameters) {
        $value = $Values[$parameter.Name]
        # Trim 也會去掉全形空白（U+3000）；清單同時接受全形逗號，中文輸入法下不必切換。
        $text = "$value".Trim()
        switch ($parameter.Kind) {
            'Switch' { if ($value) { $result[$parameter.Name] = $true } }
            # 範例或歷程裡不是數字的值原樣交給腳本，由參數繫結回報錯誤，選單本身不中斷。
            'Number' { if ($text) { $number = 0; $result[$parameter.Name] = if ([int]::TryParse($text, [ref]$number)) { $number } else { $text } } }
            'List' {
                $items = @($text -split '[,，]' | ForEach-Object Trim | Where-Object { $_ })
                if ($items.Count) { $result[$parameter.Name] = [string[]]$items }
            }
            'Unsupported' { }
            # 只放「執行前詢問」的記號，實際帳密在離開選單後才以 Get-Credential 取得。
            'Credential' { if ($value) { $result[$parameter.Name] = [pscredential]::Empty } }
            default { if ($text) { $result[$parameter.Name] = $text } }
        }
    }
    $result
}

function Add-SharedValues([hashtable]$State, $Item, [hashtable]$Values) {
    # 共用設定帶入同名參數：只帶必填或有預設值的文字參數，省略時會自己讀 .local 設定的參數（Initialize-Database 的 -Server）維持空白，
    # 否則會改變它的連線方式；switch 只在設定為 true 時勾選。
    $shared = $State.Shared.Values
    foreach ($parameter in $Item.Parameters) {
        if (!$shared.Contains($parameter.Name) -or "$($shared[$parameter.Name])" -eq '') { continue }
        $value = $shared[$parameter.Name]
        if ($parameter.Kind -eq 'Switch') { if ($value -eq $true) { $Values[$parameter.Name] = $true } }
        elseif ($parameter.Kind -eq 'Text' -and ($parameter.Mandatory -or $parameter.Default)) { $Values[$parameter.Name] = "$value" }
    }
    # 已儲存帳密時預設使用；設定成 Windows 身分（IntegratedSecurity）時不帶，避免和 -IntegratedSecurity 衝突。
    if ((Test-SavedCredential $State) -and !$State.Shared.IntegratedSecurity) {
        foreach ($parameter in @($Item.Parameters | Where-Object Kind -EQ 'Credential')) { $Values[$parameter.Name] = 'Saved' }
    }
}

# ---- 按鍵處理：回傳 'Exit' 或 'Run' 結束選單，其餘留在選單 ----

function Remove-LastCharacter([string]$Text) {
    # 以「文字元素」刪除，surrogate 字（𠀀）與組合字元一次刪完整個字。
    $info = [Globalization.StringInfo]::new($Text)
    if ($info.LengthInTextElements -le 1) { return '' }
    $info.SubstringByTextElements(0, $info.LengthInTextElements - 1)
}

function Move-Selection([hashtable]$State, [int]$Delta) {
    $rows = $State.Rows
    $items = @(for ($i = 0; $i -lt $rows.Count; $i++) { if (!$rows[$i]['Header']) { $i } })
    if (!$items.Count) { $State.Selected = -1; return }
    $position = [Math]::Max(0, [Math]::Min($items.Count - 1, [array]::IndexOf($items, $State.Selected) + $Delta))
    $State.Selected = $items[$position]
}

function Set-Filter([hashtable]$State, [string]$Text) {
    $State.Filter = $Text
    $State.Rows = Get-ListRows $Text
    $State.Selected = -1
    $State.Top = 0
    Move-Selection $State 0
}

function Open-Confirm([hashtable]$State, [string]$Back) {
    $item = $State.Entry
    $missing = @($item.Parameters | Where-Object { $_.Mandatory -and $_.Kind -ne 'Unsupported' -and !"$($State.Values[$_.Name])".Trim() } | ForEach-Object { "-$($_.Name)" })
    if ($missing.Count) {
        $State.Message = "請先填寫必填參數：$($missing -join '、')"
        $State.MessageStyle = $theme.Error
        return
    }
    try { $State.Run = ConvertTo-RunParameters $item $State.Values }
    catch {
        $State.Message = "參數值無法轉換：$($_.Exception.Message)"
        $State.MessageStyle = $theme.Error
        return
    }
    $State.Command = Format-RepoCommandLine $item.Name $State.Run
    $State.Back = $Back
    $State.Message = ''
    $State.Screen = 'Confirm'
}

function Open-Entry([hashtable]$State, $Row) {
    $State.Entry = $Row['Item']
    $State.Values = New-EditorValues $Row['Item'] $Row['Values']
    # 歷程帶入上次的值，不再用共用設定覆蓋；差異會顯示在操作對象那一行。
    if ($null -eq $Row['Values']) { Add-SharedValues $State $Row['Item'] $State.Values }
    $State.Field = 0
    $State.FieldTop = 0
    $State.Example = -1
    $State.Message = ''
    if (@($Row['Item'].Parameters | Where-Object Kind -NE 'Unsupported').Count) { $State.Screen = 'Parameters' }
    else { Open-Confirm $State 'List' }
}

function Update-ListState([hashtable]$State, $Key) {
    switch ($Key.Key) {
        'UpArrow' { Move-Selection $State -1; return }
        'DownArrow' { Move-Selection $State 1; return }
        'PageUp' { Move-Selection $State -10; return }
        'PageDown' { Move-Selection $State 10; return }
        'Home' { Move-Selection $State -1000; return }
        'End' { Move-Selection $State 1000; return }
        'Enter' { if ($State.Selected -ge 0) { Open-Entry $State $State.Rows[$State.Selected] }; return }
        'F2' { Open-Settings $State; return }
        'Escape' { if ($State.Filter) { Set-Filter $State '' } else { return 'Exit' }; return }
        'Backspace' { if ($State.Filter) { Set-Filter $State (Remove-LastCharacter $State.Filter) }; return }
    }
    if ($Key.Text) { Set-Filter $State ($State.Filter + $Key.Text) }
}

function Open-Settings([hashtable]$State) {
    $State.SettingsValues = @{}
    foreach ($key in $State.Shared.Values.Keys) { $State.SettingsValues[$key] = $State.Shared.Values[$key] }
    $State.SettingsValues['TrustServerCertificate'] = $State.Shared.Values['TrustServerCertificate'] -eq $true
    $State.SettingsValues['User'] = $State.Shared.SavedUser
    # $null 表示密碼不變；'' 表示儲存時清除。
    $State.SettingsValues['Password'] = $null
    $State.SettingsField = 0
    $State.Message = ''
    $State.Screen = 'Settings'
}

function Save-Settings([hashtable]$State) {
    $values = $State.SettingsValues
    $server = "$($values['Server'])".Trim()
    $database = "$($values['Database'])".Trim()
    $user = "$($values['User'])".Trim()
    if (!$server -or !$database) { $State.Message = '伺服器與資料庫名稱不能空白。'; $State.MessageStyle = $theme.Error; return }
    if (!$user -and $values['Password']) { $State.Message = '要儲存密碼請先填 SQL 帳號。'; $State.MessageStyle = $theme.Error; return }
    Set-RepoSharedSetting ([ordered]@{ Server = $server; Database = $database; TrustServerCertificate = [bool]$values['TrustServerCertificate'] }) -Path $State.SettingsPath
    if (!$user) { if ($State.Shared.SavedUser -or $State.Shared.HasSavedPassword) { Set-RepoSavedCredential -Clear -SecretsPath $State.SecretsPath } }
    elseif ($user -ne $State.Shared.SavedUser -or $null -ne $values['Password']) { Set-RepoSavedCredential -User $user -Password $values['Password'] -SecretsPath $State.SecretsPath }
    # 密碼寫入後立刻丟掉，不留在選單的狀態裡。
    $values['Password'] = $null
    $State.Shared = Get-RepoSharedSetting -Path $State.SettingsPath -SecretsPath $State.SecretsPath
    $State.Screen = 'List'
}

function Update-SettingsState([hashtable]$State, $Key) {
    $keys = @(Get-SettingsKeys)
    $name = $keys[$State.SettingsField]
    $value = $State.SettingsValues[$name]
    $State.Message = ''
    switch ($Key.Key) {
        'UpArrow' { $State.SettingsField = [Math]::Max(0, $State.SettingsField - 1); return }
        'DownArrow' { $State.SettingsField = [Math]::Min($keys.Count - 1, $State.SettingsField + 1); return }
        'Escape' { $State.SettingsValues = $null; $State.Screen = 'List'; return }
        'Enter' { Save-Settings $State; return }
        'Delete' { $State.SettingsValues[$name] = if ($value -is [bool]) { $false } else { '' }; return }
        'Backspace' {
            if ($value -is [bool]) { $State.SettingsValues[$name] = $false }
            elseif ($value) {
                $next = Remove-LastCharacter $value
                # 密碼刪到空白視為「不變」；要清除已儲存的密碼用 Delete。
                $State.SettingsValues[$name] = if ($name -eq 'Password' -and !$next) { $null } else { $next }
            }
            return
        }
    }
    if ($value -is [bool]) {
        if ($Key.Key -in 'Spacebar', 'LeftArrow', 'RightArrow') { $State.SettingsValues[$name] = !$value }
        return
    }
    if ($Key.Text) { $State.SettingsValues[$name] = "$value$($Key.Text)" }
}

function Step-Value([hashtable]$State, $Parameter, [int]$Delta) {
    if ($Parameter.Kind -eq 'Switch') { $State.Values[$Parameter.Name] = !$State.Values[$Parameter.Name] }
    elseif ($Parameter.Kind -eq 'Credential') {
        $options = @('', 'Prompt') + @(if (Test-SavedCredential $State) { 'Saved' })
        $index = [Math]::Max(0, [array]::IndexOf($options, "$($State.Values[$Parameter.Name])"))
        $State.Values[$Parameter.Name] = $options[($index + $Delta + $options.Count) % $options.Count]
    }
    elseif ($Parameter.Kind -eq 'Choice') {
        # 第一個選項是「未指定」，交給腳本的預設值。
        $options = @($null) + @($Parameter.Choices)
        $index = [array]::IndexOf($options, $State.Values[$Parameter.Name])
        $State.Values[$Parameter.Name] = $options[($index + $Delta + $options.Count) % $options.Count]
    }
}

function Update-ParameterState([hashtable]$State, $Key) {
    $parameters = @($State.Entry.Parameters)
    $parameter = $parameters[$State.Field]
    $name = $parameter.Name
    $State.Message = ''
    switch ($Key.Key) {
        'UpArrow' { $State.Field = [Math]::Max(0, $State.Field - 1); return }
        'DownArrow' { $State.Field = [Math]::Min($parameters.Count - 1, $State.Field + 1); return }
        'Enter' { Open-Confirm $State 'Parameters'; return }
        'Escape' { $State.Screen = 'List'; return }
        'LeftArrow' { Step-Value $State $parameter -1; return }
        'RightArrow' { Step-Value $State $parameter 1; return }
        'Delete' { $State.Values[$name] = switch ($parameter.Kind) { 'Switch' { $false } 'Credential' { '' } default { $null } }; return }
        'Tab' {
            $examples = @($State.Entry.Examples)
            if (!$examples.Count) { $State.Message = '這支腳本沒有範例。'; $State.MessageStyle = $theme.Dim; return }
            $State.Example = ($State.Example + 1) % $examples.Count
            $State.Values = New-EditorValues $State.Entry $examples[$State.Example].Parameters
            # 範例裡的主機名稱只是示意，伺服器、資料庫與帳密維持共用設定。
            Add-SharedValues $State $State.Entry $State.Values
            $State.Message = "範例 $($State.Example + 1)/$($examples.Count)：$($examples[$State.Example].Text)"
            $State.MessageStyle = $theme.Good
            return
        }
        'Backspace' {
            $State.Values[$name] = switch ($parameter.Kind) {
                'Switch' { $false }
                'Credential' { '' }
                { $_ -in 'Choice', 'Unsupported' } { $null }
                default { $(if ($State.Values[$name]) { Remove-LastCharacter $State.Values[$name] }) }
            }
            return
        }
    }
    if ($parameter.Kind -in 'Switch', 'Choice', 'Credential') {
        if ($Key.Key -eq 'Spacebar') { Step-Value $State $parameter 1 }
        return
    }
    if (!$Key.Text -or $parameter.Kind -eq 'Unsupported') { return }
    $text = $Key.Text
    if ($parameter.Kind -eq 'Number') {
        # 全形數字（１２３）是中文輸入法全形模式的常見誤打，直接轉成半形。
        $text = -join @($text.ToCharArray() | ForEach-Object { if ([char]::IsDigit($_)) { [string][int][char]::GetNumericValue($_) } else { $_ } })
        if ($text -notmatch '^\d+$') { $State.Message = '這個參數只能輸入數字。'; $State.MessageStyle = $theme.Error; return }
        if ("$($State.Values[$name])$text".Length -gt 9) { return }
    }
    $State.Values[$name] = "$($State.Values[$name])$text"
}

function Update-ConfirmState([hashtable]$State, $Key) {
    switch ($Key.Key) {
        'Enter' { return 'Run' }
        'Escape' { $State.Screen = $State.Back; $State.Message = ''; return }
        'C' {
            try {
                Set-Clipboard -Value $State.Command
                $State.Message = '已複製到剪貼簿。'
                $State.MessageStyle = $theme.Good
            } catch {
                $State.Message = "無法使用剪貼簿：$($_.Exception.Message)"
                $State.MessageStyle = $theme.Error
            }
        }
    }
}

# ---- 主控台 ----

function Read-Key {
    $key = [Console]::ReadKey($true)
    $text = if ($key.KeyChar -ne [char]0 -and ![char]::IsControl($key.KeyChar)) { [string]$key.KeyChar } else { '' }
    # CJK 擴充區的字（𠀀）是一對 surrogate，會分兩次按鍵事件送來，湊齊再處理。
    if ($text -and [char]::IsHighSurrogate($key.KeyChar)) { $text += [Console]::ReadKey($true).KeyChar }
    [pscustomobject]@{ Key = $key.Key; Ctrl = ($key.Modifiers -band [ConsoleModifiers]::Control) -ne 0; Text = $text }
}

function Write-Screen([string[]]$Lines, [int]$Height) {
    $builder = [Text.StringBuilder]::new()
    for ($row = 0; $row -lt $Height; $row++) {
        [void]$builder.Append("`e[$($row + 1);1H")
        if ($row -lt $Lines.Count) { [void]$builder.Append($Lines[$row]) }
        [void]$builder.Append("`e[0m`e[K")
    }
    [Console]::Write($builder.ToString())
}

$interactive = !$List -and ![Console]::IsInputRedirected -and ![Console]::IsOutputRedirected
if ($interactive) { try { $null = [Console]::WindowWidth } catch { $interactive = $false } }
if (!$interactive) {
    Write-ScriptList
    return
}

$localPaths = Get-RepoLocalPaths
$state = @{ Screen = 'List'; Filter = ''; Rows = $null; Selected = -1; Top = 0; Entry = $null; Values = @{}; Field = 0; FieldTop = 0
    Example = -1; Run = $null; Command = ''; Back = 'List'; Message = ''; MessageStyle = ''
    Shared = (Get-RepoSharedSetting); SettingsPath = $localPaths.Settings; SecretsPath = $localPaths.Secrets; SettingsValues = $null; SettingsField = 0 }
Set-Filter $state $Filter
$selection = $null
$treatControlC = [Console]::TreatControlCAsInput
# 替代畫面（離開後還原原本的主控台內容）、隱藏游標、關閉自動換行：
# 少數字元（─ → …）在部分終端機與字型下寬度和計算的不同，關掉自動換行後最多截掉行尾，不會讓整個版面錯位。
[Console]::Write("`e[?1049h`e[?25l`e[?7l")
try {
    [Console]::TreatControlCAsInput = $true
    while ($true) {
        $width = [Console]::WindowWidth - 1
        $height = [Console]::WindowHeight
        $tooSmall = $width -lt 40 -or $height -lt 12
        $lines = if ($tooSmall) { @((Format-Line @(' 視窗太小，請放大到至少 41 x 12。Esc 離開', $theme.Warn) $width)) }
        else {
            switch ($state.Screen) {
                'List' { Get-ListFrame $state $width $height }
                'Parameters' { Get-ParameterFrame $state $width $height }
                'Confirm' { Get-ConfirmFrame $state $width }
                'Settings' { Get-SettingsFrame $state $width }
            }
        }
        Write-Screen $lines $height
        $key = Read-Key
        if ($key.Ctrl -and $key.Key -eq 'C') { break }
        if ($tooSmall) {
            if ($key.Key -eq 'Escape') { break }
            continue
        }
        $action = switch ($state.Screen) {
            'List' { Update-ListState $state $key }
            'Parameters' { Update-ParameterState $state $key }
            'Settings' { Update-SettingsState $state $key }
            'Confirm' { Update-ConfirmState $state $key }
        }
        if ($action -eq 'Exit') { break }
        if ($action -eq 'Run') {
            $selection = $state
            break
        }
    }
} finally {
    [Console]::TreatControlCAsInput = $treatControlC
    [Console]::Write("`e[?7h`e[?25h`e[?1049l")
}

if (!$selection) { return }
$item = $selection.Entry
$parameters = $selection.Run
$target = Get-Target $selection $item $selection.Values
if ($target) { Write-Host "$($theme.Bold)$($theme.Warn)$($target.Text)$($theme.Reset)" }
Write-Host "$($theme.Accent)> $($selection.Command)$($theme.Reset)"
# 先記錄再執行：Start-Dev 之類以 Ctrl+C 結束的腳本也會留下紀錄。帳密只記來源（Saved／Prompt），不記帳號密碼。
$recorded = [ordered]@{}
foreach ($key in $parameters.Keys) { $recorded[$key] = if ($parameters[$key] -is [pscredential]) { $selection.Values[$key] } else { $parameters[$key] } }
Add-RepoScriptHistory $historyPath $item.Name $recorded
foreach ($parameter in @($item.Parameters | Where-Object { $_.Kind -eq 'Credential' -and $parameters.Contains($_.Name) })) {
    $credential = if ($selection.Values[$parameter.Name] -eq 'Saved') { Get-RepoSavedCredential -SecretsPath $selection.SecretsPath }
    if (!$credential) {
        $on = if ($target) { "$($target.Server) / $($target.Database)" } else { $item.Name }
        $credential = Get-Credential -Message "輸入 SQL 帳密：$on（不會記錄）"
        if (!$credential) { throw '沒有輸入帳密，已取消執行。' }
    }
    $parameters[$parameter.Name] = $credential
}
$global:LASTEXITCODE = 0
& $item.Path @parameters
if ($LASTEXITCODE) { exit $LASTEXITCODE }
