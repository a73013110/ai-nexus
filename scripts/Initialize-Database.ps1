#requires -Version 7.4

<#
.SYNOPSIS
以 .local/ 的設定建立空的 AiNexus 資料庫（不存在時）並開啟 RCSI。

.DESCRIPTION
執行主機的 db init 指令。只在資料庫不存在時建庫，不會刪除或清空既有資料；需要有建庫權限的 SQL 登入。
資料表等結構不在這裡建立，由 database/App 的 dacpac 部署（見 docs/architecture/database.md）。
正式環境改用主機指令，見 docs/operations/iis-deployment.md。

.EXAMPLE
./scripts/Initialize-Database.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

if ((Invoke-NexusHostCommand 'db', 'init') -ne 0) { throw '資料庫初始化未完成。請確認 SQL 連線、憑證與建庫權限。' }
