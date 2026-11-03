# HU-015 | Registrar cambios de precios en bitácora (versión actualizada)

Actualizada al cierre de los Sprints 1 y 2. Lo agregado o corregido respecto de la versión original está marcado con **(nuevo)** y **(precisado)**.

## Historia de usuario

Como Administrador, requiero que el sistema registre automáticamente cualquier cambio de precio en los productos, para mantener un historial de modificaciones con fines de auditoría.

## Descripción

**RNF-002 | Auditoría, bitácora de accesos y eventos:** el sistema debe mantener una bitácora de eventos que registre, como mínimo: inicios de sesión (exitosos y fallidos) con fecha, hora e identificador de dispositivo; cambios de precios en el catálogo; y anulaciones de órdenes con su motivo. El acceso a la bitácora estará restringido al rol de administrador.

**Entradas:** modificación de precio realizada en el catálogo, ya sea desde la pantalla de gestión o directamente en la base de datos.

**Salidas:** registro en bitácora con el producto, el precio anterior, el precio nuevo, la fecha, la hora y el usuario que realizó el cambio.

**Consideraciones:**
- Solo el Administrador tiene acceso a la bitácora de cambios de precios.
- **(precisado)** Este evento aplica a los precios de los productos (tb_Precio, HU-007) y de las bebidas (tb_Bebida). En las bebidas el precio se actualiza en la misma fila y el registro guarda el valor anterior y el nuevo.
- **(nuevo)** Dar de alta un producto con su primer precio no genera este evento porque no hay un precio anterior; tampoco lo genera guardar un precio igual al que ya tenía.
- Cuando el cambio se hace desde la pantalla de gestión, el usuario registrado es el usuario del sistema que lo realizó; cuando se hace directamente en la base de datos, es el usuario de base de datos que ejecutó la modificación.
- **(nuevo)** Quitar el precio de un tamaño de un producto, o pasar de precio único a precios por tamaño, no cambia ningún monto; se registra como una modificación del producto en el módulo Catálogo (no como cambio de precio), con el tamaño y el último precio que tenía.

## Criterios de aceptación

1. **Registro automático desde la pantalla.** Cuando se modifica el precio de un producto o de una bebida desde la pantalla de gestión del catálogo, entonces el sistema genera automáticamente un registro en bitácora, sin intervención adicional del usuario.

2. **(nuevo) Registro automático en la base de datos.** Cuando se modifica el precio de un producto o de una bebida directamente en la base de datos, entonces el sistema genera el mismo registro en bitácora, con el usuario de base de datos que ejecutó el cambio.

3. **Datos del registro.** El registro incluye: producto o bebida (y el tamaño cuando aplique), precio anterior, precio nuevo, fecha, hora y usuario que realizó el cambio (RNF-002).

4. **Acceso restringido.** Solo el rol Administrador puede consultar los registros de cambio de precio en la bitácora.

5. **Solo lectura.** Los registros de cambio de precio no pueden modificarse ni eliminarse por ningún usuario ni rol, incluido el Administrador. **(precisado)** La aplicación no ofrece esas operaciones y la base de datos rechaza UPDATE y DELETE sobre la tabla.

6. **Consulta desde HU-004.** El cambio de precio aparece como un tipo de evento filtrable en la pantalla de bitácora (HU-004), con opción de ver el detalle (precio anterior y nuevo).

7. **(nuevo) Tamaño quitado.** Cuando se quita el precio de un tamaño de un producto, o se cambia entre precio único y precios por tamaño, entonces el sistema genera un registro del módulo Catálogo con el producto, el tamaño y el precio que tenía.
