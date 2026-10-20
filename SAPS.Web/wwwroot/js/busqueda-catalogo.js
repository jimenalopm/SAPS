// Búsqueda en vivo del catálogo: mientras se escribe, la tabla de la pestaña muestra solo las filas que
// contienen todas las palabras digitadas (sin importar mayúsculas ni tildes). No recarga la página.
// Si la lista ya venía filtrada por el servidor (con ?catQ=..., etc.) y se escribe algo que no la refina,
// se vuelve a pedir al servidor para no esconder registros que no se cargaron.
(function () {
    'use strict';

    const normalizar = texto => (texto || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
    const cantidad = n => n === 1 ? '1 resultado' : n.toLocaleString('es-CR') + ' resultados';

    document.querySelectorAll('[data-busqueda-en-vivo]').forEach(function (campo) {
        const form = campo.form;
        const tabla = campo.closest('.tab-pane')?.querySelector('table');
        if (!form || !tabla || !tabla.tBodies[0]) return;

        const cuerpo = tabla.tBodies[0];
        const filas = [...cuerpo.rows].filter(f => !f.classList.contains('fila-vacia'));
        // Texto de cada fila sin la columna de acciones (botones Editar / Desactivar).
        const textos = filas.map(f => normalizar([...f.cells]
            .filter(c => !c.classList.contains('celda-acciones'))
            .map(c => c.textContent).join(' ')));
        const servidor = normalizar(campo.dataset.busquedaServidor);
        const contador = campo.dataset.contador ? document.querySelector(campo.dataset.contador) : null;
        const lupa = form.querySelector('.btn-lupa');

        // Fila de "sin resultados" y aviso para lectores de pantalla.
        const sinResultados = document.createElement('tr');
        sinResultados.className = 'fila-vacia';
        sinResultados.hidden = true;
        const celda = document.createElement('td');
        celda.colSpan = tabla.tHead?.rows[0]?.cells.length || 1;
        celda.className = 'estado-vacio';
        sinResultados.append(celda);
        cuerpo.append(sinResultados);
        const aviso = document.createElement('p');
        aviso.className = 'visually-hidden';
        aviso.setAttribute('aria-live', 'polite');
        form.append(aviso);

        let espera;
        const necesitaServidor = () => servidor !== '' && !normalizar(campo.value).includes(servidor);

        function filtrar() {
            if (necesitaServidor()) {
                clearTimeout(espera);
                espera = setTimeout(() => form.requestSubmit(), 450);
                return;
            }
            const palabras = normalizar(campo.value).split(/\s+/).filter(Boolean);
            let visibles = 0;
            filas.forEach(function (fila, i) {
                const coincide = palabras.every(p => textos[i].includes(p));
                fila.hidden = !coincide;
                if (coincide) visibles++;
            });
            sinResultados.hidden = !(filas.length > 0 && visibles === 0);
            if (!sinResultados.hidden) celda.textContent = 'No hay resultados para «' + campo.value.trim() + '».';
            if (contador) contador.textContent = visibles.toLocaleString('es-CR');
            aviso.textContent = palabras.length ? cantidad(visibles) : '';
        }

        campo.addEventListener('input', filtrar);
        // Enter o la lupa: la tabla ya está filtrada; solo se va al servidor si hace falta.
        form.addEventListener('submit', function (e) {
            if (e.submitter === lupa && !necesitaServidor()) {
                e.preventDefault();
                clearTimeout(espera);
                filtrar();
            }
        });
    });
})();
