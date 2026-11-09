# Perfil de usuario · Diseño

Dirección de design-taste-frontend: Editorial greeting, seed 271987987, manteniendo DM Sans/Manrope y la paleta de Foodies. El marco del sitio existente se conserva en lugar del marco administrativo del sorteo: el perfil pertenece al recorrido público del cliente. La identidad y el estado de verificación se leen antes de los formularios; no hay fotografías, estadísticas ni elementos decorativos inventados.

La pregunta principal es si el usuario está listo para solicitar y recoger comida. Composición de 12 columnas: datos en 5, celular en 7, un espacio de 24px y márgenes iguales. En móvil se apilan con el celular primero. El verde salvia distingue la función de avisos y el avatar usa iniciales de la cuenta. Los estados de número pendiente, PIN solicitado, error y verificado usan el mismo espacio.

El prefijo +52 está fuera del input. Se aceptan diez dígitos mexicanos o un número pegado con +52, espacios o guiones; se normaliza a +52 más diez dígitos antes de usar los endpoints existentes. Se conservan los PIN con cero inicial, el reenvío limitado, el cambio de número y el retorno al carrito. No se modificó el backend ni el requisito de SMS de exactamente 180 caracteres.

## Revisión de las capturas

Antes: formulario largo, título de 42px, prefijo dentro del campo, aviso de verificación separado de su acción. Inspector: 2 FAIL y 4 WARN, incluyendo el símbolo de marca y texto global pequeño.

Después, a 1440/768/390px: 0 FAIL en los cuatro estados con excepción explícita para el asterisco existente de la marca (--allow emoji). Lint: 0 FAIL, 0 WARN. Se corrigió el contraste de navegación y pie solo en esta página y se quitaron las etiquetas decorativas. Se mantienen dos advertencias justificadas: el fondo claro forma parte de la estética solicitada y los dos formularios tienen alturas naturales distintas en escritorio para no generar bandas vacías; comparten borde superior y una sola columna en móvil.

Se revisaron las 24 capturas finales mediante sus hojas de contacto y vistas reales de la página. La fuente de cada snapshot es el DOM del build de producción. Los datos Mariana Torres, mariana@example.test y los números/PIN de prueba son fixtures de navegador, no cuentas ni contenido añadido al producto.

Preguntas de evaluación: la acción principal (verificar celular) se identifica primero; los bordes y márgenes son comunes; el color señala la función de avisos; no hay totales ni gráficos sin datos; los estados de error se muestran junto al campo; el móvil conserva ambas secciones sin scroll horizontal. Valoración visual: diseño 8, uso 8.5, contenido 8.5.

## Comprobaciones

Build de producción correcto y servidor reiniciado. Navegador real con respuestas interceptadas: número vacío sin lada editable, pegado de +52 con formato, payload E164, PIN con cero inicial, error de código, confirmación, guardado de nombre y ausencia de desbordamiento a 320/390/768px. Sin errores de consola. No se enviaron SMS ni se modificaron cuentas reales. Smoke HTTP de SQL, sesiones y ocho rutas correcto.

Script reproducible: .taste-skill/check-profile.mjs. Capturas: .taste-skill/profile/before y after. Los fixtures prueban la interfaz, no acreditan recepción de SMS real.

## Corrección mobile-first

La base CSS ahora corresponde al teléfono, sin reglas max-width que reparen un diseño de escritorio. Las mejoras para tablet y escritorio se añaden a 601px y 901px. Cabecera compacta sin descripción ni enlace redundante a tiendas en móvil, identidad en dos líneas y formulario de celular primero. Datos personales se despliegan solo al editarlos en móvil/tablet y permanecen visibles en escritorio. Prefijo separado, campos de 16px, botones de 48px y activador de datos de 44px.

Comprobación adicional en Chrome real a 320/390/768px con viewport de 667px de alto: el botón de solicitar PIN queda completamente en la primera pantalla, no hay overflow horizontal y el formulario personal está plegado. Su despliegue, guardado de nombre y el flujo de PIN simulado también funcionan. Se conserva la dirección Editorial greeting y los colores/tipos de Foodies; se elimina la repetición que alargaba el móvil. Se revisan nuevamente los cuatro estados en todos los tamaños. No se enviaron SMS reales.
Excepción adicional de inspector: palette-claude se permite únicamente por el asterisco naranja del logotipo sobre el fondo de marca existente; no se introdujo terracota en formularios ni textos del perfil. El encogimiento del móvil deja visible ese activo y cambia la clasificación automática. Se conserva por petición explícita de mantener la estética del sitio. Resto de advertencias: alturas naturales de los formularios en escritorio, sin contenido de relleno.

El desplegable Datos personales incorpora la receta 21-accordion de transitions-dev ya instalada globalmente: altura mediante grid 0fr/1fr, desvanecido y desenfoque suaves, flecha scaleY. El cuerpo cerrado queda inert/aria-hidden y en escritorio se mantiene abierto. Usa los tokens --acc-* de 250ms y respeta movimiento reducido. La prueba real de navegador comprueba valores de altura intermedios al abrir/cerrar y altura final cero, además del flujo completo del formulario sin SMS reales. Build correcto y servidor reiniciado.
