param(
    [string]$Customer = "",
    [string]$Email = "",
    [string]$Product = "",
    [string]$Invoice = "",
    [string]$Total = "",
    [int]$LogTail = 180
)

$ErrorActionPreference = "Continue"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$reportsDir = Join-Path $root "reports"
$manualDir = Join-Path $reportsDir "manual-e2e"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $manualDir $runId

New-Item -ItemType Directory -Force -Path $runDir | Out-Null

function Read-OrDefault([string]$Label, [string]$Value) {
    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $inputValue = Read-Host $Label
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return "No registrado"
    }

    return $inputValue
}

function Run-LogCommand {
    param(
        [string]$Name,
        [string[]]$Args,
        [string]$OutputPath
    )

    Push-Location $root
    try {
        $output = & docker @Args 2>&1
        $output | Out-File -FilePath $OutputPath -Encoding utf8
    }
    finally {
        Pop-Location
    }
}

$customerValue = Read-OrDefault "Cliente mostrado en pantalla" $Customer
$emailValue = Read-OrDefault "Correo mostrado en pantalla" $Email
$productValue = Read-OrDefault "Producto comprado" $Product
$invoiceValue = Read-OrDefault "Factura generada" $Invoice
$totalValue = Read-OrDefault "Total pagado" $Total

$allLogsPath = Join-Path $runDir "docker-services.log"
$purchaseLogsPath = Join-Path $runDir "purchase-services.log"

Run-LogCommand `
    -Name "Todos los servicios" `
    -Args @("compose", "logs", "--tail=$LogTail") `
    -OutputPath $allLogsPath

Run-LogCommand `
    -Name "Servicios de compra" `
    -Args @("compose", "logs", "--tail=$LogTail", "gateway", "authservice", "catalogservice", "cartservice", "orderservice", "paymentservice") `
    -OutputPath $purchaseLogsPath

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$reportPath = Join-Path $reportsDir "manual-e2e-report.md"
$runReportPath = Join-Path $runDir "manual-e2e-report.md"

$content = @"
# Reporte manual E2E de compra

Fecha de ejecucion: $generatedAt

## Contexto

Este reporte corresponde a una validacion manual realizada desde el navegador usando el frontend local y los servicios levantados con Docker.

Flujo esperado:

```text
Frontend http://localhost:5173
Gateway http://localhost:9090
Microservicios Docker
PostgreSQL Docker
```

## Datos observados en pantalla

- Cliente: $customerValue
- Correo: $emailValue
- Producto comprado: $productValue
- Factura: $invoiceValue
- Total pagado: $totalValue

## Flujo validado manualmente

- Se abrio el frontend local.
- Se inicio sesion como cliente.
- Se cargo el catalogo.
- Se agrego producto al carrito.
- Se realizo el pago.
- Se genero factura visible en pantalla.

## Evidencia tecnica

- Logs generales Docker: `manual-e2e/$runId/docker-services.log`
- Logs del flujo de compra: `manual-e2e/$runId/purchase-services.log`
- Copia de este reporte: `manual-e2e/$runId/manual-e2e-report.md`

## Como comprobar en DevTools

En Chrome DevTools, pestana `Network`, filtro `api`, se deben observar llamadas como:

```text
GET  http://localhost:9090/api/catalog
POST http://localhost:9090/api/auth/login
POST http://localhost:9090/api/cart/items
POST http://localhost:9090/api/orders
POST http://localhost:9090/api/payments/authorize
```

Resultado esperado:

- Respuestas `200` o `201`.
- Factura visible en el frontend.
- Sin errores `500`.
- Sin errores CORS.
"@

$content | Out-File -FilePath $reportPath -Encoding utf8
$content | Out-File -FilePath $runReportPath -Encoding utf8

Write-Host ""
Write-Host "Reporte manual generado:"
Write-Host $reportPath
Write-Host ""
Write-Host "Evidencias de esta ejecucion:"
Write-Host $runDir
