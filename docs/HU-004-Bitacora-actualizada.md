# HU-004 | Registrar logs de actividad y accesos (versión actualizada)

Actualizada al cierre de los Sprints 1 y 2, con los cambios del endurecimiento de seguridad. Lo agregado respecto de la versión original está marcado con **(nuevo)** y lo que se precisó, con **(precisado)**.

## Historia de usuario

Como Administrador del sistema, requiero que el sistema genere y almacene logs detallados de accesos y acciones realizadas por los usuarios, para poder auditar, monitorear y controlar el uso del sistema.

## Descripción

**RNF-002 | Auditoría, bitácora de accesos y eventos:** el sistema debe mantener una bitácora de eventos que registre, como mínimo: inicios de sesión (exitosos y fallidos) con fecha, hora e identificador de dispositivo; cambios de precios en el catálogo; y anulaciones de órdenes con su motivo. El acceso a la bitácora estará restringido al rol de administrador.

### Entradas

- Inicios de sesión (exitosos y fallidos) y cierres de sesión de los usuarios.
- Cambios de precio de productos en el catálogo (HU-007, HU-015).
- Altas, bajas y cambios de rol de cuentas de usuario realizados por el Administrador (HU-003). Incluye reactivaciones y cambios de correo o de colaborador vinculado. **(precisado)**
- **(nuevo)** Cambio de contraseña de una cuenta: voluntario, obligatorio por primer ingreso u obligatorio por vencimiento; también el intento fallido con la contraseña actual incorrecta. Nunca se registran contraseñas.
- **(nuevo)** Altas, ediciones y activaciones o desactivaciones de elementos del catálogo (productos, bebidas, categorías, tamaños y tipos de bebida), sin duplicar los cambios de precio, que ya registran los triggers.
- **(nuevo)** Pedidos registrados (HU-008) y pedidos rechazados, con el motivo del rechazo.

### Salidas

Registro de log con fecha, hora, identificador del usuario, rol, dispositivo utilizado, dirección IP, tipo de acción realizada, módulo afectado, recurso o registro involucrado, detalle del cambio efectuado (valor anterior y nuevo cuando aplique), usuario afectado cuando la acción se realiza sobre la cuenta de un tercero, y resultado de la operación (exitosa o fallida).

### Consideraciones

- Solo el Administrador puede consultar los logs de actividad y accesos (pantalla de HU-004).
- Los registros no pueden ser alterados ni eliminados por ningún usuario ni rol, incluido el Administrador. **(precisado)** Esta protección se aplica desde la aplicación y desde la base de datos (trigger y permisos). Para que sea total frente a quien tenga las credenciales de administración del servidor, la aplicación debe conectarse con un usuario de SQL Server sin privilegios de `sysadmin` ni `db_owner`.
- El registro de anulaciones de órdenes con su motivo (RNF-002) se incorporará cuando se implemente HU-010; el servicio de logs queda preparado para recibir ese evento.
- **(precisado)** "Identificador de dispositivo" se entiende como el navegador y el sistema operativo de la persona (p. ej. "Chrome en Windows"), junto con la dirección IP.
- **(nuevo)** En un inicio de sesión fallido con un usuario que no existe no se guarda lo que se escribió en el campo de usuario, porque pudo ser una contraseña escrita en el campo equivocado.
- **(nuevo)** El envío del formulario de inicio de sesión está limitado a 10 intentos por minuto por dirección IP; los envíos que superan el límite no generan evento.

## Criterios de aceptación

1. **Registro de sesión.** Cuando ocurre un inicio de sesión (exitoso o fallido) o un cierre de sesión, entonces el sistema genera un log con fecha, hora, usuario, rol, dispositivo e IP.

2. **Cambios de precio.** Cuando se modifica el precio de un producto (HU-007, HU-015), entonces el sistema genera un log con el valor anterior, el valor nuevo, el producto, el usuario y la fecha.

3. **Altas, bajas y cambios de rol.** Cuando el Administrador crea, desactiva o cambia el rol de una cuenta (HU-003), entonces el sistema genera un log con el usuario afectado, el usuario que hizo el cambio y la fecha.

4. **Módulo y recurso.** Todo log incluye el módulo afectado (**Seguridad, Catálogo o Pedidos**, según los eventos implementados a la fecha) **(precisado)** y el recurso o registro involucrado, además del resultado de la operación (exitosa o fallida).

5. **Acceso restringido.** Solo el rol Administrador puede consultar los logs; los demás roles no ven la opción en el sistema ni pueden acceder escribiendo la dirección de la pantalla.

6. **Solo lectura.** Los logs no pueden modificarse ni eliminarse por ningún usuario ni rol, incluido el Administrador. **(precisado)** La aplicación no ofrece ninguna operación de edición o borrado, y la base de datos rechaza UPDATE y DELETE sobre la tabla.

7. **Registro automático, sin intervención.** El usuario que realiza la acción no puede omitir ni desactivar la generación del log correspondiente. **(precisado)** Los cambios de precio los registra la base de datos (trigger), aunque el cambio se haga directamente en ella. Los demás eventos los registra la aplicación en cada operación; si el servicio de bitácora falla, la operación no se interrumpe y el fallo queda en el log técnico del servidor.

8. **Eventos auditables futuros.** Cuando ocurre cualquier evento definido como auditable en RNF-002 o incorporado por otra historia de usuario (por ejemplo, la anulación de órdenes en HU-010), entonces el sistema genera un log con los mismos campos definidos en las salidas, sin modificar la estructura de la bitácora.

9. **(nuevo) Cambios de contraseña.** Cuando una persona cambia su contraseña, entonces el sistema genera un log con la cuenta, el motivo (voluntario u obligatorio), el resultado, el dispositivo y la IP, sin incluir ninguna contraseña. Si la contraseña actual es incorrecta, el intento queda registrado como fallido.

10. **(nuevo) Pedidos.** Cuando se registra un pedido, entonces el sistema genera un log con el número de pedido, el colaborador, el tipo de comida y el total. Cuando un pedido es rechazado, entonces genera un log fallido con el motivo. Un reintento con el mismo código de registro no genera un segundo log.

11. **(nuevo) Cambios del catálogo.** Cuando el Administrador o Recursos Humanos crea, modifica, activa o desactiva un elemento del catálogo, entonces el sistema genera un log con el elemento, el valor anterior y el nuevo cuando aplique, el usuario y la fecha. Si solo cambia el precio, lo registra el trigger de precios (criterio 2) y no se duplica.

## Pendientes fuera de esta historia

- Anulación de órdenes con su motivo: HU-010.
- Usuario de SQL Server con permisos mínimos para la aplicación (cierra el criterio 6 frente a quien administre el servidor): despliegue en RecyPlast.
