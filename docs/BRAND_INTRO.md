# Introducción de Foodies

Secuencia de marca aprobada a partir del boceto: el asterisco naranja cae desde fuera de la pantalla al inicio del logotipo, avanza hacia la derecha, revela foodies a su izquierda y el overlay se desvanece. Fondo y tipografía heredados de Foodies.

Duración de movimiento: 500ms de caída, 400ms de recorrido, 500ms de revelado, 150ms de pausa y 250ms de salida; se espera como máximo 450ms por la fuente antes de medir el recorrido. Se emplean tokens de movimiento existentes. El texto permanece invisible hasta que el asterisco llega a su posición final.

Se monta en el layout raíz: se reproduce al cargar o recargar, sin repetirse en navegación cliente. La página carga debajo; durante la introducción se bloquean desplazamiento e interacción. Escape o un toque permiten omitirla. Se omite con movimiento reducido y si no está disponible Web Animations. Incluye liberación de emergencia a los 4 segundos y limpieza al desmontar.

Validación: compilación de producción, 15 pruebas existentes y comprobación real con Chrome a 390/1440px. Se verificaron fases y posiciones, revelado posterior al recorrido, ausencia de desbordamiento, retirada del overlay y liberación de la página, navegación cliente, Escape y movimiento reducido. Se revisaron las seis capturas .taste-skill/intro-{390,1440}-{fall,travel,reveal}.jpg. Script: .taste-skill/check-brand-intro.mjs.
