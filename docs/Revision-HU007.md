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

## Validaciones completas del catálogo (PR #15)

### Nombres
- Categoría, producto, bebida y tamaño: mínimo de caracteres (3; en tamaños 2, para admitir «1L»), al menos una letra y solo letras, números, espacio y los signos `. , / ( ) & ' % + -`.
- Máximo: 50 en categorías, 60 en productos y bebidas, 30 en tamaños.
- Los espacios al inicio y al final se quitan y los repetidos se reducen a uno, también en los textos de búsqueda de las cuatro pestañas.
- Sin duplicados: categoría, tamaño, producto dentro de la misma categoría y bebida con el mismo nombre y tamaño. Si el existente está desactivado, el mensaje indica que se active.

### Precios
- Mayores a 0 y de máximo ₡10.000 (`ReglasCatalogo.PrecioMaximo`), en precio único, por tamaño y en bebidas.
- Los campos solo aceptan dígitos y no dejan escribir ni pegar más del máximo; el servidor repite la validación.
- Un tamaño mayor no puede costar menos que uno menor, dentro de un producto y entre las presentaciones de una misma bebida. El orden se deduce del nombre del tamaño (Pequeño, Mediano, Grande, 600ml, 1L, etc.); si el nombre no se reconoce, no se compara. Igual precio sí se permite.

### Tamaños por categoría
- Cada tamaño es de comida o de bebida. Los productos solo usan tamaños de comida y las bebidas solo de bebida.
- Cada categoría define qué tamaños permite. No se puede quitar de una categoría un tamaño que usan productos activos, ni cambiar el tipo de un tamaño en uso.

### Tipo de bebida
- Solo se aceptan Gaseosa, Embotellada, Energizante y Jugo. La lista está en `ReglasCatalogo.TiposBebida`, la usan el formulario y el servidor, y coincide con la restricción de `tb_Bebida`. Agregar un tipo nuevo exige cambiar ambos sitios (lista y restricción, con migración).

### Reglas de estado
- No se desactiva una categoría con productos activos, ni un tamaño usado por productos o bebidas activos.
- No se activa un producto con la categoría inactiva, ni una bebida con el tamaño inactivo.
- Decisión del equipo: se bloquea la desactivación en lugar de ocultar automáticamente los productos dependientes. Registrar pedido ya solo muestra artículos activos con categoría, tamaño y precio vigentes.

### Búsqueda y filtro
- La pestaña Productos tiene filtro por categoría, además de la búsqueda por nombre.

## Datos anteriores

Hay tres migraciones nuevas, que se aplican con `dotnet ef database update --project SAPS.Web`:
- `AgregarOrdenTamano` y `QuitarOrdenTamano`: la columna `Orden` se agregó y luego se eliminó, porque el orden se deduce del nombre.
- `TamanosPorCategoria`: agrega `EsParaBebida` a `tb_Tamano`, crea `tb_CategoriaTamano`, marca como de bebida los tamaños que ningún producto usa y asigna a cada categoría los tamaños que sus productos ya usan.

Después de migrar, revisar en la pestaña Tamaños que cada uno quedó con el tipo correcto. Al guardar un producto existente, sus precios activos se ajustan al modo seleccionado. No se reparan todos los productos automáticamente; revisar y guardar los productos de prueba que quedaron con modos mezclados.

Un IdTamano nulo corresponde al precio sin tamaño. Una FechaVigenciaHasta nula corresponde a un precio sin fecha de cierre. Desactivar un tamaño conserva sus asociaciones y sus precios; no equivale a reemplazar un precio.

## Verificación automática

Pruebas unitarias del proyecto `SAPS.Tests`: nombres, precios, tamaños por categoría, orden de tamaños, estado y filtros, nombre de tamaño, búsqueda, orden de precios en bebidas y tipo de bebida. Se ejecutan con `dotnet test` desde la raíz.

Verificación sobre base temporal (anterior a las validaciones de este PR): se ejecutaron 24 verificaciones sobre una base temporal de SQL Server, creada con las migraciones del proyecto y eliminada al terminar. Incluyen creación, cambio en ambos sentidos, actualización de precios, historial, validaciones, conservación de tamaños inactivos y reversión ante un fallo de inserción. La aplicación y el ejecutable de pruebas compilaron sin errores.

Desde la raíz del repositorio:

```bash
dotnet run --project tests/SAPS.Catalogo.IntegrationTests -- SAPS.Web/appsettings.json
```

Requiere .NET 10 y SQL Server local en el puerto 1433, con permisos para crear y eliminar una base temporal. Lee la conexión de ese archivo, sustituye únicamente el nombre de base por uno único de pruebas y no modifica SAPS_DB. No imprime la contraseña.

El ejecutable invoca los controladores y comprueba los registros en SQL Server. No sustituye las pruebas del navegador ni reproduce automáticamente la autenticación HTTP, la protección antifalsificación y la conversión de texto a números del formulario.

## Comprobación manual

El usuario confirmó que las correcciones anteriores funcionan al probarlas en el navegador. No se recibió un resultado individual para cada caso; se conserva la lista para que el revisor pueda repetirla. Las validaciones de este PR se verificaron con pruebas unitarias; los casos 7 a 11 están pendientes de confirmar en el navegador.

1. Abrir Nuevo producto y marcar tamaños: deben aparecer sin guardar previamente.
2. Cambiar un producto de precio único a tamaños y después a precio único: verificar el listado tras recargar.
3. Cambiar un precio, guardar y volver a editar: comprobar el valor nuevo.
4. Seleccionar un tamaño sin precio, ingresar cero, negativos, decimales y dejar la selección de tamaños vacía: comprobar que no se guarda información inválida.
5. Editar un producto y una bebida cuyo tamaño fue desactivado: deben conservar la asignación identificada como inactiva si no se modifica.
6. Comprobar con una cuenta de Recursos Humanos el acceso al catálogo, y la denegación con una cuenta sin permiso.
7. En un precio escribir letras, `e`, `-`, punto o un valor mayor a 10.000: no debe dejar escribirlo ni pegarlo.
8. Crear un tamaño «Grande » (con espacio al final) cuando ya existe «Grande»: debe rechazarlo por duplicado.
9. Intentar desactivar una categoría con productos activos y un tamaño en uso: debe mostrar el mensaje y no desactivar.
10. Crear un producto de comida: no debe ofrecer tamaños de bebida como 600ml; en la pizza, Grande más barato que Mediano debe rechazarse.
11. Crear la misma bebida en 600ml y 3L con el 3L más barato: debe rechazarlo. Probar el filtro por categoría y una búsqueda con espacios de más.

## Fuera de esta corrección

- Registro de pedidos HU-008 y pantalla de confirmación HU-009.
- Unificación de los roles Soda y Usuario.
- Las dos advertencias de vulnerabilidades de gravedad baja observadas durante la restauración de dependencias. Las compilaciones de esta revisión usaron paquetes locales con auditoría desactivada; no se cambiaron las versiones ni se solucionaron esas advertencias.

Búsqueda y filtro por categoría en Registrar pedido: se hará en otra rama o HU. Tampoco se incluye el aviso de cambio brusco de precio (descartado).

No integrar en main hasta completar la comprobación manual y la revisión del equipo.
