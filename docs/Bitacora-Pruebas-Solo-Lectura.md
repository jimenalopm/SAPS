# Pruebas de la tabla de bitácora y del bloqueo de UPDATE/DELETE (Task 4, HU-004)

Criterios cubiertos: HU-004 c4 (campos del registro), c6 y c7 (solo lectura, sin omitir) y c8 (eventos futuros sin cambiar la tabla).

## Cómo se prueba

`soda.tb_Bitacora` se prueba contra **SQL Server real** (Docker local `saps-sqlserver`), porque InMemory y SQLite no ejecutan triggers ni permisos.
El proyecto `tests/SAPS.Bitacora.IntegrationTests`:

- Solo corre contra `localhost,1433`; si el servidor es otro, se niega a ejecutar.
- Crea una base nueva `SAPS_HU004_Pruebas_<guid>` con las migraciones reales (nivel de compatibilidad 110 = SQL Server 2012) y la elimina en `finally`. **Nunca toca `SAPS_DB` ni la BD de RecyPlast**, por eso las filas que deja la prueba no se acumulan (la bitácora no se puede borrar).
- Escribe con el servicio real `ServicioBitacora` (task 5).

```bash
# desde la raíz del repo; la contraseña de sa va por variable de entorno o user-secrets
export ConnectionStrings__DefaultConnection="Server=localhost,1433;User Id=sa;Password=<...>;TrustServerCertificate=True;"
dotnet run --project tests/SAPS.Bitacora.IntegrationTests -- SAPS.Web/appsettings.json
```

## Resultados

Salida completa: `docs/evidencias/task4/salida-pruebas.txt` (29 verificaciones correctas).

| # | Prueba | Esperado | Obtenido |
|---|---|---|---|
| 1 | Estructura: 16 columnas, tipos, largos, nulabilidad e IDENTITY | Igual al diseño | OK |
| 2 | Sin FK hacia `AspNetUsers` y sin CHECK sobre `Modulo`/`TipoEvento` | 0 y 0 | OK |
| 3 | Trigger `tr_BitacoraSoloLectura` (INSTEAD OF) habilitado; `DENY UPDATE, DELETE` a `public` | Existen | OK |
| 4 | Servicio escribe inicio de sesión fallido y un evento nuevo (`OrdenAnulada`) | 2 filas; mismas columnas antes y después | OK |
| 5 | `FechaHoraUtc` (UTC) y `UsuarioBd` (`SUSER_SNAME()`) los asigna la BD | Correctos | OK |
| 6 | `UPDATE` y `DELETE` por T-SQL como `sa` | Error 50001 | OK |
| 7 | `SaveChanges` (modificar y eliminar), `ExecuteUpdate`, `ExecuteDelete` por EF | Error 50001; conteo sin cambio | OK |
| 8 | Usuario normal de la BD con `GRANT` explícito de UPDATE/DELETE | `INSERT` y `SELECT` permitidos; `UPDATE`, `DELETE` denegados (229); `TRUNCATE` denegado | OK |

## Qué agrega respecto a la task 3

La captura de `docs/evidencias/task3/bloqueo-solo-lectura.png` probó a mano, como `sa` y en `SAPS_DB`, que el trigger existe y rechaza UPDATE/DELETE. Esta task agrega: prueba repetible y automática en una base desechable, el camino real de la aplicación (EF), la segunda capa de permisos (el `DENY` prevalece aunque haya `GRANT`), el `TRUNCATE`, la escritura por el servicio y la estructura de la tabla.

## Límite conocido (confirmar con el líder técnico)

`sa` o `dbo` **sí pueden** `TRUNCATE TABLE` (no dispara triggers) o desactivar el trigger. La aplicación debe conectarse con una cuenta sin `db_owner` ni `sysadmin` (ver sección 7 del diseño).

## Pendientes para el líder técnico

- **IP del cliente detrás de un proxy.** El servicio guarda `HttpContext.Connection.RemoteIpAddress`. Si RecyPlast publica la aplicación detrás de IIS o un proxy inverso, se registraría la IP del proxy. Requiere configurar `UseForwardedHeaders` con la lista de proxys confiables en `Program.cs`; no se agregó para no confiar en `X-Forwarded-For` sin esa lista (se podría falsificar la IP).
- Los cierres de sesión forzados por el middleware (cuenta desactivada o contraseña vencida) y el 2FA no se registran: quedaron fuera de los criterios de HU-004.
