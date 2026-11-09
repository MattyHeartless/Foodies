# Plataforma · Diseño y verificación

Dirección de design-taste-frontend: **Framed sheet**, seed **1500279494**. Sin redraws. Se eligió por el trabajo de gestión: directorio, ficha y formularios. Marco gris azulado, hoja blanca, IBM Plex Sans cargada, tipografía de datos tabular y acción principal oscura. El asterisco de Foodies se dibuja en SVG y funciona como identificador de producto; no hay fotografías ni material externo que acreditar.

Pregunta de cada vista: login, quién puede administrar; directorio, qué tiendas existen y están publicadas; ficha, cómo opera esa tienda; responsables, quién tiene acceso; contraseña, cómo actualizar el acceso de plataforma. La firma del directorio reúne publicación, horario y encargado sobre cada tienda. Los conteos se calculan desde la respuesta real. No se inventaron estadísticas ni personas.

## Crítica final de app screen

1. La lista de tiendas domina el directorio; crear una tienda es una acción directa desde la cabecera. En móvil la primera tienda está en la mitad superior de la pantalla.
2. Marco de 228px, hoja con 24px interiores y bordes alineados; formularios de etiqueta + panel y lista convertida en filas de dos líneas. Navegación plegada en tablet y móvil, sin desbordamiento a 320, 390 y 768px.
3. El color se usa en la marca, la selección y los estados de publicación. Sin iconos en recuadros, gráficos decorativos, gradientes ni sombras en paneles. El nombre de Foodies y el ámbito de plataforma permanecen visibles.
4. Los valores del directorio se contrastaron con la respuesta real: 2 tiendas, 2 publicadas y 0 borradores al revisar. Esos valores no están escritos en la interfaz y cambian al administrar tiendas.
5. Ceros visibles como cero y estado vacío con una explicación y los mismos filtros; no hay paginación artificial para dos filas.
6. Sin gráficos o leyendas innecesarios. Estado, horario y responsables aparecen sobre su propia tienda.
7. El menú móvil conserva el foco, cierra con Escape y bloquea la hoja mientras está abierto. Campos móviles de 16px y controles táctiles de al menos 44px.

Valoración del componente completo: diseño 8.2, uso 8.5, contenido 9. No se aplica la rúbrica de landing pages ni un mínimo de imágenes o animaciones a este panel.

## Inspector y evidencia

- Login real de producción y snapshots de las vistas autenticadas, extraídos del DOM de producción tras el login real. Las capturas preservan valores de inputs, selects y checkboxes. Los snapshots son referencias visuales; la interacción se prueba sobre la aplicación real.
- Inspección a 1440, 768 y 390px: **0 FAIL** en login, directorio, alta, configuración, responsables, contraseña y búsqueda vacía. Lint de los tres archivos de UI: **0 FAIL, 0 WARN**.
- Se revisaron las capturas de todos los tamaños y estados, incluidas sus hojas de página completa. Están en `.taste-skill/platform/{login,stores,create,settings,members,security,empty}` y resumidas en `*-contact.jpg`.
- Advertencias defendidas: metadatos de 12px en el directorio móvil; espacio libre en la hoja de contraseña y en la búsqueda sin resultados, porque no se añade contenido inventado ni se estiran sus formularios. La hoja es el marco de la aplicación, no una tarjeta de contenido que deba llenarse.
- Formulario de alta fotografiado con “Sucursal de prueba” únicamente en memoria; no se guardó como tienda. Las tiendas y responsables mostrados son los datos locales existentes.

## Verificación funcional

- 42 pruebas backend correctas, incluidas 7 nuevas de plataforma, sobre **Foodies_Tests** con SQL Server real. Comprueban altas, publicación, validaciones, slug duplicado, asignación y revocación de permisos, cookies independientes, CSRF, bloqueo de intentos, cambio de contraseña y baja de administrador.
- 15 pruebas frontend existentes correctas y build de producción correcto.
- Navegador real: login/logout con la cuenta provisionada, listado de tiendas real, búsqueda vacía, formulario de alta, slug generado, horario semanal, ficha, responsables y contraseña; navegación móvil y foco. No se modificaron tiendas de producción durante estas comprobaciones de UI.
- Smoke HTTP: SQL, aislamiento de tiendas, sesión y CSRF de clientes/plataforma, directorio administrativo protegido y 8 rutas Next.js.
- OpenAPI actualizado con 9 operaciones de plataforma; `contracts/build.mjs` conserva su generación. CLI Postman instalada sin comando `status` disponible; no se atribuye ejecución a Postman. La evidencia de API procede de las pruebas SQL descritas arriba.

El primer acceso local está en `.runtime/platform-access.json`, excluido del repositorio y del sitio público. La contraseña se almacena con hash en SQL y puede cambiarse en Tu acceso. Las cuentas de Google deben haberse registrado antes de poder asignarles una tienda. No se implementaron invitaciones por correo ni registro público de administradores.
