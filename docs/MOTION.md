# Movimiento de Foodies

Integración de `transitions-dev` y `transitions-polish` sobre el diseño existente. Las transiciones dan continuidad a acciones y estados; no retrasan las solicitudes de negocio.

## Aplicación

- Diálogos de acceso, rechazo, platillos y categorías: apertura de 250 ms, cierre de 150 ms y escala de 0.96 a 1. El contenido permanece montado hasta terminar el cierre. Escape, clic en fondo, ciclo de Tab y recuperación del foco se manejan en un componente común.
- Avisos: entrada de 400 ms y salida de 350 ms, con desenfoque ligero. Repetir el mismo aviso renueva el mensaje y su permanencia. Botón para descartarlo.
- Categorías, filtros administrativos y navegación de tienda: indicador deslizante de 250 ms, medido sobre el control activo. Primera posición y cambios de tamaño se aplican sin recorrido inicial. Los controles mantienen sus estados accesibles.
- Cantidades, total del carrito y contadores de cocina: animación de 250 ms únicamente en los dígitos que cambian. Las actualizaciones REST con el mismo valor no vuelven a animar los números. Texto accesible separado de los caracteres visuales.
- Resumen de solicitud: entrada de 400 ms y salida de 350 ms. Se vuelve inerte inmediatamente al cerrar.
- Disponibilidad de platillos: movimiento breve del interruptor al recibir el estado confirmado por la API; sin rebote inicial ni confirmación visual anticipada.
- Catálogo y tarjetas de pedidos: aparición breve y escalonado de 40 ms en las primeras tarjetas. Interacciones de botones y tarjetas comparten tiempos y curvas. El menú móvil alterna los iconos y aparece con una transición breve.
- Cambios del estado de pedido: color y texto con transición discreta, sin repetir el efecto en cada sondeo.

## Archivos

`frontend/src/app/motion-tokens.css` contiene las variables originales compartidas de Transitions.dev. `motion-recipes.css` conserva las recetas CSS seleccionadas y sus protecciones de movimiento reducido. `motion.css` adapta los tokens, colores, geometría y estados al diseño de Foodies. No se agregó una biblioteca de animación en tiempo de ejecución.

`frontend/src/components/motion.tsx` centraliza permanencia durante el cierre, diálogos, avisos, números, pestañas, interruptores y resumen. Los temporizadores de salida leen las variables CSS; al reabrir se cancelan los cierres pendientes. Los tiempos de negocio y de red no se cambian.

## Accesibilidad y validación

Con `prefers-reduced-motion: reduce` se eliminan animaciones y transiciones, y los cierres no esperan al temporizador visual. Se mantienen textos y controles disponibles; no se oculta contenido como requisito para usarlo.

```powershell
cd frontend
npm run test:motion
npm run build
```

Nueve pruebas de componentes en JSDOM comprueban permanencia del formulario durante salida, foco y teclado, reapertura durante cierre, movimiento reducido, avisos repetidos, posición del indicador, dígitos estables, estado confirmado del interruptor y cierre inerte del resumen. JSDOM valida comportamiento DOM; no comprueba píxeles, suavidad, curvas ni medidas reales del navegador.

La revisión visual no se pudo completar: el host de acceso al navegador no estaba disponible y Computer Use se detuvo al no poder confirmar la URL de Chrome con suficiente confianza. La revisión manual recomendada es abrir/cerrar acceso, agregar un platillo varias veces, cambiar categorías y activar movimiento reducido en el sistema.
