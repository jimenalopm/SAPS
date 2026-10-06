# SAPS.FunctionalTests — Pruebas funcionales del Sprint 1 (Selenium WebDriver)

Pruebas de aceptación de **HU-001, HU-003, HU-007 y HU-008** que manejan Chrome como lo haría una persona usuaria.
Están en un proyecto aparte para que `dotnet test SAPS.Tests` **no abra navegador**. (Ojo: `dotnet test` sobre la
solución completa sí correría estas; use siempre la ruta de cada proyecto.)

## Requisitos
- App corriendo contra la BD real: `dotnet run --project SAPS.Web` (por defecto `http://localhost:5120`).
- Google Chrome instalado (Selenium Manager descarga el driver solo; la primera vez necesita internet).
- .NET 10 SDK.

## Variables de entorno (PowerShell, en la MISMA ventana donde correrá las pruebas)
| Variable | Qué es | Obligatoria |
|---|---|---|
| `SAPS_ADMIN_CORREO`, `SAPS_ADMIN_CLAVE` | Cuenta real del Administrador | Sí |
| `SAPS_RH_CORREO`, `SAPS_RH_CLAVE` | Cuenta real de Recursos Humanos | Sí |
| `SAPS_SODA_CORREO`, `SAPS_SODA_CLAVE` | Cuenta real de la usuaria de Soda | Sí |
| `SAPS_COLAB_ACTIVO` | Código de un colaborador real **activo** (solo se consulta) | Sí (HU-008) |
| `SAPS_COLAB_INACTIVO` | Código de un colaborador real **inactivo** (solo se consulta) | No: si falta, esa prueba se omite |
| `SAPS_URL` | URL base de la app | No (por defecto `http://localhost:5120`) |
| `SAPS_HEADLESS` | `1` = Chrome sin ventana | No |

```powershell
$env:SAPS_ADMIN_CORREO = "..."   # asignar los valores en la consola; nunca en archivos del repo
```
Si falta una variable obligatoria, la prueba falla con el mensaje `Falta la variable de entorno SAPS_...`.

## Cómo correrlas
```powershell
cd SAPS.FunctionalTests
.\Ejecutar-PruebasFuncionales.ps1                  # las 4 HU seguidas (pide confirmar: HU007/HU008 escriben en la BD)
.\Ejecutar-PruebasFuncionales.ps1 -Hu HU001,HU003  # solo las que no escriben datos
```
O una sola HU a mano (método del curso):
```powershell
dotnet test SAPS.FunctionalTests --filter "FullyQualifiedName~HU001" --logger "html;LogFileName=ReporteFuncional_HU001.html"
```

## Dónde quedan los resultados
- **Reportes HTML:** `SAPS.FunctionalTests\Reportes\ReporteFuncional_HU00x.html` (con el script) o `...\TestResults\` (con el comando manual).
- **Capturas:** `SAPS.FunctionalTests\Evidencias\HU001|HU003|HU007|HU008\` — un PNG por paso clave, nombrado `<prueba>__<n>_<paso>.png`.
- Ambas carpetas están en `.gitignore`: **no suba capturas ni reportes a GitHub** (muestran correos y colaboradores reales).

## Seguridad: qué escriben (y qué no) en la BD real
- HU-001 y HU-003 **no guardan nada**. Credenciales inválidas usan `noexiste@recyplast.cr`; el bloqueo de cuenta no se prueba.
- Colaboradores reales: **solo consulta**.
- HU-007 crea productos `ZZ_SELENIUM_<fecha-hora>` (y cada prueba los desactiva al terminar). Nunca toca productos, bebidas ni precios reales.
- HU-007 (cambio de precio vs. pedidos) y HU-008 (registro) guardan pedidos **solo para `DEMO001`**.
- Scripts SQL (no se ejecutan solos, en `Sql\`):
  - `crear_colaborador_demo.sql` — crea DEMO001 si no existe.
  - `limpieza_pruebas.sql` — borra ZZ_SELENIUM_%, los pedidos de DEMO001 y DEMO001. Por defecto es un ensayo (`@Confirmar = 0`).
  - `verificar_pedido_demo.sql` — consulta de solo lectura: pedido, usuaria que lo registró y detalle (evidencia de trazabilidad).

## Pruebas omitidas (visibles en el reporte con su motivo)
Funcionalidad que no existe todavía en SAPS.Web: estado laboral en login, primer ingreso, contraseña vencida, bitácora (login, roles, precios),
gestión de usuarios/roles. Bloqueo de cuenta: excluido a propósito (lo cubren las unitarias). Colaborador inactivo: se omite si falta `SAPS_COLAB_INACTIVO`.
