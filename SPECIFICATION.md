# Documento inicial de análisis y alcance — Plataforma de pedidos anticipados

**Versión:** 0.1  
**Fecha:** 2026-11-03  
**Estado:** Especificación inicial para el agente de desarrollo  
**Nombre comercial:** Pendiente de definir  
**Propósito:** Establecer el MVP funcional, reglas de negocio, arquitectura propuesta, límites y criterios de aceptación antes de implementar.

> **Instrucción para el agente:** Implementar una solución sencilla, atractiva, responsiva y operativamente útil. No ampliar el alcance por iniciativa propia. Las decisiones identificadas como **Propuesta** o **Pendiente** no deben interpretarse como requisitos aprobados. Si una decisión bloquea el desarrollo, presentar la alternativa mínima y documentarla. No usar SignalR.

---

## 1. Contexto y problema

En zonas de oficinas, tiendas y pequeños establecimientos venden comida lista o preparada al momento. El cliente suele desplazarse, ordenar presencialmente y esperar alrededor de 10 minutos o más, reduciendo el tiempo disponible durante su jornada laboral.

La solución permite elegir un establecimiento, seleccionar los platillos y solicitar su preparación con anticipación. Cuando la orden está lista, el sistema notifica al cliente para que acuda a recogerla.

**Propuesta de valor:** «Elige tienda → elige platillos → solicita; recoge cuando esté listo».

### Objetivos del MVP

1. Permitir que el cliente complete la solicitud en **tres etapas funcionales**, excluyendo registro o inicio de sesión: **(1) elegir tienda, (2) elegir platillos, (3) solicitar**.
2. Reducir la espera física del cliente y simplificar el trabajo de recepción de pedidos.
3. Ofrecer un panel muy sencillo para tienda y cocina.
4. Enviar un SMS cuando la orden esté lista para recogerse.
5. Soportar **múltiples tiendas** con separación estricta de sus datos desde la primera versión, sin convertir el producto en una plataforma SaaS compleja.

### Principios de producto

- **Pocos pasos y poca fricción.** La exploración del catálogo no requiere iniciar sesión; el inicio de sesión se solicita al confirmar el pedido.
- **Mobile-first**, atractivo y claro, con buen funcionamiento en escritorio.
- **Sin pagos en línea** en el MVP; se paga directamente en el establecimiento al recoger.
- **Sin entregas a domicilio** ni programación para otra fecha en el MVP.
- **Confianza razonable en el usuario.** Los pedidos no recogidos pueden suceder, pero no se diseñará una experiencia restrictiva por defecto. Aplicar controles proporcionales y trazabilidad.
- Evitar funciones y abstracciones que no aporten valor al flujo esencial.

---

## 2. Stack y restricciones tecnológicas

| Capa | Selección |
|---|---|
| Frontend público, perfil y administración | **React.js con Next.js**, una sola aplicación web con rutas segmentadas |
| Backend de negocio | **ASP.NET Core Web API** (.NET) |
| Base de datos | **SQL Server** |
| Persistencia | **Entity Framework Core** — propuesta de implementación |
| Identidad | Inicio de sesión con **Google**, mediante OAuth 2.0 / OpenID Connect |
| Notificaciones externas | Integración HTTP contra **SMS Gateway existente** del propietario; solo se considera el consumo de su endpoint |
| Actualizaciones en tiempo real | **SSE nativo directamente desde la API ASP.NET Core** como primera opción; WebSocket nativo es alternativa válida si se justifica |
| Librería prohibida | **No utilizar SignalR** |

### Criterios técnicos

- La API es la fuente de verdad para pedidos, estados, validaciones y cálculos. El frontend no determina precios ni montos finales autoritativos.
- No crear microservicios innecesarios. Preferir **monolito modular** con separación clara de responsabilidades.
- Autenticación segura con flujo de autorización moderno y sesión/cookies `HttpOnly`, `Secure` cuando corresponda, política `SameSite` apropiada y protección CSRF si se usa autenticación por cookies. No persistir tokens sensibles en `localStorage`.
- Validar identidad en el backend. La sesión debe determinar usuario, permisos y tiendas autorizadas.
- Considerar reintentos/reconexión en SSE. Las lecturas REST siguen siendo fuente de verdad si un cliente se desconecta.
- No asumir proveedor de hosting, dominio, servicios de correo ni costos de infraestructura.

