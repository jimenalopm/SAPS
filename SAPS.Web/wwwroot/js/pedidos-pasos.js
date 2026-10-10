// Solo presentación de "Registrar pedido": indicador de pasos, resumen y etiquetas para la vista móvil.
// No hace llamadas al servidor ni toca la lógica de pedidos.js: únicamente LEE el estado que ya
// muestra la pantalla (colaborador identificado, artículos agregados, botón de registro habilitado).
(() => {
    'use strict';
    const byId = id => document.getElementById(id);
    const pasos = byId('pasos-pedido');
    if (!pasos || !byId('pedido-app')) return;

    // Etiquetas de columna para que cada renglón se lea como tarjeta en pantallas angostas.
    const etiquetas = ['Artículo', 'Precio', 'Cantidad', 'Subtotal', ''];
    const etiquetar = () => byId('lineas-pedido').querySelectorAll('tr').forEach(fila =>
        fila.querySelectorAll('td').forEach((celda, i) => {
            if (etiquetas[i] && !celda.dataset.label) celda.dataset.label = etiquetas[i];
            if (i === 0) celda.classList.add('celda-principal');
            if (i === 4) celda.classList.add('celda-acciones');
        }));

    function actualizar() {
        etiquetar();
        const colaborador = !byId('datos-colaborador').classList.contains('d-none');
        const hayArticulos = !byId('tabla-detalle').hidden;
        const tipo = byId('tipo-comida');
        const comida = tipo.value ? tipo.options[tipo.selectedIndex].text : '';

        byId('resumen-colaborador').textContent = colaborador
            ? byId('nombre-colaborador').textContent + ' · ' + byId('codigo-encontrado').textContent : '—';
        byId('resumen-comida').textContent = comida || '—';

        const actual = !colaborador ? 1 : (hayArticulos && comida ? 3 : 2);
        pasos.querySelectorAll('.paso').forEach(paso => {
            const n = Number(paso.dataset.paso);
            paso.classList.toggle('activo', n === actual);
            paso.classList.toggle('completo', n < actual);
            if (n === actual) paso.setAttribute('aria-current', 'step'); else paso.removeAttribute('aria-current');
        });
    }

    const observador = new MutationObserver(actualizar);
    observador.observe(byId('datos-colaborador'), { attributes: true, attributeFilter: ['class'] });
    observador.observe(byId('tabla-detalle'), { attributes: true, attributeFilter: ['hidden'] });
    observador.observe(byId('lineas-pedido'), { childList: true });
    byId('tipo-comida').addEventListener('change', actualizar);
    actualizar();
})();
