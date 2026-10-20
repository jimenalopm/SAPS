// Modo claro / oscuro. Se carga en el <head> (antes de dibujar la página) para que no haya un destello claro
// al abrir cada pantalla. La elección se guarda en este navegador; si nunca se eligió, se usa la del sistema.
(function () {
    'use strict';
    var tema = null;
    try { tema = localStorage.getItem('saps-tema'); } catch (e) { /* almacenamiento bloqueado: se usa el del sistema */ }
    if (tema !== 'oscuro' && tema !== 'claro') {
        tema = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'oscuro' : 'claro';
    }
    var raiz = document.documentElement;
    raiz.setAttribute('data-tema', tema);
    raiz.setAttribute('data-bs-theme', tema === 'oscuro' ? 'dark' : 'light');
})();
