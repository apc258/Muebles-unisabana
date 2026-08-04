param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend,
    [string]$RealE2eReportPath = "",
    [string]$RealE2eCallsPath = "",
    [switch]$IncludeLatestRealE2eFromDownloads
)

$ErrorActionPreference = "Continue"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$reportsDir = Join-Path $root "reports"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $reportsDir "test-runs\$runId"
$backendDir = Join-Path $runDir "backend"
$frontendDir = Join-Path $runDir "frontend"
$realE2eDir = Join-Path $runDir "real-e2e"

New-Item -ItemType Directory -Force -Path $backendDir, $frontendDir, $realE2eDir | Out-Null

function HtmlEncode([string]$value) {
    if ($null -eq $value) { return "" }
    return [System.Net.WebUtility]::HtmlEncode($value)
}

function Run-LoggedCommand {
    param(
        [string]$Title,
        [string]$WorkingDirectory,
        [string]$LogFile,
        [scriptblock]$Command
    )

    Push-Location $WorkingDirectory
    try {
        $started = Get-Date
        Write-Host ""
        Write-Host "== $Title =="
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
        $finished = Get-Date
        $output | Out-File -FilePath $LogFile -Encoding utf8

        return [pscustomobject]@{
            Title = $Title
            ExitCode = if ($null -eq $exitCode) { 0 } else { $exitCode }
            Started = $started
            Finished = $finished
            DurationSeconds = [math]::Round(($finished - $started).TotalSeconds, 2)
            LogFile = $LogFile
            Output = ($output -join "`n")
        }
    }
    finally {
        Pop-Location
    }
}