---

## 3. Actores y permisos

| Actor | Responsabilidades |
|---|---|
| **Visitante** | Ver tiendas y platillos disponibles sin cuenta; iniciar registro o acceso al intentar solicitar |
| **Cliente registrado** | Completar perfil, indicar/verificar número telefónico, solicitar platillos, consultar estado y pedidos previos |
| **Encargado de tienda** | Configurar su tienda, horarios, monto mínimo, tiempo promedio; gestionar pedidos y catálogo |
| **Personal de cocina** | Mantener la disponibilidad real de platillos y actualizar los pedidos para preparar y entregar |
| **Operador de plataforma** | Dar de alta y vincular administradores a tiendas; función mínima de operación interna, sin panel sofisticado en el MVP |

**Autorización:** un usuario puede ser cliente y estar asignado operativamente a una tienda. Los roles de tienda se asignan por establecimiento, no globalmente. **No** permitir acceder, por sustitución de identificadores en URL o payload, a recursos administrativos de otra tienda.

**Propuesta operativa:** unificar visualmente el panel del encargado y cocina; mostrar acciones según rol. No desarrollar dos aplicaciones administrativas.

---

## 4. Experiencia de usuario y páginas

### 4.1. Flujo público en tres etapas

**Etapa 1 — Elegir tienda**
- Landing con propuesta de valor y listado de tiendas disponibles.
- Cada tienda muestra nombre, estado operativo (abierta/cerrada/pausada), horario pertinente y una descripción breve.
- Al seleccionar una tienda se abre su menú.

**Etapa 2 — Elegir platillos**
- Listar categorías y platillos activos y disponibles en esa tienda, con nombre, descripción opcional, precio y foto opcional.
- Permitir agregar múltiples platillos, ajustar cantidad y eliminar artículos del carrito.
- Mostrar subtotal/total y mínimo requerido de la tienda.
- No permitir mezclar artículos de diferentes tiendas en el mismo pedido; solicitar confirmación si el usuario cambia de tienda con un carrito activo.

**Etapa 3 — Solicitar**
- Mostrar resumen simple antes del envío: tienda, conceptos, cantidades, total, modalidad **recoger y pagar en tienda**.
- Solicitar inicio de sesión con Google en este momento si es necesario y conservar el carrito tras completar el acceso.
- Exigir teléfono validado para recibir el aviso de listo; si no existe, capturarlo y verificarlo mediante un código enviado por el SMS Gateway.
- Confirmar pedido; registrar inmediatamente el estado **Pendiente de aceptación**.
- Mostrar folio, estado y detalles. Aclarar que la tienda aún debe aceptarlo.

### 4.2. Rutas sugeridas de la misma aplicación Next.js

| Ruta | Uso |
|---|---|
| `/` | Landing y selección de tienda |
| `/tiendas/[slug]` | Catálogo y carrito de la tienda; CTA para solicitar |
| `/mis-pedidos` | Listado e historial del cliente |
| `/mis-pedidos/[id]` | Detalle, estado y tiempo estimado después de la aceptación |
| `/perfil` | Identidad básica, teléfono verificado y datos de cuenta |
| `/admin` | Panel de pedidos de la tienda activa |
| `/admin/catalogo` | Categorías, platillos y disponibilidad |
| `/admin/configuracion` | Horarios, monto mínimo, promedio y pausa temporal |

El registro con Google es un flujo/modal, no exige necesariamente una página adicional. No crear un checkout de múltiples pantallas: **la solicitud debe ser directa**.

### 4.3. Vista administrativa

- Mostrar pedidos pendientes, aceptados, en preparación y listos.
- Permitir **Aceptar**, **Iniciar preparación**, **Marcar listo** y **Marcar entregado**, según corresponda.
- Disponer de acciones justificadas para **Rechazar** y registrar motivo.
- Indicadores simples de cantidad de órdenes por estado, sin panel analítico avanzado.
- Actualización de nuevos pedidos mediante SSE/API; señal visual y sonora del lado del navegador, sujeta a sus permisos/restricciones de reproducción.
- La cocina mantiene activo/inactivo cada platillo en función de su disponibilidad real.

