// [Usabilidad HU-015, H3] Confirmación antes de guardar un cambio de precio.
// Se activa en los formularios con data-confirmar-precio. Compara cada campo marcado con data-precio contra su
// valor original (defaultValue) y, si alguno cambió, deja el mensaje en data-confirmar para que site.js muestre
// la ventana de confirmación. Si ningún precio cambió, el formulario se envía sin preguntar.
// Corre en la fase de captura, antes del manejador de envío de site.js.
(function () {
    'use strict';

    function colones(valor) {
        const n = Number(valor);
        return Number.isFinite(n) && valor !== '' ? '₡' + n.toLocaleString('es-CR') : valor;
    }

    function cambiosDePrecio(form) {
        const cambios = [];
        const modoTamano = form.querySelector('#requiereTamano');
        if (modoTamano && modoTamano.checked !== modoTamano.defaultChecked) {
            cambios.push(modoTamano.checked ? 'Pasa de precio único a precio por tamaño.' : 'Pasa de precio por tamaño a precio único.');
        }
        form.querySelectorAll('input[data-precio]').forEach(function (campo) {
            if (campo.disabled) return;   // bloque de precio que no se usa (único o por tamaño)
            const fila = campo.closest('.precio-tamano-fila');
            const incluir = fila ? fila.querySelector('input[type="checkbox"]') : null;
            const etiqueta = campo.dataset.precio;
            if (incluir && !incluir.checked) {
                if (incluir.defaultChecked) cambios.push(etiqueta + ': se quita ' + colones(campo.defaultValue));
                return;
            }
            if (incluir && !incluir.defaultChecked) {
                cambios.push(etiqueta + ': nuevo ' + colones(campo.value));
                return;
            }
            if (campo.value !== campo.defaultValue && campo.value !== '') {
                cambios.push(etiqueta + ': ' + colones(campo.defaultValue) + ' → ' + colones(campo.value));
            }
        });
        return cambios;
    }

    document.addEventListener('submit', function (evento) {
        const form = evento.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirmar-precio')) return;
        if (form.dataset.confirmado === 'true') return;

        const cambios = cambiosDePrecio(form);
        if (cambios.length === 0) {
            delete form.dataset.confirmar;
            return;
        }
        const nombre = form.querySelector('[data-nombre-articulo]');
        const articulo = nombre && nombre.value ? ' de «' + nombre.value.trim() + '»' : '';
        form.dataset.confirmar = '¿Confirma el cambio de precio' + articulo + '?\n\n' + cambios.join('\n') +
            '\n\nEl nuevo precio aplica a los pedidos siguientes (los ya registrados conservan su precio) ' +
            'y el cambio queda registrado en el historial de precios.';
    }, true);
})();
