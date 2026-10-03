#requires -Version 7.4
param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# Start-Local or the development API must be running. No identity credentials are needed for this Development-only endpoint.
Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/openapi/v1.json" -OutFile (Join-Path $taskRoot 'contracts/openapi.json') -TimeoutSec 10
npm --prefix (Join-Path $taskRoot 'tooling/contracts') run generate
if ($LASTEXITCODE -ne 0) { throw 'Contract generation failed.' }