---

## 5. Reglas de negocio

### RN-01. Aislamiento de tiendas
Cada tienda posee catálogo, categorías, pedidos, personal, horarios, monto mínimo y tiempo promedio propios. Una cuenta cliente puede ordenar en varias tiendas, pero **cada pedido pertenece a una sola tienda**.

### RN-02. Disponibilidad del establecimiento
Solo se pueden generar solicitudes si la tienda está activa, está dentro de su horario configurado y **no está pausada**. Fuera de horario se permite consultar el catálogo, pero no confirmar pedidos. El horario se evalúa con la zona horaria de la tienda.

### RN-03. Disponibilidad de platillos
Solo son solicitables productos activos y disponibles. La cocina administra dicha disponibilidad; se revalida **al confirmar** en el backend para evitar pedidos con artículos agotados.

### RN-04. Monto mínimo
El total debe alcanzar el monto mínimo configurable por la tienda; se permite **$0** para tiendas sin mínimo. Todas las validaciones se realizan nuevamente en la API.

### RN-05. Precio de un pedido
Guardar precio unitario, cantidad, nombre visible y total por concepto como **snapshot histórico**. Modificaciones posteriores al catálogo no alteran pedidos registrados. Usar tipo decimal adecuado para moneda; nunca operaciones monetarias con flotantes binarios.

### RN-06. Aceptación manual
El pedido inicia en **Pendiente de aceptación**. La tienda lo **acepta** o **rechaza**. No asumir que la orden ya se está preparando inmediatamente después de solicitarla.

### RN-07. Tiempo estimado
El tiempo es **orientativo**, no un compromiso exacto. El encargado define el **tiempo promedio de preparación de la tienda**, en minutos. Al aceptar un pedido, la API calcula:

```
TiempoEstimadoMin = TiempoPromedioTiendaMin + (2 * PedidosActivosAnteriores)
```

- `PedidosActivosAnteriores`: cantidad de pedidos **anteriores de la misma tienda** todavía pendientes, aceptados o en preparación, excluyendo pedidos listos, entregados, rechazados o cancelados, así como el propio pedido.
- El cálculo y su fecha de referencia se registran en la aceptación; no se modifica continuamente al cambiar la cola. Esto evita cambios constantes en la estimación mostrada.
- El valor es por **pedido**, no por cada platillo del carrito.
- **Ejemplo:** promedio 10 min; con 3 pedidos anteriores activos, estimación = **16 minutos**.
- **Nota de diseño:** la regla de qué estados cuentan como «activos anteriores» se define aquí como interpretación inicial para materializar el requerimiento; si el negocio requiere excluir pedidos pendientes sin aceptar, ajustar esta única política, no dispersarla en el código.
- No presentar la estimación como garantía, ni introducir inteligencia de colas en el MVP.

### RN-08. Estados del pedido
Flujo principal:

```
Pendiente -> Aceptado -> EnPreparacion -> Listo -> Entregado
     |           |
     v           v
  Rechazado   Cancelado (según autorización)
```

- Registrar fecha, actor y transición.
- No permitir transiciones arbitrarias ni saltos desde cliente.
- `Cancelado` y `NoRecogido` deben contemplarse en el modelo; la política exacta de cancelación y el momento de marcar **No recogido** quedan **pendientes** y no deben bloquear el flujo principal.

### RN-09. Notificación SMS
Enviar al **teléfono verificado** del cliente **cuando el estado pase a Listo**. Invocar el endpoint del SMS Gateway existente desde el backend; no exponer credenciales en Next.js. Registrar resultado, errores y reintentos limitados. El fallo del SMS **no revierte** el cambio a Listo. Evitar SMS duplicados ante reintentos o solicitudes repetidas.

### RN-10. Inicio de sesión y datos personales
- Google simplifica el acceso. La primera vez, registrar identidad mínima (identificador estable de Google, correo, nombre visible y foto si está disponible).
- El usuario puede completar y actualizar su teléfono; **Google no garantiza proporcionar el número**.
- Para solicitar es obligatorio el teléfono verificado. Aplicar caducidad, límite de intentos y frecuencia de envío de códigos OTP.
- No pedir dirección personal ni información adicional sin necesidad operativa.

