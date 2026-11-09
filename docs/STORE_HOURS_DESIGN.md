# Horario compacto · referencia aprobada

Se aplicó la franja aprobada por el usuario, siguiendo design-taste-frontend y la identidad existente de Foodies. No hubo sorteo ni nuevas imágenes: se mantuvieron colores y tipografías. El icono de reloj es funcional, sin contenedor ornamental. El horario ocupa 88px contraído en el ejemplo; al tocarlo se despliega la semana mediante details/summary nativos.

Los datos proceden de la tienda. Lun–Dom aparece únicamente cuando todos los días comparten exactamente los mismos intervalos; en los demás casos se muestra Hoy. Se mantienen zona horaria, jornadas partidas, días cerrados, cruce de medianoche y estado operativo recibido. Sin horarios publicados se muestra Por confirmar, sin flecha inactiva.

## Validación

- Compilación de producción correcta y servidor reiniciado en http://127.0.0.1:3002.
- 6 pruebas de horarios correctas, incluyendo conservación del despliegue al refrescar los datos.
- Lint del componente y CSS: 0 FAIL, 0 WARN.
- Inspector del componente contraído y abierto: 0 FAIL, 2 WARN a 390, 768 y 1440px. Se conserva el fondo de marca; el espacio vacío de la página de referencia corresponde al aislamiento del componente.
- Revisadas todas las capturas de ambos estados y la captura móvil del catálogo de producción.
- El catálogo conserva los cuatro avisos previos de diseño: símbolo del logotipo, medición de contraste del tab con indicador independiente, iconos de otros controles y textos pequeños ajenos al componente. No se atribuye un pase completo al catálogo.

## Crítica del componente

ROUND 1
Seen in one second: reloj, horario de apertura y estado actual.
Idea (one sentence): el horario se lee como una sola franja, con la semana disponible a un toque.
Live thing: despliegue nativo de los horarios reales, con el día local destacado.
Layout words by section: resumen=franja compacta / semana=lista desplegable.
Scores: design 8 · usability 8.5 · creativity 7 · content 9 → overall 8.1.
Caps applied: creatividad limitada a 7 por tratarse de un patrón compartido por otras tiendas; evaluación del componente, no de la página de referencia vacía.
Top three problems (most visible first), each with the screenshot it shows in and the fix:
1. El bloque anterior repetía título, día y zona horaria: sustituido por dos líneas en hours-compact/mobile-fold.jpg.
2. El detalle semanal anterior tenía una segunda caja y otro rótulo: integrado en la misma superficie, hours-compact-expanded/mobile-fold.jpg.
3. El estado quedaba separado del horario: alineado junto al título, hours-compact/mobile-fold.jpg. Sin problemas pendientes visibles en el componente.

## Capturas

- Contraído: ../.taste-skill/hours-compact/mobile-fold.jpg
- Abierto: ../.taste-skill/hours-compact-expanded/mobile-fold.jpg
- Catálogo de producción: ../.taste-skill/hours-compact-live/mobile-page-01.jpg

## Movimiento de la lista

Se incorporó la receta 21-accordion.md de transitions-dev: grid 0fr a 1fr, opacidad y desenfoque de entrada/salida, con inversión vertical de la flecha. Se reutilizan los tokens --acc-* existentes, de 250ms. El despliegue ahora usa un botón con aria-expanded y aria-controls; el contenido cerrado queda inert y aria-hidden. La preferencia de movimiento reducido elimina la transición.

Verificación adicional: 6 pruebas de horarios y 9 de movimiento correctas, compilación de producción correcta. Chrome comprobó a 390 y 1440px que la altura pasa por valores intermedios al abrir y cerrar, termina en cero al cerrar, resiste clics rápidos, no desborda horizontalmente y responde de inmediato con movimiento reducido. Capturas revisadas en .taste-skill/hours-motion-390.jpg y hours-motion-1440.jpg. Estas comprobaciones sustituyen las referencias anteriores a details/summary nativos.
