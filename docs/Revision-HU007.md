# Revisión de HU-007

## Problemas corregidos

- Los formularios de productos cargan los tamaños desde el primer acceso, incluso si el producto tiene precio único.
- Al cambiar entre precio único y precios por tamaño, se cierran los precios que dejan de corresponder: Activo=false y FechaVigenciaHasta con la fecha del cambio. No se elimina el historial.
- El listado distingue el precio único de los precios asociados a tamaños.
- Los dos guardados de una actualización de precio se realizan en una transacción: si falla la inserción del nuevo precio, se revierte el cierre del anterior.
- Los tamaños inactivos ya asignados permanecen visibles al editar productos y bebidas, identificados como inactivos. No se admiten nuevas asignaciones de tamaños inactivos.
- Las categorías inactivas actualmente asignadas se conservan y se identifican como tales.
- Se validan tamaños inexistentes, repetidos y seleccionados sin precio, y la categoría recibida al guardar productos.
- Recursos Humanos puede acceder al catálogo, conforme a HU-007. No se modifica el conjunto de roles del sistema.

## Datos anteriores

No hay cambios en el esquema de SQL Server ni migraciones nuevas. Al guardar un producto existente, sus precios activos se ajustan al modo seleccionado. No se reparan todos los productos automáticamente; revisar y guardar los productos de prueba que quedaron con modos mezclados.

Un IdTamano nulo corresponde al precio sin tamaño. Una FechaVigenciaHasta nula corresponde a un precio sin fecha de cierre. Desactivar un tamaño conserva sus asociaciones y sus precios; no equivale a reemplazar un precio.

## Verificación automática

Se ejecutaron 24 verificaciones sobre una base temporal de SQL Server, creada con las migraciones del proyecto y eliminada al terminar. Incluyen creación, cambio en ambos sentidos, actualización de precios, historial, validaciones, conservación de tamaños inactivos y reversión ante un fallo de inserción. La aplicación y el ejecutable de pruebas compilaron sin errores.

Desde la raíz del repositorio:

```bash
dotnet run --project tests/SAPS.Catalogo.IntegrationTests -- SAPS.Web/appsettings.json
```

Requiere .NET 10 y SQL Server local en el puerto 1433, con permisos para crear y eliminar una base temporal. Lee la conexión de ese archivo, sustituye únicamente el nombre de base por uno único de pruebas y no modifica SAPS_DB. No imprime la contraseña.

El ejecutable invoca los controladores y comprueba los registros en SQL Server. No sustituye las pruebas del navegador ni reproduce automáticamente la autenticación HTTP, la protección antifalsificación y la conversión de texto a números del formulario.

## Comprobación manual

El usuario confirmó que las correcciones funcionan al probarlas en el navegador. No se recibió un resultado individual para cada caso; se conserva la lista para que el revisor pueda repetirla.

1. Abrir Nuevo producto y marcar tamaños: deben aparecer sin guardar previamente.
2. Cambiar un producto de precio único a tamaños y después a precio único: verificar el listado tras recargar.
3. Cambiar un precio, guardar y volver a editar: comprobar el valor nuevo.
4. Seleccionar un tamaño sin precio, ingresar cero, negativos, decimales y dejar la selección de tamaños vacía: comprobar que no se guarda información inválida.
5. Editar un producto y una bebida cuyo tamaño fue desactivado: deben conservar la asignación identificada como inactiva si no se modifica.
6. Comprobar con una cuenta de Recursos Humanos el acceso al catálogo, y la denegación con una cuenta sin permiso.

## Fuera de esta corrección

- Registro de pedidos HU-008 y pantalla de confirmación HU-009.
- Regla de disponibilidad para ventas al desactivar una categoría o tamaño; requiere acuerdo del equipo.
- Unificación de los roles Soda y Usuario.
- Las dos advertencias de vulnerabilidades de gravedad baja observadas durante la restauración de dependencias. Las compilaciones de esta revisión usaron paquetes locales con auditoría desactivada; no se cambiaron las versiones ni se solucionaron esas advertencias.

No integrar en main hasta completar la comprobación manual y la revisión del equipo.