### RN-11. Pedidos no recogidos / abuso
Mantener el historial y registros de pedidos rechazados, cancelados o no recogidos. Añadir control de solicitudes repetidas, rate limiting y deduplicación. **No exigir pago anticipado, depósito ni penalización automática** en el MVP.

### RN-12. Seguridad y consistencia
No aceptar identificadores de tienda/usuario como fuente de permisos. Aplicar controles de acceso en cada operación. Confirmación de pedidos idempotente para impedir duplicados por doble clic o reintentos de red.

---

## 6. Casos de uso del MVP

| Código | Caso de uso | Actor | Resultado esperado |
|---|---|---|---|
| CU-01 | Consultar tiendas | Visitante/cliente | Ve tiendas activas y sus estados |
| CU-02 | Consultar menú y disponibilidad | Visitante/cliente | Ve catálogo vigente de una tienda |
| CU-03 | Iniciar sesión con Google | Cliente | Cuenta creada/vinculada y sesión válida |
| CU-04 | Completar perfil y verificar teléfono | Cliente | Teléfono validado para avisos |
| CU-05 | Armar carrito | Cliente | Selecciona artículos/cantidades de una tienda |
| CU-06 | Solicitar pedido | Cliente | Pedido persistido en estado Pendiente |
| CU-07 | Consultar mis pedidos y detalle | Cliente | Ve estados, folio, importes y estimación cuando exista |
| CU-08 | Aceptar/rechazar pedido | Encargado/cocina autorizada | Pedido aceptado con estimación o rechazado con motivo |
| CU-09 | Actualizar preparación y entrega | Encargado/cocina | Cambia estados mediante transiciones autorizadas |
| CU-10 | Alta/edición de categorías y platillos | Encargado/cocina | Catálogo mantenible, precio y disponibilidad controlados |
| CU-11 | Configurar tienda | Encargado | Horario, mínimo, promedio, estado/pausa |
| CU-12 | Notificar pedido listo | Sistema | Emite SMS por gateway y registra ejecución |
| CU-13 | Recibir nuevos pedidos sin recargar | Encargado/cocina | Eventos SSE a usuarios autorizados |
| CU-14 | Dar de alta tienda y asignar encargado | Operador | Nueva tienda operativa sin panel SaaS complejo |

---

## 7. Modelo de datos conceptual

Nombres propuestos; no representan aún un esquema físico aprobado.

| Entidad | Campos principales |
|---|---|
| `Users` | `Id`, `GoogleSubject`, `Email`, `DisplayName`, `AvatarUrl`, `PhoneE164`, `PhoneVerifiedAt`, `CreatedAt`, `IsActive` |
| `Stores` | `Id`, `Slug`, `Name`, `Description`, `AddressText`, `TimeZoneId`, `IsActive`, `IsPaused`, `MinimumOrderAmount`, `AveragePreparationMinutes`, `CreatedAt` |
| `StoreMemberships` | `Id`, `StoreId`, `UserId`, `Role`, `IsActive` |
| `StoreBusinessHours` | `Id`, `StoreId`, `DayOfWeek`, `OpeningTime`, `ClosingTime`, `IsClosed` (horarios partidos, si se requieren, mediante varias filas) |
| `Categories` | `Id`, `StoreId`, `Name`, `SortOrder`, `IsActive` |
| `Dishes` | `Id`, `StoreId`, `CategoryId`, `Name`, `Description`, `Price`, `ImageUrl`, `IsActive`, `IsAvailable`, `SortOrder`, `CreatedAt`, `UpdatedAt` |
| `Orders` | `Id`, `StoreId`, `CustomerUserId`, `PublicCode`, `Status`, `Total`, `AveragePreparationSnapshotMin`, `ActiveOrdersAheadSnapshot`, `EstimatedPreparationSnapshotMin`, `AcceptedAt`, `ReadyAt`, `DeliveredAt`, `CreatedAt`, `UpdatedAt` |
| `OrderItems` | `Id`, `OrderId`, `DishId`, `NameSnapshot`, `UnitPriceSnapshot`, `Quantity`, `LineTotal` |
| `OrderStatusHistory` | `Id`, `OrderId`, `PreviousStatus`, `NewStatus`, `ChangedByUserId`, `Reason`, `ChangedAt` |
| `SmsDeliveries` | `Id`, `OrderId`, `TemplateType`, `PhoneMasked`, `ProviderReference`, `Status`, `AttemptCount`, `LastError`, `CreatedAt`, `SentAt` |
| `PhoneVerificationCodes` | `Id`, `UserId`, `PhoneE164`, `CodeHash`, `ExpiresAt`, `Attempts`, `ConsumedAt` |

