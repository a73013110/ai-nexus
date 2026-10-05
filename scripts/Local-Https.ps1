# Local HTTPS uses the SDK certificate; private keys stay in ignored .local.
function Assert-NexusHttpsCertificate {
    dotnet dev-certs https --check --quiet
    if ($LASTEXITCODE -ne 0) { throw '找不到本機 HTTPS 憑證。請先執行 dotnet dev-certs https --trust，再重新啟動。純 HTTP 測試可明確使用 -Http（僅限 localhost）。' }
}
