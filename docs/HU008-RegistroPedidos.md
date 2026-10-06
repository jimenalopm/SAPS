# HU-008 — Registrar pedido de un colaborador

Implementación local del sprint 1. Parte del commit `47d421e`, que contiene las correcciones de HU-007, en la rama `codex/hu008-registro-pedidos`.

## Qué hace

La operadora de la soda inicia sesión, busca al comprador por su código, verifica su nombre y foto, selecciona el tipo de comida y agrega productos, adicionales o bebidas. Puede cambiar cantidades, quitar artículos y escribir observaciones. El botón **Registrar pedido** guarda la compra con el monto calculado por el servidor.

La persona que atiende y la persona que compra son distintas:

- **SODA001**: cuenta ficticia que inicia sesión como operadora, contraseña de desarrollo `Prueba123`.
- **DEMO001**: Ana Solís, compradora ficticia activa.
- **DEMO002**: Luis Mora, comprador ficticio activo.
- **DEMO003**: colaborador ficticio inactivo, utilizado para comprobar el rechazo.

También puede entrar la cuenta de desarrollo ADM001. Los roles existentes Soda, Usuario y Administrador tienen acceso a pedidos. RecursosHumanos y las cuentas sin rol no lo tienen. Soda no puede modificar el catálogo. Se conservan los nombres actuales de los roles; no se reorganiza HU-003.

Los colaboradores DEMO no son reales. Viven en `rrhh.tb_Colaborador` como cualquier otro, pero solo los inserta el sembrado de prueba (Development contra un servidor local); en la base real no existen. Los pedidos a su nombre llevan `EsPrueba = true`.

## Alcance y dependencias

- Utiliza HU-001 para sesión, HU-003 para permisos y HU-007 para catálogo.
- Para este sprint se consulta el catálogo activo de HU-007. La planificación del menú diario de HU-005 no se implementa aquí.
- El tipo de comida se elige explícitamente: Desayuno, Almuerzo o Café. El nombre visible Café conserva el valor interno `Merienda` para compatibilidad con la base de datos y los pedidos existentes. Los acompañamientos se agregan como artículos del catálogo.
- Solo se ofrecen productos activos de categorías activas, precios vigentes del modo correspondiente y tamaños activos. Las bebidas con tamaño inactivo tampoco se venden.
- Desactivar una categoría o tamaño impide nuevas ventas de esas opciones y conserva los pedidos ya registrados.
- No se agregan restricciones horarias ni topes comerciales de consumo. Las cantidades deben ser enteros positivos dentro de la capacidad técnica del tipo INT.
- No se aplica impuesto.
- Se incorpora el botón mínimo **Registrar pedido** autorizado para HU-008. No se añade la pantalla separada de confirmación de HU-009.
- No envía comprobantes, exporta planillas ni anula órdenes: esas historias están fuera del alcance.
- Los colaboradores salen de la tabla `rrhh.tb_Colaborador` (RNF-006), que RH mantendrá desde SAPS. La búsqueda normaliza el código a 10 dígitos (`842` → `0000000842`) y distingue tres resultados: no encontrado, inactivo (`EstaActivo = 0`, no se permite registrar) y activo. Sin foto (`RutaFoto` vacía) la pantalla muestra las iniciales.

## Qué se guarda y cuándo

Antes de pulsar Registrar pedido, el carrito existe solamente en la memoria de la página. Buscar colaboradores y consultar el catálogo no insertan pedidos. Recargar, salir de la página o cerrar sesión descarta el borrador. No se utiliza localStorage ni sessionStorage para conservarlo.

Al registrar, el servidor vuelve a validar colaborador, disponibilidad, cantidades y precios. El precio enviado desde la pantalla solo sirve para detectar cambios: el cobro se calcula con el precio consultado en SQL Server. Si cambió, se solicita actualizar el catálogo y revisar el monto.

Una **transacción** guarda cabecera y renglones juntos. Si falla un renglón, se revierte todo ese pedido. No se eliminan otras compras. Un identificador único por intento evita duplicar la compra al repetir la solicitud. Si se pierde la respuesta, la pantalla conserva la misma solicitud y ofrece **Reintentar registro**.

Cerrar sesión después de pulsar Registrar pedido no anula una compra que ya se haya guardado. La condición de descarte se refiere al borrador sin registrar.

## Tablas nuevas

| Tabla | Contenido |
|---|---|
| `soda.tb_Pedido` | Número de pedido, comprador, operadora, fecha UTC, tipo de comida, observación, total, marca de prueba e identificador para evitar duplicados. |
| `soda.tb_DetallePedido` | Artículo, tamaño, cantidad, precio cobrado y subtotal de cada renglón. |