### Consideraciones de persistencia

- Índices compuestos por `StoreId` en lecturas administrativas y pedidos, además de índices en `Orders(StoreId, Status, CreatedAt)` y `Orders(CustomerUserId, CreatedAt)`.
- Claves foráneas y restricciones de unicidad pertinentes; `Stores.Slug` único y `Users.GoogleSubject` único.
- Guardar fechas en UTC; convertir a hora local para presentar horarios y evaluar la tienda.
- Monto en moneda definida por negocio: **MXN como valor inicial propuesto**, sin necesidad de conversión multimoneda.
- Eliminar un platillo no debe romper el historial: usar desactivación lógica y snapshots en `OrderItems`.
- Las acciones de aceptación y cambio de estado deben ser atómicas e impedir transiciones concurrentes duplicadas. Usar control de concurrencia o condiciones sobre estado en la actualización.
- Aislamiento multi-tienda por `StoreId` validado en **todas** las consultas de negocio y permisos; no basta filtrar desde el frontend.

---

## 8. Diseño inicial de API (propuesta)

Prefijo propuesto: `/api/v1`. Nombres sujetos a normalización del proyecto.

### Público y cuenta

```
GET    /stores
GET    /stores/{slug}
GET    /stores/{storeId}/dishes
GET    /auth/google/start           # o flujo OIDC equivalente
GET    /auth/google/callback        # según estrategia de sesión
GET    /me
PATCH  /me
POST   /me/phone-verifications
POST   /me/phone-verifications/confirm
GET    /me/orders
GET    /me/orders/{orderId}
POST   /orders                     # confirma carrito, con idempotency key
```

### Administración por tienda

```
GET    /admin/stores/{storeId}/orders
POST   /admin/stores/{storeId}/orders/{orderId}/accept
POST   /admin/stores/{storeId}/orders/{orderId}/reject
POST   /admin/stores/{storeId}/orders/{orderId}/start-preparation
POST   /admin/stores/{storeId}/orders/{orderId}/ready
POST   /admin/stores/{storeId}/orders/{orderId}/delivered
GET    /admin/stores/{storeId}/dishes
POST   /admin/stores/{storeId}/dishes
PATCH  /admin/stores/{storeId}/dishes/{dishId}
GET    /admin/stores/{storeId}/categories
POST   /admin/stores/{storeId}/categories
PATCH  /admin/stores/{storeId}/settings
GET    /admin/stores/{storeId}/events     # SSE autenticado
```

**SSE:** entregar eventos ligeros como `order.created`, `order.status-changed`, `dish.availability-changed`; enviar solo identificadores/datos mínimos y recargar detalle desde REST según necesidad. Proteger streams por membresía/usuario y filtrar eventos por tienda. Manejar reconexión, heartbeat y cierre de sesión. Para cambios del pedido del cliente, usar consulta REST y, si aporta valor, stream autenticado por usuario o sondeo ligero.

**Operador de plataforma:** alta de tienda y asignación de responsables mediante operación interna protegida, seed/CLI o endpoints privados. No crear un panel global avanzado en el MVP.

---

## 9. Validaciones y escenarios alternativos

