# Pruebas manuales del trigger de precios (Task 17, HU-015)

Pruebas hechas en la BD local de desarrollo (Docker `saps-sqlserver`, base `SAPS_DB`) con `sqlcmd -I`.
Triggers probados: `soda.tr_AuditoriaPrecio` (tb_Precio) y `soda.tr_AuditoriaBebida` (tb_Bebida).

> Nota: `sqlcmd` se abre con la opción `-I` (QUOTED_IDENTIFIER ON), porque tb_Precio tiene un índice filtrado. La aplicación (EF Core) ya lo activa por defecto.

## Resultados

| # | Prueba | Resultado esperado | Resultado obtenido |
|---|--------|--------------------|--------------------|
| 1 | `UPDATE` directo del Monto (idPrecio 1, 1100 -> 1200 -> 1300) | 1 registro `CambioPrecio` por cambio | OK: bitácora 2 (1100 -> 1200) y 3 (1200 -> 1300), usuario `sa` |
| 2a | Cerrar precio viejo (`EstaActivo = 0`) | No se registra nada | OK: sin registro nuevo |
| 2b | `INSERT` del precio nuevo (idPrecio 3002, Monto 1350) | 1 registro con anterior 1300 y nuevo 1350 | OK: bitácora 4 (1300 -> 1350) |
| 3 | Cambio con `CONTEXT_INFO` (`prueba-id-001\|ADM001\|Administrador`) | Usuario y rol de la app en el registro | OK: bitácora 5 (1350 -> 1360), Nombre `ADM001`, Rol `Administrador`, UsuarioBd `sa` |
| 4 | `UPDATE` de `tb_Bebida.Precio` (idBebida 1) | Registro con Recurso `tb_Bebida` | OK: bitácora 6 (1000 -> 1020) |

## Consulta de resumen

```sql
SELECT idBitacora, Recurso, IdRegistro, LEFT(NombreUsuario,10) AS Usuario,
       LEFT(Rol,15) AS Rol, UsuarioBd, LEFT(ValorAnterior,8) AS Anterior, LEFT(ValorNuevo,8) AS Nuevo
FROM soda.tb_Bitacora
WHERE TipoEvento = 'CambioPrecio'
ORDER BY idBitacora;
```

Salida obtenida:

| idBitacora | Recurso | IdRegistro | Usuario | Rol | UsuarioBd | Anterior | Nuevo |
|---|---|---|---|---|---|---|---|
| 2 | tb_Precio | 1 | sa | NULL | sa | 1100 | 1200 |
| 3 | tb_Precio | 1 | sa | NULL | sa | 1200 | 1300 |
| 4 | tb_Precio | 3002 | sa | NULL | sa | 1300 | 1350 |
| 5 | tb_Precio | 3002 | ADM001 | Administrador | sa | 1350 | 1360 |
| 6 | tb_Bebida | 1 | sa | NULL | sa | 1000 | 1020 |

## Conclusiones

- Un cambio de precio produce **un solo** registro (cerrar el precio viejo no registra nada).
- Sin `CONTEXT_INFO` el usuario queda como el de la conexión a la BD (`sa`); con `CONTEXT_INFO` se guarda el usuario y rol de la aplicación.
- Los datos de prueba quedan en la BD local de desarrollo (la bitácora es de solo lectura y no se pueden borrar). No afectan la BD de RecyPlast.

## Evidencias (capturas)

Guardar en `docs/evidencias/task17/`:

1. `01-precio-actual.png` - precio actual antes del cambio.
2. `02-cambio.png` - el `UPDATE` / `INSERT` del cambio.
3. `03-registro-bitacora.png` - la consulta de resumen con los registros.