Los nombres y precios se copian al detalle como constancia de lo cobrado. Cambiar el catálogo después no cambia los pedidos antiguos. Las relaciones impiden eliminar físicamente un precio o bebida que esté referenciado por un pedido.

Los precios unitarios son INT; total y subtotal son BIGINT, ambos enteros. BIGINT evita que una suma grande desborde INT. FechaRegistroUtc utiliza DATETIME2 y se guarda en UTC.

### Estándar EBD11

El catálogo existente ya utiliza precios enteros. Esta historia no requiere convertir sus columnas. Para resolver la diferencia documental con RNF-005, el equipo debe actualizar EBD11: **precios unitarios INT; subtotales y totales BIGINT; sin decimales**. Este documento deja registrada la propuesta; no modifica el Word original de estándares.

## Diferencia entre código, migración y base

1. Los archivos C# describen cómo funciona el sistema.
2. La migración `AgregarRegistroPedidos` describe cómo agregar las dos tablas.
3. Preparar esa migración crea archivos en el repositorio; no la aplica a SAPS_DB.
4. Ejecutar `dotnet-ef database update` aplica las migraciones pendientes a la conexión configurada.

En esta implementación, el método `Up` de la migración nueva solo crea las dos tablas, sus índices y restricciones. No contiene eliminación ni modificación de tablas de catálogo o usuarios. El método `Down` es la operación inversa que EF genera para una eventual reversión; no se ejecuta al actualizar hacia esta migración.

## Pruebas realizadas y seguridad de los datos

Las pruebas automatizadas crean una base distinta, con nombre `SAPS_HU008_Pruebas_` seguido de un identificador aleatorio. Cambian el nombre de la base en la conexión antes de ejecutar cualquier migración o insertar datos. Al terminar, comprueban que el destino todavía sea esa base temporal antes de eliminarla.

**No utilizan SAPS_DB para las compras de prueba.** También inician una aplicación separada en un puerto libre, conectada exclusivamente a la base temporal. La aplicación que el estudiante tiene abierta no se detiene.

Resultado: **54 verificaciones de HU-008 y 24 verificaciones de regresión de HU-007 correctas**. La compilación no presenta errores. Persisten las dos advertencias NU1901 de NuGet.Packaging y NuGet.Protocol que ya existían en el proyecto; no se actualizaron esas dependencias como parte de esta historia.

Comprobaciones automatizadas: búsqueda, errores, catálogo activo, vigencia de precios, cantidades, montos, historial, guardado atómico, reintentos, solicitudes simultáneas, acceso por roles, protección antifalsificación y cierre de sesión sin guardar.

Pruebas de navegador realizadas sobre esa instancia temporal: fotografías, selección, cambio inmediato del total, cantidad cero, eliminación de renglones, observaciones, registro correcto, colaborador inactivo y volver atrás después de cerrar sesión.

Comando para repetir las pruebas desde la raíz del repositorio:

```bash
dotnet run --project tests/SAPS.Pedidos.IntegrationTests -- SAPS.Web/appsettings.json
```

El argumento opcional `--browser` mantiene abierta la aplicación de prueba y muestra su dirección. Enter termina esa sesión y limpia únicamente su base temporal.

## Cómo probar con tu catálogo local

Al momento de entregar la implementación, la migración nueva **no se ha aplicado a SAPS_DB**. Estas instrucciones son el siguiente paso para hacerlo.

1. Mantener Docker y SQL Server encendidos.
2. Si la aplicación anterior sigue ejecutándose en una terminal, presionar Ctrl+C en esa terminal. Esto detiene la web, no borra la base.
3. Abrir la terminal en el proyecto:

```bash
cd /Users/mac/Documents/GitHub/SAPS/SAPS.Web
```

4. Agregar las tablas nuevas a la base local configurada:

```bash
"$HOME/.dotnet/tools/dotnet-ef" database update
```

5. Cuando aparezca Done, iniciar la web:

```bash
dotnet run
```

6. Abrir en el navegador la dirección de `Now listening on`.
7. Iniciar sesión con SODA001 / Prueba123 y entrar a Registrar pedido.
8. Buscar DEMO001 o DEMO002. Elegir el tipo de comida y artículos del catálogo local; modificar y quitar cantidades antes de registrar.
9. Pulsar Registrar pedido. Debe aparecer el número y total del pedido y quedar vacío el formulario para una nueva compra.
10. En la extensión SQL Server, refrescar la lista de tablas. Se pueden consultar sin modificar datos:

