// Inicio: cada tarjeta abre, en la misma pantalla, el panel con su detalle resumido.
// Solo hay un panel abierto a la vez. Se cierra con la X, con Esc o tocando de nuevo la tarjeta.
(function () {
    'use strict';
    const tarjetas = [...document.querySelectorAll('[data-panel]')];
    if (!tarjetas.length) return;
    const panelDe = tarjeta => document.getElementById(tarjeta.dataset.panel);
    let abierta = null;

    function cerrar(devolverFoco) {
        if (!abierta) return;
        const panel = panelDe(abierta);
        if (panel) panel.hidden = true;
        abierta.setAttribute('aria-expanded', 'false');
        abierta.classList.remove('abierta');
        if (devolverFoco) abierta.focus();
        abierta = null;
    }

    function abrir(tarjeta) {
        cerrar(false);
        const panel = panelDe(tarjeta);
        if (!panel) return;
        panel.hidden = false;
        tarjeta.setAttribute('aria-expanded', 'true');
        tarjeta.classList.add('abierta');
        abierta = tarjeta;
        panel.focus({ preventScroll: true });
        panel.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'nearest' });
    }

    tarjetas.forEach(t => t.addEventListener('click', () => (abierta === t ? cerrar(true) : abrir(t))));
    document.querySelectorAll('[data-cerrar-panel]').forEach(b => b.addEventListener('click', () => cerrar(true)));
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && abierta) cerrar(true); });
})();