| Escenario | Respuesta del sistema |
|---|---|
| El usuario intenta solicitar sin sesión | Solicitar acceso con Google y conservar carrito |
| No cuenta con teléfono verificado | Pedir captura/verificación antes de confirmar |
| Tienda cerrada o pausada | Deshabilitar confirmación y mostrar motivo |
| Artículo se agotó tras agregar al carrito | Rechazar esa combinación y permitir corregir carrito |
| El mínimo no se alcanza | Mostrar diferencia faltante y bloquear confirmación |
| Cambió el precio antes de solicitar | Recalcular en backend; mostrar cambios y exigir revisión del usuario |
| Doble clic o reintento de POST | Retornar el mismo pedido con clave de idempotencia |
| Tienda rechaza pedido | Reflejar motivo y estado al cliente; no enviar SMS de listo |
| Nuevo pedido con panel abierto | Evento SSE en la tienda correcta y actualización de la bandeja |
| Fallo del SMS al marcar listo | Mantener estado Listo; registrar fallo y reintento controlado |
| Intento de acceso a tienda ajena | Rechazar por autorización, sin filtrar información |
| Cliente no recoge | Conservar trazabilidad; política de cierre como NoRecogido pendiente |

---

## 10. Requisitos no funcionales

- Interfaces accesibles, rápidas y responsivas; formularios con mensajes claros de error, carga y éxito.
- Evitar doble envío, estados inconsistentes y pérdida de carrito por autenticar.
- Validación de entradas tanto en cliente como servidor, con servidor autoritativo.
- Registro estructurado de operaciones relevantes y errores de integración.
- No registrar OTP ni credenciales en texto plano. Proteger datos personales y reducir información sensible en logs.
- Aplicar rate limiting en autenticación, verificación telefónica y creación de órdenes.
- Sanitización y validación de imágenes/archivos si se habilita carga de fotos; en el MVP puede usarse URL controlada o almacenamiento simple configurado.
- No incorporar analítica invasiva ni herramientas pesadas sin justificación.
- Pruebas automatizadas de reglas críticas y separación entre tiendas.

---

## 11. Plan de implementación sugerido para el agente

### Fase 0 — Fundaciones
- Crear estructura de repositorio (frontend Next.js, API ASP.NET Core), configuración por entorno, conexión SQL Server y migraciones.
- Definir entidades, autenticación/autorización, contratos de API y controles por tienda.
- Proveer datos semilla de demostración para al menos dos tiendas que permitan verificar el aislamiento.

**Entrega:** aplicación ejecutable localmente, API documentada y modelo de datos migrable.

### Fase 1 — Tiendas y catálogo
- Landing/listado de tiendas, detalle de tienda, horarios, montos mínimos, pausa y catálogo.
- Panel sencillo para altas/edición/activación/disponibilidad de platillos, con permisos.

**Entrega:** cocina puede gestionar un menú real y el visitante puede navegarlo.

### Fase 2 — Identidad y perfil
- Login Google, alta automática de cuenta, perfil, verificación de teléfono mediante gateway.
- Restaurar el carrito tras autenticación.

**Entrega:** usuario listo para solicitar con identidad y teléfono válidos.

### Fase 3 — Solicitudes y ciclo de pedido
- Carrito multítem, controles de horario/mínimo/disponibilidad, total confiable e idempotencia.
- Pedido pendiente, aceptación/rechazo, cálculo +2 min por pedido activo anterior, estados de preparación y entrega, historial.

**Entrega:** ciclo completo de recogida y pago presencial.

### Fase 4 — Tiempo real y SMS
- SSE directo en ASP.NET Core para panel de tienda, reconexión y alertas visuales/sonoras.
- Integrar endpoint SMS Gateway, disparar al marcar Listo y registrar reintentos/resultado.

**Entrega:** tienda visualiza pedidos sin refrescar y cliente recibe aviso de listo.

### Fase 5 — Calidad y validación de MVP
- Tests unitarios, integración, E2E de tres etapas, concurrencia, seguridad multi-tienda, estados vacíos y errores.
- Refinar UX mobile-first, mensajes y documentación de ejecución/despliegue genérica.

**Entrega:** MVP probado y preparado para piloto.

**Regla para el agente:** completar cada fase de forma funcional y verificable; reportar lo implementado, pruebas ejecutadas, pendientes y decisiones tomadas. No declarar terminada una capacidad apoyada solo en mocks.

---

## 12. Criterios de aceptación principales

