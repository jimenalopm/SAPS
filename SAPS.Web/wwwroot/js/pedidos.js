(() => {
    'use strict';
    const byId = id => document.getElementById(id);
    const app = byId('pedido-app');
    if (!app) return;
    const moneda = valor => '₡' + BigInt(valor).toLocaleString('es-CR');
    let catalogo = [], carrito = [], colaborador = null, busqueda = 0;
    let enviando = false, pendiente = null, cargandoCatalogo = false, confirmando = false;
    let token = crypto.randomUUID();
    const mensaje = (texto, tipo = 'danger', enfocar = true) => {
        const caja = byId('mensaje-pedido');
        caja.className = 'alert alert-' + tipo;
        caja.textContent = texto;
        if (enfocar) caja.focus();
    };
    const limpiarMensaje = () => byId('mensaje-pedido').classList.add('d-none');
    const CANTIDAD_MAXIMA = 999, TOTAL_ADVERTENCIA = 25000n, TOTAL_MAXIMO = 40000n;
    const cantidadValida = valor => /^\d+$/.test(String(valor)) && Number(valor) > 0 && Number(valor) <= CANTIDAD_MAXIMA;
    // Un input numérico ignora maxlength, así que se limita a 3 dígitos y se bloquean e, +, - y punto.
    const limitarDigitos = entrada => {
        entrada.addEventListener('keydown', e => { if (['e', 'E', '+', '-', '.', ','].includes(e.key)) e.preventDefault(); });
        entrada.addEventListener('input', () => { if (entrada.value.length > 3) entrada.value = entrada.value.slice(0, 3); }, true);
    };
    const totalCarrito = () => carrito.reduce((suma, l) => suma + BigInt(l.precio) * BigInt(l.cantidad), 0n);
    // Aviso permanente (no se borra con otros mensajes) mientras el total siga fuera de lo razonable.
    function actualizarTotal() {
        const total = totalCarrito();
        byId('total-pedido').textContent = moneda(total);
        const aviso = byId('aviso-total');
        if (total > TOTAL_MAXIMO) {
            aviso.className = 'alert alert-danger';
            aviso.textContent = 'El pedido supera el máximo permitido de ' + moneda(TOTAL_MAXIMO) + '. Reduzca cantidades o quite artículos para poder registrarlo.';
        } else if (total > TOTAL_ADVERTENCIA) {
            aviso.className = 'alert alert-warning';
            aviso.textContent = 'Advertencia: el pedido excede ' + moneda(TOTAL_ADVERTENCIA) + ', que es el límite razonable para un pedido. Verifique las cantidades. No podrá registrar más de ' + moneda(TOTAL_MAXIMO) + '.';
        } else aviso.className = 'alert d-none';
        return total;
    }
    function pintarTipos() {
        const select = byId('tipo-comida'), anterior = select.value;
        const categorias = [...new Set(catalogo.map(a => a.categoria))];
        select.replaceChildren(new Option('Seleccione…', ''), ...categorias.map(c => new Option(c, c)));
        select.value = categorias.includes(anterior) ? anterior : '';
        pintarArticulos();
    }
    function pintarArticulos() {
        const select = byId('articulo'), tipo = byId('tipo-comida').value;
        const lista = catalogo.filter(a => a.categoria === tipo);
        select.replaceChildren(new Option(tipo ? (lista.length ? 'Seleccione un artículo…' : 'No hay artículos en esta categoría') : 'Seleccione primero el tipo de comida', ''));
        lista.forEach(a => select.append(new Option(a.nombre + (a.tamano ? ' · ' + a.tamano : '') + ' — ' + moneda(a.precio), a.clave)));
        bloquear();
    }
    async function solicitar(url, opciones = {}) {
        const respuesta = await fetch(url, { credentials: 'same-origin', cache: 'no-store', ...opciones });
        if (respuesta.redirected || respuesta.status === 401 || respuesta.status === 403) {
            reiniciar();
            throw new Error('La sesión terminó o no tiene permiso. Inicie sesión nuevamente y recargue esta pantalla.');
        }
        const json = respuesta.headers.get('content-type')?.includes('application/json');
        const datos = json ? await respuesta.json() : null;
        if (!respuesta.ok) {
            const error = new Error(datos?.mensaje || (respuesta.status === 400
                ? 'La sesión o la solicitud dejó de ser válida. Recargue la pantalla.'
                : 'No se pudo completar la operación. Intente nuevamente.'));
            error.estado = respuesta.status;
            throw error;
        }
        if (!json) throw new Error('No se recibió una respuesta válida del servidor.');
        return datos;
    }
    function bloquear() {
        // Ante una respuesta incierta se permite reintentar exactamente el mismo pedido.
        app.querySelectorAll('input:not([type=hidden]), select, textarea, button').forEach(c => c.disabled = enviando || confirmando || !!pendiente);
        byId('registrar-pedido').disabled = enviando || confirmando || cargandoCatalogo || (!pendiente && (totalCarrito() > TOTAL_MAXIMO || !colaborador || !carrito.length || !byId('tipo-comida').value || [...app.querySelectorAll('.cantidad-pedido')].some(c => !cantidadValida(c.value))));
        byId('registrar-pedido').textContent = enviando ? 'Registrando…' : pendiente ? 'Reintentar registro' : 'Registrar pedido';
        if (!enviando && !confirmando && !pendiente) { byId('agregar').disabled = cargandoCatalogo || byId('articulo').options.length < 2; byId('actualizar-catalogo').disabled = cargandoCatalogo; }
    }
    function pintar() {
        const cuerpo = byId('lineas-pedido');
        cuerpo.replaceChildren();
        let total = 0n;
        carrito.forEach((linea, indice) => {
            const fila = document.createElement('tr');
            const nombre = document.createElement('td');
            nombre.textContent = linea.nombre + (linea.tamano ? ' · ' + linea.tamano : '');
            const precio = document.createElement('td'); precio.textContent = moneda(linea.precio);
            const celdaCantidad = document.createElement('td');
            const entrada = document.createElement('input');
            Object.assign(entrada, { type: 'number', min: '1', max: String(CANTIDAD_MAXIMA), step: '1', value: linea.cantidad, className: 'form-control cantidad-pedido' });
            entrada.setAttribute('aria-label', 'Cantidad de ' + nombre.textContent);
            limitarDigitos(entrada);
            entrada.addEventListener('input', () => {
                const valida = cantidadValida(entrada.value);
                entrada.setCustomValidity(valida ? '' : 'Indique una cantidad entera entre 1 y ' + CANTIDAD_MAXIMA + '.');
                entrada.setAttribute('aria-invalid', String(!valida));
                if (valida) {
                    linea.cantidad = Number(entrada.value);
                    celdaSubtotal.textContent = moneda(BigInt(linea.precio) * BigInt(linea.cantidad));
                    actualizarTotal();
                }
                bloquear();
            });
            entrada.addEventListener('change', () => {
                if (!cantidadValida(entrada.value)) mensaje('La cantidad debe ser un número entero entre 1 y ' + CANTIDAD_MAXIMA + '.');
            });
            celdaCantidad.append(entrada);
            const subtotal = BigInt(linea.precio) * BigInt(linea.cantidad);
            total += subtotal;
            const celdaSubtotal = document.createElement('td'); celdaSubtotal.textContent = moneda(subtotal);
            const acciones = document.createElement('td');
            const quitar = document.createElement('button');
            Object.assign(quitar, { type: 'button', className: 'btn btn-outline-danger btn-sm', textContent: 'Quitar' });
            quitar.setAttribute('aria-label', 'Quitar ' + nombre.textContent);
            quitar.addEventListener('click', () => { carrito.splice(indice, 1); pintar(); });
            acciones.append(quitar);
            fila.append(nombre, precio, celdaCantidad, celdaSubtotal, acciones); cuerpo.append(fila);
        });
        byId('tabla-detalle').hidden = !carrito.length;
        byId('pedido-vacio').hidden = !!carrito.length;
        actualizarTotal();
        bloquear();
    }
    function reiniciar() {
        carrito = []; colaborador = null; pendiente = null; token = crypto.randomUUID(); busqueda++;
        byId('codigo-colaborador').value = ''; byId('tipo-comida').value = ''; byId('observaciones').value = '';
        pintarArticulos(); byId('cantidad').value = '1';
        byId('datos-colaborador').classList.add('d-none'); pintar();
    }
    async function cargarCatalogo(revisar = false) {
        if (enviando || pendiente || cargandoCatalogo) return;
        cargandoCatalogo = true; bloquear();
        try {
            const nuevos = await solicitar(app.dataset.catalogoUrl);
            catalogo = nuevos;
            pintarTipos();
            byId('estado-catalogo').textContent = catalogo.length ? 'Solo se muestran opciones activas con precio vigente.' : 'No hay artículos disponibles. Solicite al administrador revisar el catálogo.';
            if (revisar) {
                let cambios = false;
                carrito = carrito.flatMap(l => {
                    const actual = catalogo.find(a => a.clave === l.clave);
                    if (!actual) { cambios = true; return []; }
                    if (actual.precio !== l.precio) cambios = true;
                    return [{ ...actual, cantidad: l.cantidad }];
                });
                mensaje(cambios ? 'Se actualizaron precios o se quitaron opciones no disponibles. Revise el detalle y el total antes de registrar.' : 'El catálogo está actualizado.', cambios ? 'warning' : 'info');
            }
            pintar();
        } catch (error) { mensaje(error.message); }
        finally { cargandoCatalogo = false; bloquear(); }
    }
    byId('codigo-colaborador').addEventListener('input', () => {
        const teniaBorrador = carrito.length > 0 || byId('tipo-comida').value || byId('observaciones').value;
        busqueda++; colaborador = null; carrito = []; pendiente = null; token = crypto.randomUUID();
        byId('tipo-comida').value = ''; byId('observaciones').value = '';
        pintarArticulos(); byId('cantidad').value = '1';
        byId('datos-colaborador').classList.add('d-none'); pintar();
        if (teniaBorrador) mensaje('Se descartó el pedido pendiente porque cambió el código del colaborador. Busque al colaborador y prepare un nuevo pedido.', 'info', false);
    });
    byId('buscar-colaborador').addEventListener('submit', async evento => {
        evento.preventDefault(); limpiarMensaje();
        const intento = ++busqueda;
        colaborador = null; byId('datos-colaborador').classList.add('d-none'); bloquear();
        try {
            const datos = await solicitar(app.dataset.colaboradorUrl + '?codigo=' + encodeURIComponent(byId('codigo-colaborador').value.trim()));
            if (intento !== busqueda) return;
            colaborador = datos;
            byId('nombre-colaborador').textContent = datos.nombre;
            byId('codigo-encontrado').textContent = datos.codigo;
            const foto = byId('foto-colaborador');
            if (datos.fotoUrl) {
                foto.classList.remove('sin-foto'); foto.textContent = '';
                foto.style.backgroundImage = `url("${datos.fotoUrl}")`;
                foto.style.backgroundPosition = datos.posicionFoto;
                foto.setAttribute('aria-label', 'Foto de ' + datos.nombre);
            } else {
                // Las fotos todavía no existen: se muestran las iniciales.
                foto.classList.add('sin-foto'); foto.style.backgroundImage = '';
                foto.textContent = datos.nombre.split(/\s+/).filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
                foto.setAttribute('aria-label', 'Sin foto: ' + datos.nombre);
            }
            byId('datos-colaborador').classList.remove('d-none'); bloquear();
        } catch (error) { if (intento === busqueda) mensaje(error.message); }
    });
    byId('agregar-articulo').addEventListener('submit', evento => {
        evento.preventDefault();
        const articulo = catalogo.find(a => a.clave === byId('articulo').value);
        const cantidad = byId('cantidad').value;
        if (!articulo || !cantidadValida(cantidad)) { mensaje('Seleccione un artículo y una cantidad entera entre 1 y ' + CANTIDAD_MAXIMA + '.'); return; }
        const existente = carrito.find(l => l.clave === articulo.clave);
        if (existente) {
            if (!cantidadValida(existente.cantidad + Number(cantidad))) { mensaje('La cantidad total de un artículo no puede superar ' + CANTIDAD_MAXIMA + ' unidades.'); return; }
            existente.cantidad += Number(cantidad);
        } else carrito.push({ ...articulo, cantidad: Number(cantidad) });
        limpiarMensaje(); pintar();
    });
    byId('tipo-comida').addEventListener('change', pintarArticulos);
    limitarDigitos(byId('cantidad'));
    byId('actualizar-catalogo').addEventListener('click', () => cargarCatalogo(true));
    const modalDescarte = new bootstrap.Modal(byId('confirmar-descarte-pedido'));
    const modalDescartado = new bootstrap.Modal(byId('pedido-descartado'));
    let descarteAceptado = false;
    byId('confirmar-descarte-pedido').addEventListener('hidden.bs.modal', () => {
        confirmando = false;
        bloquear();
        if (descarteAceptado) {
            descarteAceptado = false;
            modalDescartado.show();
        } else byId('descartar-pedido').focus();
    });
    byId('pedido-descartado').addEventListener('hidden.bs.modal', () => byId('codigo-colaborador').focus());
    byId('descartar-pedido').addEventListener('click', () => {
        if (enviando || pendiente || confirmando) return;
        confirmando = true;
        descarteAceptado = false;
        byId('aceptar-descarte-pedido').disabled = false;
        bloquear();
        modalDescarte.show();
    });
    byId('aceptar-descarte-pedido').addEventListener('click', () => {
        if (!confirmando || enviando || pendiente || descarteAceptado) return;
        descarteAceptado = true;
        byId('aceptar-descarte-pedido').disabled = true;
        reiniciar();
        limpiarMensaje();
        modalDescarte.hide();
    });
    const modalRegistro = new bootstrap.Modal(byId('confirmar-registro-pedido'));
    const modalExito = new bootstrap.Modal(byId('pedido-registrado'));
    byId('pedido-registrado').addEventListener('hidden.bs.modal', () => byId('codigo-colaborador').focus());
    byId('confirmar-registro-pedido').addEventListener('hidden.bs.modal', () => {
        confirmando = false;
        bloquear();
        if (!enviando) byId('registrar-pedido').focus();
    });
    byId('registrar-pedido').addEventListener('click', () => {
        if (enviando || confirmando || byId('registrar-pedido').disabled) return;
        byId('confirmar-colaborador').textContent = colaborador.nombre + ' (' + colaborador.codigo + ')';
        byId('confirmar-total').textContent = byId('total-pedido').textContent;
        byId('aceptar-registro-pedido').disabled = false;
        byId('aceptar-registro-pedido').textContent = pendiente ? 'Sí, reintentar registro' : 'Sí, registrar pedido';
        confirmando = true;
        bloquear();
        modalRegistro.show();
    });
    byId('aceptar-registro-pedido').addEventListener('click', () => {
        if (!confirmando || enviando || byId('aceptar-registro-pedido').disabled) return;
        byId('aceptar-registro-pedido').disabled = true;
        // Esperar el cierre evita superponer las ventanas si el registro responde rápidamente.
        byId('confirmar-registro-pedido').addEventListener('hidden.bs.modal', registrarPedido, { once: true });
        modalRegistro.hide();
    });
    async function registrarPedido() {
        if (enviando || cargandoCatalogo || [...app.querySelectorAll('.cantidad-pedido')].some(c => !cantidadValida(c.value))) return;
        if (!pendiente) {
            if (!colaborador || !carrito.length || !byId('tipo-comida').value) return;
            pendiente = {
                tokenRegistro: token, codigoColaborador: colaborador.codigo, tipoComida: byId('tipo-comida').value,
                observaciones: byId('observaciones').value,
                lineas: carrito.map(l => ({ clave: l.clave, cantidad: l.cantidad, precioMostrado: l.precio }))
            };
        }
        enviando = true; bloquear(); limpiarMensaje();
        try {
            const resultado = await solicitar(app.dataset.registroUrl, {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': app.querySelector('input[name="__RequestVerificationToken"]').value },
                body: JSON.stringify(pendiente)
            });
            reiniciar();
            byId('numero-pedido-registrado').textContent = '#' + resultado.idPedido;
            byId('total-pedido-registrado').textContent = moneda(resultado.total);
            modalExito.show();
        } catch (error) {
            if (error.estado === 400) pendiente = null;
            mensaje(error.message + (pendiente ? ' Use “Reintentar registro” para comprobar o completar este mismo pedido sin duplicarlo.' : ''));
        } finally { enviando = false; bloquear(); }
    }
    // No se usan cookies ni almacenamiento del navegador para conservar borradores.
    // Impide recuperar un pedido sin registrar al volver atrás después de cerrar sesión.
    window.addEventListener('pagehide', reiniciar);
    window.addEventListener('pageshow', evento => { if (evento.persisted) { reiniciar(); location.reload(); } });
    document.querySelectorAll('form[action*="Logout"]').forEach(form => form.addEventListener('submit', reiniciar));
    cargarCatalogo();
})();
