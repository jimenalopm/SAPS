# Diseño de la tabla de bitácora (HU-004 y HU-015)

Sprint 2 · Task 1 (Design) · Responsable: Jimena · Estándares: IS-011 (EBD01–EBD13)

Este documento define la tabla única de bitácora de SAPS. Sirve de base para la
implementación (task 2), el bloqueo de UPDATE/DELETE (task 3), el servicio de logs,
la pantalla de bitácora y el trigger de precios (task 16).

## 1. Decisiones de diseño

| # | Decisión | Justificación |
|---|---|---|
| D1 | **Una sola tabla** genérica `soda.tb_Bitacora` para todos los eventos. | HU-004 criterio 8: los eventos futuros (p. ej. anulación de órdenes, HU-010) usan los mismos campos sin cambiar la estructura. |
| D2 | Esquema **`soda`** (EBD13). | Los eventos de este sprint (seguridad y catálogo) pertenecen a la gestión de la soda. `rrhh` es solo para planillas y colaboradores. |
| D3 | El **módulo, el tipo de evento y el recurso** son texto, **sin CHECK sobre sus valores**. | Un CHECK con la lista de valores obligaría a alterar la tabla al agregar un módulo o evento nuevo. Los valores válidos se controlan en el servicio de logs (constantes en C#). |
| D4 | **Sin llave foránea** hacia `AspNetUsers`. | Un intento fallido puede ser de un usuario que no existe, y un log nunca debe impedir ni verse afectado por cambios en las cuentas. Se guarda una copia (snapshot) del nombre y del rol. |
| D5 | La fecha y hora se guardan en **UTC** (`SYSUTCDATETIME()`), con el valor por defecto definido en la BD. | Coincide con `tb_Pedido.FechaRegistroUtc`. Como el default está en la BD, el trigger y la aplicación escriben la hora con la misma regla. |
| D6 | El resultado es un campo **`EsExitoso` (BIT)**. | EBD10 (booleanos con prefijo Es/Esta/Tiene) y EBD11 (BIT para booleanos). La pantalla muestra "Exitoso" o "Fallido". |
| D7 | Los valores anterior y nuevo son **texto** (`VARCHAR(500)`). | Sirven para precios, roles y cualquier evento futuro sin crear columnas por tipo de dato. |

## 2. Definición de la tabla

**Nombre:** `soda.tb_Bitacora` (EBD02, EBD13) · **PK:** `PK_Bitacora` (EBD08)

| Campo | Tipo (EBD11) | Nulo | Descripción | Criterio que cubre |
|---|---|---|---|---|
| `idBitacora` | INT IDENTITY | No | Llave primaria (EBD03). | — |
| `FechaHoraUtc` | DATETIME2 | No | Fecha y hora del evento, en UTC. Default `SYSUTCDATETIME()`. | HU-004 c1, c2, c3 · HU-015 c3 |
| `Modulo` | VARCHAR(30) | No | Módulo afectado: `Seguridad` o `Catalogo` (a la fecha). | HU-004 c4 |
| `TipoEvento` | VARCHAR(50) | No | Código del evento (ver sección 3). Es el campo por el que filtra la pantalla. | HU-004 c8 · HU-015 c6 |
| `Recurso` | VARCHAR(100) | No | Entidad o tabla involucrada (p. ej. `tb_Precio`, `AspNetUsers`, `Sesion`). | HU-004 c4 |
| `IdRegistro` | VARCHAR(50) | Sí | Identificador del registro involucrado (texto, porque puede ser un INT o el Id de una cuenta). | HU-004 c3, c4 · HU-015 c3 |
| `EsExitoso` | BIT | No | 1 = operación exitosa, 0 = fallida. | HU-004 c1, c4 |
| `idUsuario` | NVARCHAR(450) | Sí | Id de la cuenta que realizó la acción, si existe (mismo tipo que `AspNetUsers.Id`). Sin FK (D4). | HU-004 c1, c2, c3 |
| `NombreUsuario` | VARCHAR(256) | Sí | Copia del nombre de usuario o código de colaborador al momento del evento. En un intento fallido con cuenta inexistente, lo escrito en el login solo si tiene forma de correo; si no, `(usuario no reconocido)` (podría ser una contraseña). | HU-004 c1, c2 · HU-015 c3 |
| `Rol` | VARCHAR(50) | Sí | Copia del rol de la cuenta en ese momento. | HU-004 c1 |
| `UsuarioBd` | VARCHAR(128) | No | Usuario de SQL Server que insertó la fila. Default `SUSER_SNAME()`. En cambios directos en la BD es quien hizo el cambio. | HU-015 c2 |
| `DireccionIp` | VARCHAR(45) | Sí | IP del cliente (45 caracteres alcanzan para IPv6). Nula si el cambio fue directo en la BD. | HU-004 c1 |
| `Dispositivo` | VARCHAR(200) | Sí | Dispositivo o navegador (User-Agent resumido). Nulo si el cambio fue directo en la BD. | HU-004 c1 |
| `ValorAnterior` | VARCHAR(500) | Sí | Valor antes del cambio (precio anterior, rol anterior). | HU-004 c2, c3 · HU-015 c3 |
| `ValorNuevo` | VARCHAR(500) | Sí | Valor después del cambio. | HU-004 c2, c3 · HU-015 c3 |
| `Descripcion` | VARCHAR(300) | Sí | Texto legible del evento. Para altas, bajas y cambios de rol incluye la cuenta afectada (p. ej. "Cuenta cocina01: rol Personal de cocina → Administrador"). | HU-004 c3 |

> **Intentos fallidos de inicio de sesión:** `IdUsuario` queda vacío (nadie hizo la acción); la cuenta afectada va en `IdRegistro` y `NombreUsuario`. **Limitación conocida:** si quien intenta ya tiene una sesión abierta en ese navegador, `ServicioBitacora` rellena `IdUsuario` con esa sesión.

### Restricciones e índices (EBD07, EBD08)

| Nombre | Tipo | Detalle |
|---|---|---|
| `PK_Bitacora` | PK | `idBitacora` |
| `DF_Bitacora_FechaHoraUtc` | Default | `SYSUTCDATETIME()` |
| `DF_Bitacora_UsuarioBd` | Default | `SUSER_SNAME()` |
| `CK_Bitacora_Modulo` | — | **No se crea** (D3). |
| `IX_Bitacora_FechaHoraUtc` | Índice | `FechaHoraUtc DESC`: la pantalla muestra primero lo más reciente. |
| `IX_Bitacora_TipoEvento_FechaHoraUtc` | Índice | Filtro por tipo de evento (HU-015 c6). |
| `IX_Bitacora_idUsuario` | Índice | Consulta de la actividad de una cuenta. |

Las propiedades C# se nombran en PascalCase (EP05) y la entidad se llamará `Bitacora`, igual que se hizo con el resto del modelo.

## 3. Catálogo inicial de eventos

| Módulo | TipoEvento | Recurso | Cuándo | Valores anterior / nuevo |
|---|---|---|---|---|
| Seguridad | `InicioSesion` | `Sesion` | Inicio de sesión (`EsExitoso` = 1 o 0). | — |
| Seguridad | `CierreSesion` | `Sesion` | Cierre de sesión. | — |
| Seguridad | `CuentaCreada` | `AspNetUsers` | El Administrador crea una cuenta (HU-003). | — / rol asignado |
| Seguridad | `CuentaDesactivada` | `AspNetUsers` | El Administrador desactiva una cuenta. | activa / inactiva |
| Seguridad | `CuentaReactivada` | `AspNetUsers` | El Administrador reactiva una cuenta desactivada (agregado en la task 7). | inactiva / activa |
| Seguridad | `RolCambiado` | `AspNetUsers` | El Administrador cambia el rol de una cuenta. | rol anterior / rol nuevo |
| Catalogo | `CambioPrecio` | `tb_Precio` | Se modifica un precio (HU-007, HU-015), desde la pantalla o directo en la BD. | precio anterior / precio nuevo |
| *(futuro)* | `OrdenAnulada` | `tb_Pedido` | HU-010 (Sprint 4). | Misma estructura, sin cambios en la tabla. |

En `IdRegistro` va el id de la cuenta afectada (`AspNetUsers`), el id del precio (`tb_Precio`) o el id de la sesión, según el evento. Para saber qué producto cambió de precio, `Descripcion` incluye el nombre del producto y el tamaño.

## 4. Cobertura de los criterios de aceptación

| Criterio | Cómo lo cubre este diseño |
|---|---|
| HU-004 c1 (sesión) | `FechaHoraUtc`, `NombreUsuario`, `Rol`, `Dispositivo`, `DireccionIp`, `EsExitoso`. |
| HU-004 c2 (precio) | `ValorAnterior`, `ValorNuevo`, `Recurso`/`IdRegistro`/`Descripcion` (producto), usuario y fecha. |
| HU-004 c3 (cuentas) | `Recurso` + `IdRegistro` + `Descripcion` (cuenta afectada), `idUsuario`/`NombreUsuario` (quien hizo el cambio), `FechaHoraUtc`. |
| HU-004 c4 (módulo y recurso) | `Modulo`, `Recurso`, `IdRegistro`, `EsExitoso`; todos obligatorios salvo `IdRegistro`. |
| HU-004 c5 (solo Administrador) | Pantalla y rol: tasks 11 y 13 (Nataly). No depende de la tabla. |
| HU-004 c6 y c7 (solo lectura, sin omitir) | Task 3 (ver sección 5). El servicio de logs (task 5) es el único camino de escritura de la aplicación. |
| HU-004 c8 (eventos futuros) | D1, D3 y D7: un evento nuevo es una fila nueva con un `TipoEvento` nuevo. |
| HU-015 c2 y c3 (trigger) | `UsuarioBd` (default `SUSER_SNAME()`), `FechaHoraUtc`, `ValorAnterior`, `ValorNuevo`. |
| HU-015 c5 (solo lectura) | Task 3. |
| HU-015 c6 (filtro y detalle) | `IX_Bitacora_TipoEvento_FechaHoraUtc`, `TipoEvento = 'CambioPrecio'`, `ValorAnterior`/`ValorNuevo`. |

## 5. Notas para las tasks siguientes

- **Task 2 (tabla y migración):** crear la entidad, la configuración en `ApplicationDbContext` (con los nombres de esta sección) y la migración. Regenerar `db/SAPS_DB_servidor.sql`.
- **Task 3 (solo lectura):** `DENY UPDATE, DELETE` no alcanza, porque no aplica al propietario de la BD ni a `sysadmin`. Se propone complementarlo con un trigger `tr_BitacoraSoloLectura` (EBD09) `INSTEAD OF UPDATE, DELETE` que rechace la operación para cualquier usuario, incluido el Administrador. Considerar también `TRUNCATE TABLE`, que no dispara triggers.
- **Task 16 (trigger de precios):**
  - `Precio.cs` indica que al cambiar un precio se **cierra la fila anterior** (UPDATE de `FechaVigenciaHasta` y `EstaActivo`) y se **inserta una fila nueva**. Hay que definir si el trigger detecta el cambio en el INSERT de la fila nueva (comparando con la fila que cierra) o en el UPDATE de `Monto`.
  - Los precios de las bebidas están en `tb_Bebida.Precio`, no en `tb_Precio`. Confirmar si el trigger también debe cubrirla.
  - Si la pantalla registra el cambio desde C# (task 18) **y** el trigger también lo registra, un mismo cambio queda dos veces. Hay que acordar con Tiber y Dylan cuál de los dos escribe.
  - El servidor de RecyPlast usa **SQL Server 2012**: no existe `SESSION_CONTEXT` (es de 2016); se puede usar `CONTEXT_INFO` si la aplicación necesita pasar el usuario al trigger. No usar `DROP ... IF EXISTS`.

## 6. Referencia: script T-SQL equivalente (borrador)

La migración de EF (task 2) generará algo equivalente a esto:

```sql
CREATE TABLE soda.tb_Bitacora (
    idBitacora    INT IDENTITY(1,1) NOT NULL,
    FechaHoraUtc  DATETIME2 NOT NULL CONSTRAINT DF_Bitacora_FechaHoraUtc DEFAULT SYSUTCDATETIME(),
    Modulo        VARCHAR(30)  NOT NULL,
    TipoEvento    VARCHAR(50)  NOT NULL,
    Recurso       VARCHAR(100) NOT NULL,
    IdRegistro    VARCHAR(50)  NULL,
    EsExitoso     BIT          NOT NULL,
    idUsuario     NVARCHAR(450) NULL,
    NombreUsuario VARCHAR(256) NULL,
    Rol           VARCHAR(50)  NULL,
    UsuarioBd     VARCHAR(128) NOT NULL CONSTRAINT DF_Bitacora_UsuarioBd DEFAULT SUSER_SNAME(),
    DireccionIp   VARCHAR(45)  NULL,
    Dispositivo   VARCHAR(200) NULL,
    ValorAnterior VARCHAR(500) NULL,
    ValorNuevo    VARCHAR(500) NULL,
    Descripcion   VARCHAR(300) NULL,
    CONSTRAINT PK_Bitacora PRIMARY KEY (idBitacora)
);
CREATE INDEX IX_Bitacora_FechaHoraUtc ON soda.tb_Bitacora (FechaHoraUtc DESC);
CREATE INDEX IX_Bitacora_TipoEvento_FechaHoraUtc ON soda.tb_Bitacora (TipoEvento, FechaHoraUtc DESC);
CREATE INDEX IX_Bitacora_idUsuario ON soda.tb_Bitacora (idUsuario);
```

## 7. Implementación del solo lectura (task 3)

Migración `BloquearBitacoraSoloLectura`. Dos capas sobre `soda.tb_Bitacora`:

| Capa | Qué hace | Qué cubre |
|---|---|---|
| Trigger `soda.tr_BitacoraSoloLectura` (`INSTEAD OF UPDATE, DELETE`, EBD09) | Rechaza toda modificación o eliminación con el error 50001: "La bitácora es de solo lectura...". | Cualquier usuario de la aplicación o de la BD que ejecute UPDATE o DELETE, incluido el Administrador de SAPS (HU-004 c6, HU-015 c5). |
| `DENY UPDATE, DELETE` al rol `public` | Las cuentas normales de la BD no tienen el permiso. | Segunda barrera: la operación se niega antes de llegar al trigger. |

En el mapeo de EF (`BitacoraConfiguration`) la tabla se declara con `HasTrigger`, porque EF Core inserta con `OUTPUT` y eso falla en tablas con triggers si no se declaran.

**Límite conocido.** SQL Server no permite blindar una tabla contra su propio dueño: un `sysadmin` o `dbo` puede ejecutar `TRUNCATE TABLE` (no dispara triggers), desactivar el trigger (`ALTER TABLE ... DISABLE TRIGGER`) o eliminar la tabla. Por eso, en producción la aplicación debe conectarse con una cuenta **sin** permisos de `db_owner` ni `sysadmin`, y el acceso administrativo a la BD de RecyPlast debe quedar restringido y fuera del uso diario. Esto se debe confirmar con el líder técnico.

## 8. Trigger de cambios de precio (task 16)

Migración `CrearTriggersAuditoriaPrecio`. Decisiones tomadas para esta task:

| # | Decisión | Justificación |
|---|---|---|
| D8 | **Solo el trigger registra** los cambios de precio, vengan de la pantalla o directo de la BD. | Una sola fuente, sin registros duplicados, y la aplicación no puede omitir el log (HU-004 c7). La pantalla no escribe en la bitácora para los precios; solo le pasa el usuario al trigger. |
| D9 | Se cubren **las dos tablas con precio**: `tb_Precio` y `tb_Bebida`. | Ningún precio del catálogo queda sin auditar. |

### Objetos creados

| Objeto | Descripción |
|---|---|
| `soda.fn_UsuarioContexto()` (EBD12) | Lee el usuario que la aplicación dejó en `CONTEXT_INFO` y devuelve `idUsuario`, `NombreUsuario` y `Rol`. Si no hay contexto (cambio directo en la BD), devuelve el usuario de SQL Server en `NombreUsuario`. |
| `soda.tr_AuditoriaPrecio` (EBD09) sobre `soda.tb_Precio` | `AFTER INSERT, UPDATE`. Registra un `CambioPrecio` con `ValorAnterior` y `ValorNuevo`. |
| `soda.tr_AuditoriaBebida` (EBD09) sobre `soda.tb_Bebida` | `AFTER UPDATE`. Registra un `CambioPrecio` cuando cambia `Precio`. |

### Cuándo se registra un cambio en `tb_Precio`

| Situación | ¿Se registra? | Cómo |
|---|---|---|
| La pantalla cierra la fila anterior (`EstaActivo = 0`) e inserta una fila nueva con otro monto. | Sí | El `INSERT` busca la fila cerrada más reciente del mismo producto y tamaño; ese es el precio anterior. |
| La pantalla cierra la fila anterior (solo cambia `EstaActivo` y `FechaVigenciaHasta`). | No | El `UPDATE` no cambia `Monto`. |
| `UPDATE` directo del `Monto` en la BD. | Sí | Compara `deleted` contra `inserted`. |
| Alta de un producto o de un tamaño nuevo (sin fila anterior). | No | Es un alta, no un cambio. |
| El monto nuevo es igual al anterior. | No | No hay cambio. |

Cada fila del log lleva `Modulo = Catalogo`, `TipoEvento = CambioPrecio`, `Recurso = tb_Precio` o `tb_Bebida`, `IdRegistro` con el id del precio o de la bebida, y en `Descripcion` el nombre del producto o bebida y el tamaño.

### Contrato con la aplicación (task 18, Tiber)

Para que el log diga **quién** hizo el cambio desde la pantalla, la aplicación debe dejar el usuario en `CONTEXT_INFO` **en la misma conexión y transacción** donde guarda el precio, con el formato `idUsuario|nombreUsuario|rol` (solo caracteres ASCII, máximo 128 bytes). Al terminar debe limpiarlo, porque las conexiones se reutilizan. Ejemplo:

```csharp
await using var tx = await db.Database.BeginTransactionAsync();
await db.Database.ExecuteSqlRawAsync(
    "DECLARE @c varbinary(128) = CAST(CAST({0} AS varchar(128)) AS varbinary(128)); SET CONTEXT_INFO @c;",
    $"{idUsuario}|{nombreUsuario}|{rol}");
// ... cerrar la fila anterior y guardar la nueva (todos los SaveChanges) ...
await db.Database.ExecuteSqlRawAsync("SET CONTEXT_INFO 0x00;");
await tx.CommitAsync();
```

Sin ese paso el trigger igual registra el cambio, pero `NombreUsuario` queda con el usuario de SQL Server de la aplicación (p. ej. `sa`) y no con el de la persona.

### Límites

- `CONTEXT_INFO` no es una prueba de identidad: quien tenga una conexión a la BD puede escribirlo. Sirve para trazabilidad dentro de la aplicación; el usuario real de la BD siempre queda en `UsuarioBd`.
- La aplicación no guarda `DireccionIp` ni `Dispositivo` en los cambios de precio: el trigger no los conoce.