1. Un visitante consulta tiendas y platillos sin autenticarse.
2. Un cliente registrado solicita su comida siguiendo las tres etapas funcionales propuestas.
3. Si no ha iniciado sesión, al solicitar se autentica con Google **sin perder el carrito**.
4. Sin teléfono verificado no se confirma la solicitud; completar OTP permite continuar.
5. La API rechaza solicitudes fuera de horario, con tienda pausada, platillos inactivos o total inferior al mínimo.
6. Una solicitud válida queda en **Pendiente**, con folio, conceptos, precios históricos y tienda correctos.
7. Una tienda recibe únicamente sus propios pedidos y los acepta/rechaza desde su panel.
8. Con tiempo promedio de 10 min y tres pedidos anteriores activos en la misma tienda, al aceptar se conserva una estimación de **16 min**.
9. La cocina puede activar/desactivar platillos de forma inmediata y los pedidos los revalidan al confirmar.
10. El panel recibe eventos nuevos por SSE propio de la API, sin SignalR, y puede recuperar la verdad vía REST.
11. Marcar un pedido como **Listo** dispara un único evento de notificación SMS por transición efectiva, con registro de entrega/fallo.
12. El cliente ve estado/historial; ningún usuario o empleado obtiene datos de tiendas o pedidos ajenos por manipular IDs.
13. Reintentos de confirmación con la misma clave no crean pedidos duplicados.
14. La aplicación funciona correctamente en dispositivos móviles y escritorio.

---

## 13. Fuera de alcance del MVP

- Pasarelas de pago, cobros en línea, facturación o reembolsos.
- Envíos a domicilio, repartidores, geolocalización en tiempo real.
- Programación de pedidos para un horario o fecha futura.
- Subscripciones de tiendas, cobro de comisiones y panel financiero de SaaS.
- Programas de lealtad, cupones, reseñas o recomendaciones automáticas.
- Inventario detallado con existencias, insumos y recetas; solo disponibilidad de platillos controlada por cocina.
- App móvil nativa; se entrega experiencia web responsive.
- Predicción avanzada de demanda, algoritmos complejos de cola o ETA exacto.
- Panel global sofisticado de plataforma y reportes avanzados.
- SignalR.

---

## 14. Pendientes no bloqueantes / decisiones explícitas

| Punto | Estado | Política para continuar |
|---|---|---|
| Nombre comercial y branding | Pendiente | Usar nombre provisional neutro y diseño limpio |
| Contrato exacto del SMS Gateway | Pendiente | Integrar mediante interfaz/configuración; no inventar URL ni credenciales |
| Política de cancelación del cliente | Pendiente | Modelar estado; no habilitar cancelación sin regla aprobada |
| Política para marcar pedido NoRecogido | Pendiente | Mantener trazabilidad; no imponer umbral arbitrario |
| Comercialización (piloto, mensualidad, comisión) | Pendiente | No desarrollar monetización en MVP |
| Horarios especiales/feriados | Pendiente | Soportar semana habitual; documentar limitación |
| Imágenes de platillos y almacenamiento | Pendiente | Diseñar campo opcional; configurar solución básica cuando se decida |
| Uso de WebSocket vs SSE | Propuesta | Preferir SSE para eventos del servidor hacia frontend; WebSocket nativo solo si surge necesidad bidireccional |
| Moneda | Propuesta | MXN para piloto local, sin multimoneda |

---

## 15. Instrucción final para el agente de desarrollo

Desarrolla el **MVP de plataforma de pedidos anticipados** con **Next.js/React**, **ASP.NET Core Web API** y **SQL Server**. Sigue las fases y reglas de este documento; prioriza el recorrido **elegir tienda → elegir platillos → solicitar**, pago presencial, Google Login, panel operativo sencillo, cálculo de estimación **promedio de tienda + 2 minutos por pedido anterior activo**, notificación SMS al quedar listo, aislamiento multi-tienda y actualización en tiempo real mediante **SSE o WebSocket nativo directo a la API; jamás SignalR**.

Antes de cada fase, identifica dependencias reales y riesgos. Implementa el alcance acordado, mantén APIs y componentes reutilizables sin sobreingeniería, agrega pruebas significativas y entrega un resumen de evidencias al cerrar cada fase. Los puntos pendientes se documentan sin convertirlos en requisitos definitivos.
