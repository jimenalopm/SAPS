# Base de datos: estándares IS-011 y compatibilidad con SQL Server 2012

El servidor de RecyPlast tiene **Windows Server 2012 + SQL Server 2012 Express (11.0)**.
Por eso un `.bak` hecho en el Docker (SQL Server 2022) no se puede restaurar ahí;
la base se crea con un script generado desde las migraciones de EF.

## Cambios de esta rama

- El mapeo de EF (`Data/ApplicationDbContext.cs`, `Data/PedidoConfiguration.cs`) sigue
  los estándares EBD01–EBD13. **Las propiedades C# no cambiaron**, solo los nombres en la BD.
- `UseCompatibilityLevel(110)` en `Program.cs` y en `ApplicationDbContextFactory`:
  evita que EF use `OPENJSON` (no existe en 2012), p. ej. en `tamanosAsignados.Contains(...)`.
- Cadena de conexión con `Encrypt=False;TrustServerCertificate=True;`.
- Se eliminaron las migraciones anteriores: los datos eran de prueba y se regenera
  una migración inicial única con los nombres nuevos.
- `seed_catalogo.sql`, pruebas y documentación actualizados a los nombres nuevos.

| Antes | Ahora |
|---|---|
| `dbo.tb_*` | `soda.tb_*` (EBD13) |
| `nombreCategoria`, `nombreTamano`, `nombreProducto`, `nombreBebida` | `Nombre` |
| `tipoBebida` | `Tipo` |
| `tb_Precio.precio` | `tb_Precio.Monto` (EBD10: no repetir el nombre de la tabla) |
| `tb_Bebida.precio` | `Precio` |
| `activo` / `esEspecial` / `requiereTamano` | `EstaActivo` / `EsEspecial` / `TieneTamano` |
| `IdPedido`, `IdDetallePedido`, `IdPrecio`, `IdBebida` | `idPedido`, `idDetallePedido`, `idPrecio`, `idBebida` (EBD03/04) |
| `IdUsuarioRegistro` | `idUsuario` |
| `nvarchar` en textos del sistema | `varchar` (EBD11) |
| `PK_tb_*`, `FK_tb_..._tb_..._Id*`, `IX_tb_*` | `PK_<Tabla>`, `FK_<Origen>_<Destino>`, `IX_<Tabla>_<Campo>`, `UQ_`, `CK_` (EBD07/08) |

Excepciones documentadas:
- Tablas `AspNet*` (ASP.NET Identity): quedan en `dbo` con los nombres del framework.
- `idUsuario` es `nvarchar(450)` porque debe coincidir con `AspNetUsers.Id`.
- `Total` y `Subtotal` se mantienen `bigint`: son montos acumulados (la app calcula con
  `checked` y no hay tope de consumo); EBD11 pide `INT` para precios unitarios, que sí lo son.

## Pasos (una sola vez, en la máquina de desarrollo)

```powershell
cd SAPS.Web
# 1. Regenerar la migración inicial con los nombres nuevos
dotnet ef migrations add InicialEstandares
# 2. Recrear la base local del Docker (los datos eran de prueba)
dotnet ef database drop -f
dotnet ef database update
# 3. Generar el script para el servidor de RecyPlast
dotnet ef migrations script --idempotent -o ..\db\SAPS_DB_servidor.sql
```

Hacer commit de la carpeta `Migrations` nueva y de `db/SAPS_DB_servidor.sql`.

## En el servidor (SQL Server 2012)

```
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE DATABASE SAPS_DB"
sqlcmd -S .\SQLEXPRESS -E -I -d SAPS_DB -i SAPS_DB_servidor.sql
```

`-I` activa `QUOTED_IDENTIFIER`, necesario para los índices filtrados.
Para el catálogo de ejemplo (opcional): `sqlcmd ... -f 65001 -i seed_catalogo.sql`
(`-f 65001` para que lea bien las tildes).

Cambios futuros al modelo: crear la migración como siempre y generar el script con
`dotnet ef migrations script --idempotent`; se puede volver a ejecutar completo en el
servidor porque solo aplica lo que falta.
