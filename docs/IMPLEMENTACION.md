# Análisis y evidencia del MVP

## Actualización: teléfono temporal sin gateway

El propietario autorizó guardar el teléfono sin PIN mientras se integra SMS. Se agregó `PATCH /me/phone`, validación E.164, política compartida por sesión/API y cambios en perfil/carrito. El teléfono guardado permanece sin verificar; cambiar el número invalida la verificación y códigos anteriores. Sin número sigue sin ser posible solicitar.

La configuración local habilita `PhoneVerification:AllowUnverifiedOrders=true`; por defecto es false. `Sms:Enabled=true` exige siempre verificación, independientemente de ese permiso temporal o de la disponibilidad del gateway. No se encolan SMS para teléfonos sin verificar. No requiere migración ni cambios manuales en los datos del usuario.

La suite actual tiene **35 pruebas aprobadas**: incluye guardar/validar el teléfono, solicitar sin PIN en modo temporal, bloquearlo en modo estricto, exigir un número, invalidar códigos al cambiarlo, protección CSRF y las cuatro combinaciones de configuración SMS/permiso temporal. Build de Next.js y TypeScript aprobado. La evidencia por fases de abajo corresponde al alcance inicial previo a esta excepción autorizada.

El documento se implementó siguiendo su flujo central: elegir tienda → elegir platillos → solicitar. El catálogo es público; confirmar requiere sesión y teléfono verificado. No se habilitaron funciones fuera de alcance ni las políticas pendientes de cancelación/no recogido.

## Decisiones mínimas

| Tema | Decisión y motivo |
|---|---|
| Base de datos | Foodies independiente en el servidor indicado; contribuciones no se modifica. Foodies_Tests para pruebas reales. |
| Arquitectura | Una API ASP.NET Core/EF Core y una aplicación Next.js con rutas públicas/privadas. |
| Nombre/moneda | Foodies provisional y MXN para el piloto; decisiones explícitas, reemplazables. |
| Permisos | Membership por usuario/tienda; cocina opera pedidos/catálogo, encargado también configuración. CLI para alta y asignación. |
| Concurrencia | sp_getapplock dentro de transacciones serializables y rowversion del pedido. Unicidad de idempotencia por cliente y outbox por pedido/tipo. |
| Precios | decimal(18,2), expectedUnitPrice para detectar cambios; no se acepta el total calculado en el cliente. |
| Tiempo real | SSE por tienda para una instancia, heartbeat de 15 s y reconexión de 5 s. REST inmediato al conectar y respaldo cada 20 s. |
| Cuenta | Google OAuth con PKCE; sesión HttpOnly y CSRF. Acceso local solo Development, sin verificación ficticia de teléfono. |
| SMS | Adaptador HTTP sustituible y outbox persistente, deshabilitado hasta conocer el contrato. Sin suponer endpoint ni credenciales reales. |
| Imágenes | URLs opcionales; ejemplos de Unsplash. No se introdujo carga de archivos. |

## Cierre por fase

| Fase | Dependencias/riesgos identificados | Resultado verificable |
|---|---|---|
| 0. Fundaciones | Credenciales SQL, aislamiento y migraciones. La contraseña inicial fue rechazada y la corregida funcionó. | API y solución .NET, configuración excluida de Git, Foodies creada por EF, dos tiendas sembradas, contrato OpenAPI. |
| 1. Tiendas/catálogo | Filtrado obligatorio por StoreId y categorías activas. | Landing, menú, CRUD lógico de categorías/platillos, disponibilidad y configuración por rol. Build Next.js y lecturas HTTP reales. |
| 2. Identidad/perfil | Credenciales Google y contrato SMS, pospuestos expresamente por el usuario. | Código de Google/cookies/CSRF, perfil y OTP. Verificados controles de OTP mediante registros de prueba. Acceso Google y SMS reales pendientes. |
| 3. Pedidos | Revalidaciones, carreras de estado, doble clic y snapshots históricos. | Pedidos persistidos, estados e historial, ETA de 16 min con tres anteriores; concurrencia e idempotencia probadas contra SQL. |
| 4. SSE/SMS | SSE requiere REST de recuperación; gateway aún no definido. | Stream SSE real autorizado probado; outbox único al pasar a Listo. Worker y adaptador preparados, entrega externa/reintentos contra proveedor pendientes. |
| 5. Calidad | Separación entre tiendas, accesibilidad y revisión visual. | 29 pruebas aprobadas sobre SQL Server y reglas puras; build producción/TypeScript aprobado; smoke HTTP de aplicación completa aprobado. Revisión visual de escritorio/móvil pendiente por herramientas. |

