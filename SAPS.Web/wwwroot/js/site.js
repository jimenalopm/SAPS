// Comportamientos comunes de SAPS.
// Se activan solo en los formularios que tengan estos atributos:
//   data-confirmar="mensaje"   -> [H3] pide confirmación antes de enviar.
//   data-bloquear-envio        -> [H3] deshabilita el botón y muestra "Guardando..." al enviar.

(function () {
    'use strict';

    // ¿El formulario pasa la validación de jQuery Validate? (si la página no la usa, se asume válido)
    function esValido(form) {
        if (window.jQuery && jQuery.fn.valid && jQuery(form).data('validator')) {
            return jQuery(form).valid();
        }
        return true;
    }

    // Deshabilita el botón de envío y muestra un indicador de procesamiento.
    function bloquearEnvio(form, boton) {
        form.dataset.enviando = 'true';
        const btn = boton || form.querySelector('button[type="submit"]');
        if (!btn) return;
        btn.dataset.textoOriginal = btn.innerHTML;
        btn.disabled = true;
        btn.setAttribute('aria-busy', 'true');
        const texto = btn.dataset.textoProcesando || 'Guardando...';
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>' + texto;
    }

    // Si la persona vuelve con el botón "Atrás", el navegador puede mostrar la página guardada
    // con el botón todavía deshabilitado. Aquí se restaura.
    window.addEventListener('pageshow', function () {
        document.querySelectorAll('form[data-enviando="true"]').forEach(function (form) {
            delete form.dataset.enviando;
            delete form.dataset.confirmado;
            form.querySelectorAll('button[aria-busy="true"]').forEach(function (btn) {
                btn.disabled = false;
                btn.removeAttribute('aria-busy');
                if (btn.dataset.textoOriginal) btn.innerHTML = btn.dataset.textoOriginal;
            });
        });
    });

    document.addEventListener('submit', function (evento) {
        const form = evento.target;
        if (!(form instanceof HTMLFormElement)) return;
        // Si otra validación ya canceló el envío (por ejemplo, campos con error), no se hace nada.
        if (evento.defaultPrevented) return;

        // Evita un segundo envío mientras el primero se procesa.
        if (form.dataset.enviando === 'true') {
            evento.preventDefault();
            return;
        }

        // [H3] Confirmación antes de acciones que cambian el estado de un registro.
        const mensaje = form.dataset.confirmar;
        if (mensaje && form.dataset.confirmado !== 'true') {
            evento.preventDefault();
            const modalEl = document.getElementById('modalConfirmar');
            if (!modalEl || !window.bootstrap) {
                // Respaldo si Bootstrap no cargó: confirmación nativa del navegador.
                if (window.confirm(mensaje)) { form.dataset.confirmado = 'true'; form.requestSubmit(); }
                return;
            }
            document.getElementById('modalConfirmarMensaje').textContent = mensaje;
            const aceptar = document.getElementById('modalConfirmarAceptar');
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            aceptar.onclick = function () {
                form.dataset.confirmado = 'true';
                modal.hide();
                form.requestSubmit();
            };
            modal.show();
            return;
        }

        // [H3] Indicador de procesamiento y bloqueo de doble envío.
        if (form.hasAttribute('data-bloquear-envio') && esValido(form)) {
            bloquearEnvio(form, evento.submitter);
        }
    });
})();

// Drawer del menú lateral (tablet y móvil). En escritorio el sidebar siempre está visible.
(function () {
    'use strict';
    const boton = document.getElementById('btn-menu');
    const menu = document.getElementById('sidebar');
    const fondo = document.getElementById('sidebar-fondo');
    if (!boton || !menu || !fondo) return;
    const movil = window.matchMedia('(max-width: 991.98px)');

    function fijar(abierto) {
        menu.classList.toggle('abierto', abierto);
        fondo.classList.toggle('abierto', abierto);
        boton.setAttribute('aria-expanded', String(abierto));
        boton.setAttribute('aria-label', abierto ? 'Cerrar menú de navegación' : 'Abrir menú de navegación');
        // Con el drawer cerrado en móvil, su contenido no debe ser alcanzable con el teclado.
        menu.toggleAttribute('inert', movil.matches && !abierto);
        document.body.style.overflow = movil.matches && abierto ? 'hidden' : '';
        if (abierto) menu.querySelector('a, button')?.focus();
    }

    boton.addEventListener('click', () => fijar(!menu.classList.contains('abierto')));
    fondo.addEventListener('click', () => { fijar(false); boton.focus(); });
    menu.addEventListener('click', e => { if (e.target.closest('a')) fijar(false); });
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && menu.classList.contains('abierto')) { fijar(false); boton.focus(); }
    });
    movil.addEventListener('change', () => fijar(false));
    fijar(false);
})();

/* Campos de precio: solo dígitos y nunca más que el máximo permitido (atributo max). */
(function () {
    function limitar(campo) {
        const max = parseInt(campo.getAttribute('max'), 10);
        if (!max) return;
        const digitos = String(max).length;
        let valor = campo.value.replace(/\D/g, '').slice(0, digitos);
        if (valor !== '' && parseInt(valor, 10) > max) valor = valor.slice(0, -1);
        if (valor !== campo.value) campo.value = valor;
    }
    document.addEventListener('input', function (e) {
        const campo = e.target;
        if (campo instanceof HTMLInputElement && campo.getAttribute('inputmode') === 'numeric' && campo.hasAttribute('max')) limitar(campo);
    });
    document.addEventListener('keydown', function (e) {
        const campo = e.target;
        if (campo instanceof HTMLInputElement && campo.getAttribute('inputmode') === 'numeric' && campo.hasAttribute('max') && ['e', 'E', '+', '-', '.', ','].includes(e.key)) e.preventDefault();
    });
})();