function Get-RelativePath([string]$path) {
    $rootText = [string]$root
    if (-not $rootText.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootText = $rootText + [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = New-Object System.Uri($rootText)
    $pathUri = New-Object System.Uri((Resolve-Path $path))
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("\", "/")
}

function Get-ReportRelativePath([string]$path) {
    $reportsText = [string](Resolve-Path $reportsDir)
    if (-not $reportsText.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $reportsText = $reportsText + [System.IO.Path]::DirectorySeparatorChar
    }

    $reportsUri = New-Object System.Uri($reportsText)
    $pathUri = New-Object System.Uri((Resolve-Path $path))
    return [System.Uri]::UnescapeDataString($reportsUri.MakeRelativeUri($pathUri).ToString()).Replace("\", "/")
}

function Parse-TrxSummary([string]$trxPath) {
    if (-not (Test-Path $trxPath)) {
        return [pscustomobject]@{
            Total = 0
            Passed = 0
            Failed = 0
            Skipped = 0
            Tests = @()
        }
    }

    [xml]$trx = Get-Content $trxPath
    $counters = $trx.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    $unitResults = $trx.SelectNodes("//*[local-name()='UnitTestResult']")

    $tests = @()
    foreach ($result in $unitResults) {
        $tests += [pscustomobject]@{
            Name = $result.testName
            Outcome = $result.outcome
            Duration = $result.duration
        }
    }

    return [pscustomobject]@{
        Total = [int]$counters.total
        Passed = [int]$counters.passed
        Failed = [int]$counters.failed
        Skipped = [int]$counters.notExecuted
        Tests = $tests
    }
}

function Parse-FrontendSummary([string]$output) {
    $tests = 0
    $passed = 0
    $failed = 0
    $files = 0

    if ($output -match "Tests\s+(\d+)\s+passed") {
        $tests = [int]$Matches[1]
        $passed = $tests
    }
    if ($output -match "Test Files\s+(\d+)\s+passed") {
        $files = [int]$Matches[1]
    }
    if ($output -match "(\d+)\s+failed") {
        $failed = [int]$Matches[1]
    }

    return [pscustomobject]@{
        Files = $files
        Total = $tests + $failed
        Passed = $passed
        Failed = $failed
    }
}

function Find-LatestDownload([string]$Pattern) {
    $downloadsDir = Join-Path $env:USERPROFILE "Downloads"
    if (-not (Test-Path $downloadsDir)) {
        return ""
    }

    $file = Get-ChildItem -Path $downloadsDir -Filter $Pattern -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($file) { return $file.FullName }
    return ""
}

function Get-TestType([string]$testName) {
    if ($testName -match "^Unitaria:") { return "Unitaria" }
    if ($testName -match "^TDD ") { return "Unitaria / TDD" }
    if ($testName -match "^Integral:") { return "Integral" }
    if ($testName -match "^Sistema:") { return "Sistema" }
    return "Otro"
}

function ConvertTo-ReportJson($value) {
    if ($null -eq $value) { return "Sin cuerpo" }
    return ($value | ConvertTo-Json -Depth 20 -Compress)
}

function Get-RelatedTestsForEndpoint([string]$method, [string]$path) {
    if ($path -like "*api/catalog/validate*") {
        return "Sistema: CatalogEndpointSystemTests.cs | Unitarias: ProductValidationTests.cs, TddIterationEvidenceTests.cs"
    }
    if ($path -like "*api/catalog*" -and $method -eq "GET") {
        return "Integral: ProductCatalogServiceIntegrationTests.cs | E2E simulado: App.e2e.test.tsx"
    }
    if ($path -like "*api/auth/login*") {
        return "Frontend/E2E: App.test.tsx, App.e2e.test.tsx"
    }
    if ($path -like "*api/cart/items*") {
        return "Frontend/E2E: App.test.tsx, App.e2e.test.tsx"
    }
    if ($path -like "*api/orders*") {
        return "Frontend/E2E: App.test.tsx, App.e2e.test.tsx"
    }
    if ($path -like "*api/payments/authorize*") {
        return "Frontend/E2E: App.test.tsx, App.e2e.test.tsx | Flujo real: pago y factura Docker"
    }

    return "E2E real Docker"
}

function Get-ValidationTypeForEndpoint([string]$method, [string]$path) {
    if ($path -like "*api/catalog/validate*") { return "Sistema + Unitarias" }
    if ($path -like "*api/catalog*" -and $method -eq "GET") { return "E2E real + Integral" }
    return "E2E real"
}

$backendResult = $null
$frontendResult = $null
$backendSummary = $null
$frontendSummary = $null
$realE2eReportCopy = ""
$realE2eCallsCopy = ""

if ($IncludeLatestRealE2eFromDownloads) {
    if ([string]::IsNullOrWhiteSpace($RealE2eReportPath)) {
        $RealE2eReportPath = Find-LatestDownload "real-e2e-report-*.md"
    }
    if ([string]::IsNullOrWhiteSpace($RealE2eCallsPath)) {
        $RealE2eCallsPath = Find-LatestDownload "real-e2e-calls-*.json"
    }
}

if (-not $SkipBackend) {
    $backendResult = Run-LoggedCommand `
        -Title "Backend CatalogService tests" `
        -WorkingDirectory $root `
        -LogFile (Join-Path $backendDir "console.log") `
        -Command {
            dotnet test backend/services/CatalogService/CatalogService.Tests/CatalogService.Tests.csproj `
                --logger "trx;LogFileName=catalog-tests.trx" `
                --logger "console;verbosity=detailed" `
                --results-directory $backendDir `
                --collect:"XPlat Code Coverage"
        }

    $trxPath = Join-Path $backendDir "catalog-tests.trx"
    $backendSummary = Parse-TrxSummary $trxPath
}

if (-not $SkipFrontend) {
    $frontendResult = Run-LoggedCommand `
        -Title "Frontend Vitest tests" `
        -WorkingDirectory (Join-Path $root "frontend") `
        -LogFile (Join-Path $frontendDir "console.log") `
        -Command {
            npm.cmd test -- --run
        }

    $frontendSummary = Parse-FrontendSummary $frontendResult.Output
}

if (-not [string]::IsNullOrWhiteSpace($RealE2eReportPath)) {
    if (Test-Path -LiteralPath $RealE2eReportPath) {
        $realE2eReportCopy = Join-Path $realE2eDir (Split-Path -Leaf $RealE2eReportPath)
        Copy-Item -LiteralPath $RealE2eReportPath -Destination $realE2eReportCopy -Force
    }
    else {
        Write-Warning "No se encontro el reporte E2E real: $RealE2eReportPath"
    }
}

if (-not [string]::IsNullOrWhiteSpace($RealE2eCallsPath)) {
    if (Test-Path -LiteralPath $RealE2eCallsPath) {
        $realE2eCallsCopy = Join-Path $realE2eDir (Split-Path -Leaf $RealE2eCallsPath)
        Copy-Item -LiteralPath $RealE2eCallsPath -Destination $realE2eCallsCopy -Force
    }
    else {
        Write-Warning "No se encontro el JSON E2E real: $RealE2eCallsPath"
    }
}

$checkoutReport = Join-Path $reportsDir "checkout-e2e-report.md"
$coverageFiles = Get-ChildItem -Path $backendDir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
$coverageLink = if ($coverageFiles.Count -gt 0) { Get-ReportRelativePath $coverageFiles[0].FullName } else { "" }

$backendStatus = if ($SkipBackend) { "SKIPPED" } elseif ($backendResult.ExitCode -eq 0) { "OK" } else { "FAILED" }
$frontendStatus = if ($SkipFrontend) { "SKIPPED" } elseif ($frontendResult.ExitCode -eq 0) { "OK" } else { "FAILED" }
$overallStatus = if (($backendStatus -eq "FAILED") -or ($frontendStatus -eq "FAILED")) { "FAILED" } else { "OK" }

$backendRows = ""
$unitTestCount = 0
$integrationTestCount = 0
$systemTestCount = 0
if ($backendSummary) {
    foreach ($test in $backendSummary.Tests) {
        $class = if ($test.Outcome -eq "Passed") { "pass" } else { "fail" }
        $testType = Get-TestType $test.Name
        if ($testType -like "Unitaria*") { $unitTestCount++ }
        if ($testType -eq "Integral") { $integrationTestCount++ }
        if ($testType -eq "Sistema") { $systemTestCount++ }
        $backendRows += "<tr><td>$(HtmlEncode $testType)</td><td>$(HtmlEncode $test.Name)</td><td class='$class'>$(HtmlEncode $test.Outcome)</td><td>$(HtmlEncode $test.Duration)</td></tr>`n"
    }
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$backendLogLink = if ($backendResult) { Get-ReportRelativePath $backendResult.LogFile } else { "" }
$frontendLogLink = if ($frontendResult) { Get-ReportRelativePath $frontendResult.LogFile } else { "" }
$trxPathForLink = Join-Path $backendDir "catalog-tests.trx"
$trxLink = if (Test-Path $trxPathForLink) { Get-ReportRelativePath $trxPathForLink } else { "" }
$checkoutLink = if (Test-Path $checkoutReport) { Get-ReportRelativePath $checkoutReport } else { "" }
$realE2eReportLink = if ($realE2eReportCopy -and (Test-Path $realE2eReportCopy)) { Get-ReportRelativePath $realE2eReportCopy } else { "" }
$realE2eCallsLink = if ($realE2eCallsCopy -and (Test-Path $realE2eCallsCopy)) { Get-ReportRelativePath $realE2eCallsCopy } else { "" }

$realE2eRows = ""
if ($realE2eCallsCopy -and (Test-Path $realE2eCallsCopy)) {
    try {
        $realCalls = Get-Content -LiteralPath $realE2eCallsCopy -Raw | ConvertFrom-Json
        foreach ($call in $realCalls) {
            $method = [string]$call.method
            $path = [string]$call.path
            $status = [string]$call.status
            $okClass = if ($call.ok) { "pass" } else { "fail" }
            $validationType = Get-ValidationTypeForEndpoint $method $path
            $relatedTests = Get-RelatedTestsForEndpoint $method $path
            $requestBody = HtmlEncode (ConvertTo-ReportJson $call.requestBody)
            $responseBody = HtmlEncode (ConvertTo-ReportJson $call.responseBody)

            $realE2eRows += "<tr><td>$(HtmlEncode $method)</td><td>$(HtmlEncode $path)</td><td class='$okClass'>$(HtmlEncode $status)</td><td>$(HtmlEncode $validationType)</td><td>$(HtmlEncode $relatedTests)</td><td><details><summary>Ver datos reales</summary><p><strong>Request</strong></p><pre>$requestBody</pre><p><strong>Response</strong></p><pre>$responseBody</pre></details></td></tr>`n"
        }
    }
    catch {
        $realE2eRows = "<tr><td colspan='6'>No fue posible leer el JSON E2E real: $(HtmlEncode $_.Exception.Message)</td></tr>"
    }
}
elseif ($realE2eReportLink) {
    $realE2eRows = "<tr><td colspan='6'>Se adjunto reporte Markdown E2E real, pero no se adjunto JSON de llamadas.</td></tr>"
}
else {
    $realE2eRows = "<tr><td colspan='6'>No se adjunto ejecucion E2E real. Usa -IncludeLatestRealE2eFromDownloads o -RealE2eCallsPath.</td></tr>"
}

$html = @"
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Reporte de pruebas</title>
  <style>
    :root { color-scheme: light; font-family: Inter, Segoe UI, Arial, sans-serif; }
    body { margin: 0; background: #f7f5f2; color: #201a17; }
    header { background: #4f3424; color: white; padding: 28px 36px; }
    main { max-width: 1180px; margin: 0 auto; padding: 28px; }
    h1, h2, h3 { margin: 0; }
    h1 { font-size: 30px; }
    h2 { font-size: 22px; margin: 28px 0 14px; }
    p { line-height: 1.55; }
    .muted { color: #756b64; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 16px; }
    .card { background: white; border: 1px solid #e2ded8; border-radius: 8px; padding: 18px; box-shadow: 0 1px 2px rgba(0,0,0,.04); }
    .metric { font-size: 34px; font-weight: 800; margin-top: 8px; }
    .status { display: inline-flex; align-items: center; border-radius: 999px; padding: 5px 10px; font-size: 13px; font-weight: 700; }
    .ok { background: #e5f8ed; color: #08623c; }
    .failed { background: #fde8e8; color: #9b1c1c; }
    .skipped { background: #eeeae5; color: #5b514a; }
    table { width: 100%; border-collapse: collapse; background: white; border: 1px solid #e2ded8; border-radius: 8px; overflow: hidden; }
    th, td { padding: 11px 12px; text-align: left; border-bottom: 1px solid #ebe7e1; font-size: 14px; vertical-align: top; }
    th { background: #eee9e2; font-weight: 700; }
    tr:last-child td { border-bottom: 0; }
    pre { max-width: 520px; overflow: auto; background: #f7f5f2; border: 1px solid #ebe7e1; border-radius: 6px; padding: 10px; white-space: pre-wrap; word-break: break-word; }
    summary { cursor: pointer; font-weight: 700; }
    .pass { color: #087443; font-weight: 700; }
    .fail { color: #a11d1d; font-weight: 700; }
    a { color: #68462f; font-weight: 700; text-decoration: none; }
    a:hover { text-decoration: underline; }
    code { background: #eee9e2; padding: 2px 5px; border-radius: 4px; }
  </style>
</head>
<body>
  <header>
    <h1>Reporte visual de pruebas</h1>
    <p>Generado: $generatedAt | Ejecucion: $runId | Estado general: <span class="status $(if ($overallStatus -eq "OK") { "ok" } else { "failed" })">$overallStatus</span></p>
  </header>
  <main>
    <section class="grid">
      <article class="card">
        <h3>Backend CatalogService</h3>
        <div class="metric">$(if ($backendSummary) { "$($backendSummary.Passed)/$($backendSummary.Total)" } else { "N/A" })</div>
        <p><span class="status $(if ($backendStatus -eq "OK") { "ok" } elseif ($backendStatus -eq "SKIPPED") { "skipped" } else { "failed" })">$backendStatus</span></p>
        <p class="muted">Unitarias: $unitTestCount | Integrales: $integrationTestCount | Sistema: $systemTestCount</p>
      </article>
      <article class="card">
        <h3>Frontend React</h3>
        <div class="metric">$(if ($frontendSummary) { "$($frontendSummary.Passed)/$($frontendSummary.Total)" } else { "N/A" })</div>
        <p><span class="status $(if ($frontendStatus -eq "OK") { "ok" } elseif ($frontendStatus -eq "SKIPPED") { "skipped" } else { "failed" })">$frontendStatus</span></p>
        <p class="muted">UI, flujos y E2E simulado.</p>
      </article>
      <article class="card">
        <h3>E2E simulado</h3>
        <div class="metric">$(if ($checkoutLink) { "SI" } else { "NO" })</div>
        <p class="muted">Compra con orden, pago y factura.</p>
      </article>
      <article class="card">
        <h3>E2E real Docker</h3>
        <div class="metric">$(if ($realE2eReportLink) { "SI" } else { "NO" })</div>
        <p class="muted">Payloads reales capturados desde el navegador.</p>
      </article>
    </section>

    <h2>Detalle backend</h2>
    <table>
      <thead><tr><th>Tipo</th><th>Prueba</th><th>Resultado</th><th>Duracion</th></tr></thead>
      <tbody>
        $backendRows
      </tbody>
    </table>

    <h2>Mapa E2E real y pruebas relacionadas</h2>
    <p class="muted">Esta tabla usa datos reales capturados desde el navegador y los relaciona con las pruebas automaticas que respaldan cada parte del flujo.</p>
    <table>
      <thead><tr><th>Metodo</th><th>Endpoint real</th><th>Status</th><th>Tipo validacion</th><th>Tests relacionados</th><th>Datos reales</th></tr></thead>
      <tbody>
        $realE2eRows
      </tbody>
    </table>

    <h2>Evidencias</h2>
    <table>
      <thead><tr><th>Tipo</th><th>Archivo</th></tr></thead>
      <tbody>
        <tr><td>Log backend</td><td><a href="$backendLogLink">$backendLogLink</a></td></tr>
        <tr><td>TRX backend</td><td>$(if ($trxLink) { "<a href='$trxLink'>$trxLink</a>" } else { "No generado" })</td></tr>
        <tr><td>Cobertura backend</td><td>$(if ($coverageLink) { "<a href='$coverageLink'>$coverageLink</a>" } else { "No generado" })</td></tr>
        <tr><td>Log frontend</td><td><a href="$frontendLogLink">$frontendLogLink</a></td></tr>
        <tr><td>Reporte E2E compra simulada</td><td>$(if ($checkoutLink) { "<a href='$checkoutLink'>$checkoutLink</a>" } else { "No generado" })</td></tr>
        <tr><td>Reporte E2E real Docker</td><td>$(if ($realE2eReportLink) { "<a href='$realE2eReportLink'>$realE2eReportLink</a>" } else { "No adjuntado" })</td></tr>
        <tr><td>JSON llamadas E2E real</td><td>$(if ($realE2eCallsLink) { "<a href='$realE2eCallsLink'>$realE2eCallsLink</a>" } else { "No adjuntado" })</td></tr>
      </tbody>
    </table>

    <h2>Como volver a generar</h2>
    <p>Ejecuta desde la raiz del proyecto:</p>
    <p><code>powershell -ExecutionPolicy Bypass -File scripts\run-test-report.ps1</code></p>
    <p>Para unificar tambien el reporte real descargado desde el navegador:</p>
    <p><code>powershell -ExecutionPolicy Bypass -File scripts\run-test-report.ps1 -RealE2eReportPath "C:\Users\LENOVO\Downloads\real-e2e-report-archivo.md" -RealE2eCallsPath "C:\Users\LENOVO\Downloads\real-e2e-calls-archivo.json"</code></p>
    <p>Para tomar automaticamente el ultimo E2E real de Descargas:</p>
    <p><code>powershell -ExecutionPolicy Bypass -File scripts\run-test-report.ps1 -IncludeLatestRealE2eFromDownloads</code></p>
  </main>
</body>
</html>
"@

$indexPath = Join-Path $reportsDir "index.html"
$html | Out-File -FilePath $indexPath -Encoding utf8

Write-Host ""
Write-Host "Reporte generado:"
Write-Host $indexPath

if ($overallStatus -eq "FAILED") {
    exit 1
}

exit 0