```sql
SELECT TOP (20) * FROM soda.tb_Pedido ORDER BY idPedido DESC;
SELECT TOP (50) * FROM soda.tb_DetallePedido ORDER BY idDetallePedido DESC;
```

Si no hay opciones para comprar, revisar con ADM001 que existan productos activos, categorías activas, tamaños activos cuando corresponda y precios vigentes. HU-008 no crea automáticamente productos en tu catálogo.

## Seguimiento para Azure DevOps

| To-do | Resultado local |
|---|---|
| 1. Revisar e integrar dependencias | Implementado: colaboradores desde `rrhh.tb_Colaborador`. |
| 2. Buscar colaborador por código | Implementado y probado. |
| 3. Mostrar código, nombre y foto | Implementado y probado en navegador. |
| 4. Seleccionar productos y bebidas | Implementado y probado. |
| 5. Armar y editar carrito temporal | Implementado y probado en navegador. |
| 6. Calcular subtotales y total | Implementado y probado en servidor y pantalla. |
| 7. Persistir pedido y detalle | Implementado; migración probada en SQL Server temporal. Aplicación a SAPS_DB pendiente del paso local explicado arriba. |
| 8. Registrar con validaciones | Implementado y probado. |
| 9. Descartar borrador y manejar errores | Implementado y probado. |
| 10. Pruebas y preparación de integración | Pruebas locales realizadas. Falta revisión del estudiante/equipo, publicación y PR. |

Una tarea de programación completa no equivale a una historia integrada en main. La rama de HU-008 depende de las correcciones locales de HU-007. Antes de preparar su PR hacia main, se deben integrar o acordar esas dependencias para que no aparezcan mezcladas como trabajo nuevo de HU-008.

Los cambios de HU-008 están en los archivos locales, todavía sin un commit propio. No se publicaron cambios ni se modificó main. La cuenta todavía necesita permiso de escritura para publicar directamente en el repositorio del equipo.

## Mapa del código para estudiar

- `Controllers/PedidosController.cs`: recibe solicitudes de la pantalla, exige rol y devuelve resultados o errores.
- `Services/Pedidos/Colaboradores.cs`: contrato de búsqueda, consulta a `rrhh.tb_Colaborador` y normalización del código.
- `Services/Pedidos/ServicioPedidos.cs`: reglas del catálogo, validaciones, precios, transacción y registro.
- `Models/Pedidos/`: datos que recibe el servidor y entidades de las nuevas tablas.
- `Data/PedidoConfiguration.cs`: relaciones y reglas de SQL Server para esas entidades.
- `Data/ApplicationDbContextFactory.cs`: permite ejecutar la herramienta de migraciones sin iniciar la web ni crear usuarios.
- `Views/Pedidos/Index.cshtml`: estructura de la pantalla.
- `wwwroot/js/pedidos.js`: búsqueda, carrito en memoria, cálculos visibles y envío del pedido.
- `tests/SAPS.Pedidos.IntegrationTests/`: pruebas ejecutables y base temporal aislada.

## Recurso visual de prueba

Imagen generada mediante la herramienta integrada imagegen; no usa fotos de la empresa. Archivo: `SAPS.Web/wwwroot/images/demo/colaboradores.png`. La pantalla muestra la mitad correspondiente a cada colaborador ficticio.

Prompt utilizado:

> Use case: photorealistic-natural. Create a single square asset consisting of two employee ID portrait photographs side by side in exact equal left and right halves, no gap. Left half fictional adult woman age 35, dark curly hair, neutral green shirt. Right half fictional adult man age 40, short dark hair, blue shirt. Each face centered within its own half, head and shoulders, neutral pale gray background, even realistic studio light. Entirely fictional people for a university cafeteria software demo, not actual employees. No text, no logo, no watermark. Each portrait must fit entirely in its half with generous head margins.

## Ajuste de la pantalla de Soda: Café y tamaños

El tiempo de comida se muestra como **Café**. El valor enviado al servidor continúa siendo `Merienda`, por lo que este cambio no requiere migraciones ni modifica compras anteriores.

No hay un límite de dos tamaños en el selector: se muestran todas las variantes activas con precio vigente asignado al producto. Para ofrecer Pequeño, Mediano y Grande, los tres tamaños deben estar activos y seleccionados con su respectivo precio en la edición del producto. Crear un tamaño por sí solo no le asigna precio a cada producto.

Se agregó una prueba de regresión que ofrece y registra tres tamaños del mismo producto, y comprueba que desactivar uno deje disponibles los otros dos.
