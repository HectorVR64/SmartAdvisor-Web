// Transición entre secciones.
// En navegadores con View Transitions entre páginas (Chrome, Edge, Safari reciente) la animación
// la hace el CSS (@view-transition). En los demás, se anima la salida del contenido y luego se navega.
(function () {
  var soportaTransicion = 'CSSViewTransitionRule' in window;
  var reducido = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (soportaTransicion || reducido) return;

  document.addEventListener('click', function (e) {
    var a = e.target.closest && e.target.closest('.lateral a[href]');
    if (!a || e.metaKey || e.ctrlKey || e.shiftKey || a.target === '_blank') return;
    var url = a.getAttribute('href');
    if (!url || url.charAt(0) === '#' || a.classList.contains('activo')) return;
    e.preventDefault();
    var c = document.querySelector('.contenido');
    if (c) c.classList.add('saliendo');
    setTimeout(function () { window.location.href = url; }, 220);
  });
})();