## Evidencia de pruebas

29 casos aprobados, 0 fallidos y 0 omitidos en la última ejecución (configuración Release):

- Fórmula de estimación, estados activos/excluidos y transiciones prohibidas.
- Zona horaria, cierre exclusivo, horarios nocturnos y partidos, pausa.
- Catálogo público y cookies/CSRF; mutaciones autenticadas sin CSRF rechazadas.
- Ciclo completo contra SQL Server; folio, snapshots históricos y estimación estables después de editar el catálogo.
- Seis confirmaciones simultáneas con igual clave producen un solo pedido; payload distinto con igual clave devuelve conflicto.
- Dos aceptaciones simultáneas producen una sola transición/historial.
- Mínimo, precio cambiado, disponibilidad, platillos de otra tienda, cierre y teléfono sin verificar.
- Lecturas administrativas y stream de otra tienda rechazados; pedidos ajenos no revelados; cocina sin permiso para configurar.
- Rechazo con motivo y sin SMS de listo; repetir Listo no duplica outbox.
- OTP: intentos máximos, caducidad, verificación y consumo único; gateway deshabilitado no simula éxito.
- Rate limiting de confirmaciones.
- Challenge Google con PKCE y callback del origen público HTTPS, cookies Secure y ausencia del acceso Development en producción. Esta prueba no llama a Google ni verifica un inicio de sesión externo.
- Eventos SSE reales a la tienda autorizada y separación de canales.

La cuenta telefónica verificada de los tests existe exclusivamente en Foodies_Tests. No se marcaron como verificados los usuarios de la aplicación.

La prueba HTTP a través de Next.js comprobó SQL disponible, menús de dos tiendas, sesión/CSRF y siete rutas web con respuesta 200. No equivale a una prueba visual del carrito en navegador.

## Pendientes aceptados y límites

1. Proporcionar Google ClientId/ClientSecret y probar acceso real, conservación de carrito y cookies con HTTPS.
2. Proporcionar contrato del SMS Gateway, adaptar HttpSmsGateway y verificar envío OTP, timeout, error, reintentos e idempotencia del proveedor. La unicidad del outbox está probada; la entrega física no.
3. Revisar visualmente móvil y escritorio y completar E2E de tres etapas cuando se autorice el navegador.
4. Definir branding final, políticas de cancelación/no recogido, feriados y almacenamiento de imágenes según el piloto.

La revisión automática rechazó la acción de controlar Chrome con el motivo “Computer Use was not approved to use Google Chrome”. La CLI Postman falló al arrancar por un manifiesto de Private Cloud impuesto por política; no se modificó esa política.

La instancia local quedó accesible para revisión. El entorno del agente denegó terminar sus procesos de servidor iniciados en segundo plano. Se conserva `.runtime/processes.json` para que el usuario pueda ejecutar `dev.ps1 stop` desde su terminal. El script publica próximas instancias en `.runtime/api` para evitar bloquear los archivos de compilación.

## Referencias técnicas consultadas

- [OAuth social en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/)
- [Concurrencia de EF Core y rowversion](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [Proxy mediante rewrites de Next.js](https://nextjs.org/docs/app/api-reference/config/next-config-js/rewrites)
