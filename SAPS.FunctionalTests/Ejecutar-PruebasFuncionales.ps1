<#
.SYNOPSIS
    Corre las pruebas funcionales (Selenium) de las 4 HU del Sprint 1 una tras otra y genera un reporte HTML por HU.

.DESCRIPTION
    Equivale a ejecutar, para cada HU:
      dotnet test SAPS.FunctionalTests --filter "FullyQualifiedName~HU001" --logger "html;LogFileName=ReporteFuncional_HU001.html"
    Los reportes quedan en SAPS.FunctionalTests\Reportes y las capturas en SAPS.FunctionalTests\Evidencias\HUxxx.
    La app debe estar corriendo (dotnet run en SAPS.Web) y las variables de entorno configuradas (ver README.md).

.PARAMETER Hu
    HU a correr. Por defecto las cuatro: HU001, HU003, HU007, HU008.

.PARAMETER SinConfirmacion
    No pide confirmación antes de correr (HU007 y HU008 escriben en la BD real).

.EXAMPLE
    .\Ejecutar-PruebasFuncionales.ps1
    .\Ejecutar-PruebasFuncionales.ps1 -Hu HU001,HU003      # solo las HU que no escriben datos
#>
param(
    [string[]]$Hu = @('HU001', 'HU003', 'HU007', 'HU008'),
    [switch]$SinConfirmacion
)

$ErrorActionPreference = 'Stop'
$proyecto = $PSScriptRoot
$reportes = Join-Path $proyecto 'Reportes'

# 1) Revisar que estén las variables de entorno (solo se muestra el NOMBRE, nunca el valor).
$requeridas = 'SAPS_ADMIN_CORREO', 'SAPS_ADMIN_CLAVE', 'SAPS_RH_CORREO', 'SAPS_RH_CLAVE',
              'SAPS_SODA_CORREO', 'SAPS_SODA_CLAVE', 'SAPS_COLAB_ACTIVO'
$faltantes = $requeridas | Where-Object { -not [Environment]::GetEnvironmentVariable($_) }
if ($faltantes) {
    Write-Host "Faltan variables de entorno: $($faltantes -join ', ')" -ForegroundColor Red
    Write-Host 'Configúrelas en esta misma ventana de PowerShell (ver README.md) y vuelva a correr el script.'
    exit 1
}
if (-not $env:SAPS_COLAB_INACTIVO) {
    Write-Host 'Aviso: SAPS_COLAB_INACTIVO no está definida; la prueba de colaborador inactivo (HU008) se omitirá.' -ForegroundColor Yellow
}
$url = if ($env:SAPS_URL) { $env:SAPS_URL } else { 'http://localhost:5120 (valor por defecto)' }
Write-Host "URL de la app: $url"

# 2) Advertir que HU007 y HU008 escriben en la BD real.
if (-not $SinConfirmacion -and ($Hu -contains 'HU007' -or $Hu -contains 'HU008')) {
    Write-Host ''
    Write-Host 'ATENCIÓN: HU007 y HU008 ESCRIBEN en la base de datos REAL (productos ZZ_SELENIUM_* y pedidos de DEMO001).' -ForegroundColor Yellow
    Write-Host 'Corra esas HU una sola vez y luego ejecute Sql\limpieza_pruebas.sql.'
    if ((Read-Host 'Escriba SI para continuar') -ne 'SI') { Write-Host 'Cancelado.'; exit 0 }
}

New-Item -ItemType Directory -Force -Path $reportes | Out-Null

# 3) Compilar una sola vez.
dotnet build $proyecto --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'La compilación falló.' -ForegroundColor Red; exit $LASTEXITCODE }

# 4) Correr cada HU. Si una falla, se sigue con las demás y se resume al final.
$resumen = @()
foreach ($h in $Hu) {
    Write-Host ''
    Write-Host "=== $h ===" -ForegroundColor Cyan
    dotnet test $proyecto --no-build --filter "FullyQualifiedName~$h" `
        --logger "html;LogFileName=ReporteFuncional_$h.html" --results-directory $reportes
    $resumen += [pscustomobject]@{ HU = $h; Resultado = $(if ($LASTEXITCODE -eq 0) { 'OK' } else { 'CON FALLAS' }); Reporte = "Reportes\ReporteFuncional_$h.html" }
}

Write-Host ''
$resumen | Format-Table -AutoSize
Write-Host "Reportes:  $reportes"
Write-Host "Capturas:  $(Join-Path $proyecto 'Evidencias')"
if ($resumen | Where-Object { $_.Resultado -ne 'OK' }) { exit 1 }
