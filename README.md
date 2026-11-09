# Foodies · pedidos anticipados

Implementación local del MVP de [la especificación](SPECIFICATION.md): Next.js 16 / React, ASP.NET Core 10, EF Core 10 y SQL Server. Nombre provisional **Foodies**, moneda piloto **MXN**. Un monolito de negocio y una sola aplicación web; SSE nativo, sin SignalR.

## Ejecutar en Windows

Requisitos: .NET SDK 10, Node.js 20.9+ y SQL Server accesible.

La conexión local ya está guardada en `backend/Foodies.Api/appsettings.Local.json`, excluido mediante `.gitignore` y de la publicación. En otra máquina, copiar `appsettings.Local.example.json` a `appsettings.Local.json` y reemplazar la contraseña, o establecer `ConnectionStrings__Foodies` en el entorno. Nunca guardar credenciales en el frontend.

```powershell
.\dev.ps1 setup    # restaurar dependencias, crear/aplicar migraciones y seed Development
.\dev.ps1 start    # compilar e iniciar ambos procesos ocultos
.\dev.ps1 stop
```

Abrir **http://127.0.0.1:3002**. Usar consistentemente este origen para cookies y OAuth. API directa: `http://127.0.0.1:5088`. Logs locales: `.runtime/`.

`start` usa la compilación de producción de Next.js y la API en entorno Development. Para edición con recarga, iniciar en dos terminales:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project backend/Foodies.Api --urls http://127.0.0.1:5088
```

```powershell
cd frontend
npm run dev -- --port 3002
```

En entornos que bloquean la creación de subprocesos, `next dev` puede fallar con `spawn EPERM`; `dev.ps1 start` usa `next start`.

## Bases y datos de ejemplo

- **Foodies**: aplicación. Migraciones versionadas en `backend/Foodies.Api/Migrations`.
- **Foodies_Tests**: pruebas de integración reales, aisladas de la base de la aplicación. Cada prueba crea tiendas propias con identificadores únicos; los datos de prueba se conservan para inspección.
- La base `contribuciones` no se modifica. El comando `--migrate` solo acepta bases cuyo nombre empiece con `Foodies`.
- El seed de Development crea La Cocina de Barrio y Verde & Grano, con menús independientes y horario de ejemplo de 08:00 a 20:00, zona America/Mexico_City.
- No se crean datos de demostración fuera de Development.

Con `DevelopmentAccess:Enabled=true`, el diálogo de inicio permite elegir Cliente, Encargada Barrio, Cocina Barrio o Encargada Verde. Son cuentas locales identificadas como prueba. No tienen teléfonos verificados ni sustituyen Google. El acceso de desarrollo no se registra en otros entornos.

### Guardado temporal de teléfono sin SMS

Por solicitud del propietario, la configuración local habilita `PhoneVerification:AllowUnverifiedOrders=true`. Mientras `Sms:Enabled=false`, el perfil permite **Guardar número** y el cliente puede solicitar con ese teléfono, que permanece **sin verificar**. Se valida formato internacional (por ejemplo `+525512345678`). El carrito regresa al perfil únicamente si falta el número.

`PATCH /api/v1/me/phone` guarda el número propio con sesión y CSRF. Cambiarlo elimina su verificación anterior e invalida códigos pendientes. No se encolan avisos SMS a números sin verificar; el cliente consulta el seguimiento en la web.

Al configurar y habilitar `Sms:Enabled=true`, vuelve a exigirse el PIN incluso si el permiso temporal sigue activo. También puede restablecerse esa exigencia con `PhoneVerification:AllowUnverifiedOrders=false`. No depende de que el gateway responda o esté bien configurado: una falla de SMS nunca activa el modo temporal por sí sola.

## Funcionalidad

- Tiendas y menús públicos, disponibilidad y horarios; diseño adaptable a móvil/escritorio.
- Carrito persistido localmente, de una sola tienda, confirmación para reemplazarlo, cantidades y total; el backend recalcula y valida todo. Los cambios de precio exigen revisión.
- Cookies HttpOnly, protección CSRF, Google OAuth con PKCE preparado; perfiles, flujo OTP y teléfono verificado obligatorio salvo el modo temporal explícito descrito arriba.
- Pedidos Pendiente → Aceptado → EnPreparacion → Listo → Entregado; rechazo de pendientes con motivo, historial y snapshots monetarios.
- Estimación fija al aceptar: promedio de la tienda + 2 × pedidos anteriores pendientes/aceptados/en preparación de esa misma tienda.
- Idempotencia por cliente, comparación de payload, bloqueos transaccionales SQL Server y rowversion para concurrencia.
- Panel único de encargado/cocina, acciones por membresía; catálogo/categorías, disponibilidad, configuración de horario/mínimo/promedio/pausa para encargado.
- SSE autenticado con heartbeat/reconexión y respaldo REST cada 20 segundos; alerta sonora voluntaria. Seguimiento cliente mediante REST cada 10 segundos.
- Outbox SQL para SMS al marcar Listo: un registro por pedido/tipo, envío en segundo plano, hasta 3 intentos con espera. No revierte Listo si el proveedor falla.

## Google y SMS: configurar al final

Para Google Login local no necesitas comprar un dominio. Sigue [la guia de Google en localhost](docs/GOOGLE_LOGIN.md): el origen actual es `http://127.0.0.1:3002` y el retorno es `http://127.0.0.1:3002/api/v1/auth/google/callback`. El script `configure-google.ps1` importa el JSON descargado desde Google Cloud sin imprimir el secreto.

Ambas integraciones permanecen deshabilitadas hasta recibir los datos del propietario. No se han enviado SMS ni se ha probado una cuenta real de Google.

**Google**: definir `Google:ClientId`, `Google:ClientSecret` y `PublicOrigin`. Registrar en Google la URI `${PublicOrigin}/api/v1/auth/google/callback`. En local: `http://127.0.0.1:3002/api/v1/auth/google/callback`. El carrito permanece durante el acceso. El backend usa el origen configurado para construir el callback detrás del proxy Next.js. Nunca almacena los tokens OAuth en el navegador.

**SMS**: integrado con SMSGate en `https://api.sms-gate.app/3rdparty/v1/messages`. Configura `Sms:Username`, `Sms:Password` y `Sms:Enabled=true` solo en configuración privada del backend. Usa Basic, JSON UTF-8 `{id,textMessage:{text},phoneNumbers:[numero],ttl:300}` y conserva el ID estable en reintentos. El adaptador exige textos de **exactamente 180 caracteres**, ni menos ni más. La plantilla de PIN conserva el texto y los espacios de la prueba de PowerShell que SMSGate reportó como entregada (180 caracteres, 183 bytes UTF-8); el PIN de seis dígitos sigue siendo dinámico. La entrega desde el flujo completo debe comprobarse en el celular. El ID de respuesta confirma recepción por el gateway; `Pending` no acredita entrega al teléfono. La outbox registra `Submitted`, no entrega confirmada. No se incorporaron webhooks de entrega.

Al habilitar SMS se exige verificar el teléfono antes de pedir, incluidos los números guardados durante el modo temporal. PIN aleatorio de 6 dígitos, validez 5 minutos, 5 intentos y reenvío limitado. No se devuelve ni registra el PIN. Contrato del proveedor usado: `contracts/smsgate.openapi.json`.

Prueba local sin SMS reales: ejecutar `postman simulation run postman/simulations/smsgate.sim.yaml --port 4605 --no-history`, establecer `$env:FOODIES_SMS_SIMULATION='http://127.0.0.1:4605'` y ejecutar `dotnet test backend/Foodies.Tests -c Release --filter FullyQualifiedName~SmsGatewayTests`. Usa credenciales ficticias y verifica envío por HTTP real hacia el simulador; no demuestra entrega por el proveedor. Las pruebas SQL requieren SQL Server en localhost:1433.

La garantía local es un solo evento/outbox por transición efectiva. Para impedir duplicados físicos ante timeout o caída después del envío, el gateway debe respetar la clave de idempotencia; de lo contrario no es posible prometer exactamente un SMS entre dos sistemas. Un mensaje `Failed` conserva sus intentos para inspección; no hay reenvío ilimitado. Los pendientes se procesan cuando se habilita el gateway: revisar esa cola antes de habilitarlo.

OTP: cinco minutos de validez, hasta cinco intentos, un solo uso, hash con salt, un envío por minuto y cinco por hora/usuario. Con verificación exigida, cambiar teléfono solo se hace efectivo al verificar el nuevo número; en el modo temporal se permite guardarlo sin verificar. La cuenta de prueba no imprime ni devuelve OTPs.

## Administración de plataforma

Abre `http://127.0.0.1:3002/plataforma`. Es un acceso con correo y contraseña independiente del login de Google y del panel de encargados (`/admin`). El primer acceso local está en `.runtime/platform-access.json`, excluido del repositorio. Puedes cambiar la contraseña desde **Tu acceso**.

El panel permite crear tiendas, configurar datos y horarios, publicar u ocultar el catálogo, pausar nuevos pedidos y asignar o retirar responsables. Las altas empiezan ocultas y pausadas. Para asignar a alguien, primero debe iniciar sesión con Google en Foodies; después indica ese mismo correo y el rol Encargado o Cocina.

Para provisionar o recuperar un administrador desde el servidor, establece `FOODIES_PLATFORM_PASSWORD` mediante un secreto del entorno y ejecuta:

```powershell
dotnet run --project backend/Foodies.Api -c Release -- --operator platform-admin admin@tu-dominio.com "Administrador"
```

La contraseña debe tener de 12 a 128 caracteres. El comando crea o actualiza la cuenta y revoca sus sesiones anteriores. No existe registro público de administradores. Aplica las migraciones antes de provisionar. En local usa el entorno `Development`; en producción configura `PublicOrigin` con HTTPS. Detalles en [PLATFORM_API.md](docs/PLATFORM_API.md).

### Operación por consola

Con conexión de servidor y permisos administrativos del operador:

```powershell
dotnet run --project backend/Foodies.Api -c Release -- --operator create-store mi-tienda "Mi tienda"
dotnet run --project backend/Foodies.Api -c Release -- --operator assign mi-tienda responsable@ejemplo.com Manager
dotnet run --project backend/Foodies.Api -c Release -- --operator assign mi-tienda cocina@ejemplo.com Kitchen
```

El usuario debe existir previamente mediante Google. Las tiendas creadas por consola se crean pausadas y sin horario; el encargado las configura y habilita en `/admin/configuracion`. Esta alternativa requiere acceso a la máquina/configuración de operación. Desde la web, asignar roles requiere una sesión de administración de plataforma.

## Contrato y pruebas

[OpenAPI 3.1](backend/Foodies.Api/openapi.json), también disponible en `/api/v1/openapi.json`. Fuente editable: `contracts/build.mjs`; regenerar con `node contracts/build.mjs`. Es importable en Postman. Los DTO PATCH reciben todos los campos editables; no son JSON Patch. Consultar sesión antes de mutaciones y enviar `X-CSRF-TOKEN`; la cookie se maneja automáticamente. Confirmaciones también requieren `Idempotency-Key`.

```powershell
.\dev.ps1 test
.\dev.ps1 build
node contracts/smoke.mjs  # requiere ambos procesos iniciados
```

La suite usa SQL Server real con la misma conexión cambiando únicamente el nombre a `Foodies_Tests`; falla si la conexión no está configurada. No reemplaza SQL Server por memoria o SQLite. El test verifica el nombre de la base antes de migrar. Reporte TRX: `backend/Foodies.Tests/TestResults/foodies.trx`.

Para agregar migraciones, instalar `dotnet-ef` 10.x y ejecutar `dotnet ef migrations add Nombre --project backend/Foodies.Api`. Para aplicar: `.\dev.ps1 migrate`. No se utiliza `EnsureCreated`.

## Límites del piloto

- Cocina o encargado pueden cancelar pedidos Pendiente, Aceptado o EnPreparacion, con motivo obligatorio y SMS de 180 caracteres al cliente verificado. Listo y Entregado no se pueden cancelar. NoRecogido sigue modelado sin acción operativa. No hay pagos en línea, entrega a domicilio, programación futura, monetización ni inventario detallado.
- Semana habitual y horarios partidos/nocturnos. Feriados mediante pausa manual.
- Una instancia de API para SSE; los eventos en memoria no tienen replay. Una reconexión recupera REST, que es la fuente de verdad. Varias réplicas necesitarían distribución de eventos y coordinación del rate limiting.
- Historial visible: últimos 100 pedidos del cliente y 150 de tienda. No hay paginación de archivo en este piloto.
- Imágenes demostrativas por URL HTTPS y tipografía de Google Fonts; no hay subida de archivos. Sustituir por recursos del establecimiento en el piloto real.
- Verificación visual/E2E con navegador pendiente: el acceso a Chrome fue rechazado por la revisión automática y el navegador integrado no estaba disponible. El build, TypeScript y las rutas HTTP sí están comprobados.
- La CLI Postman está bloqueada por una política local de Private Cloud cuyo manifiesto no puede leer. Se conservaron contrato OpenAPI y pruebas ejecutables locales, sin publicar datos a servicios externos.

Antes de alojar: configurar un origen HTTPS y AllowedHosts del dominio, cuenta SQL de mínimos privilegios, secretos por entorno, persistencia y protección de las claves Data Protection, backups y retención de datos personales. `PublicOrigin` exige HTTPS fuera de Development; el backend usa ese origen confiable para OAuth y cookies Secure incluso detrás del proxy. La configuración de hosting queda a decisión del propietario.

Ver [análisis y evidencia de implementación](docs/IMPLEMENTACION.md).

API en Docker: [Dockerfile](backend/Foodies.Api/Dockerfile), [Compose](compose.api.yaml), [variables de ejemplo](.env.api.example) y [guía de arranque, migraciones y persistencia](docs/API_DOCKER.md). Construir desde la raíz del repositorio con `docker build -f backend/Foodies.Api/Dockerfile -t foodies-api:local .`.

Frontend en Docker: [Dockerfile](frontend/Dockerfile) y [guía de frontend y arranque conjunto](docs/FRONTEND_DOCKER.md). Con `.env.api` configurado, `docker compose up -d --build` levanta ambos servicios desde [compose.yaml](compose.yaml): frontend en 3003 y API en 5089 del anfitrión. La dirección interna de la API se fija al compilar el frontend; por defecto es `http://api:8080`.

Publicación: [plan de despliegue y variables necesarias](docs/DEPLOYMENT.md). Las plantillas son [.env.example](.env.example) para Compose/frontend y [.env.api.example](.env.api.example) para la API.

Portadas: se pueden subir JPG, PNG o WebP (hasta 5 MB) desde Configuración o usar una URL HTTPS. Los archivos viven en `Media:StoragePath`, por defecto `backend/Foodies.Api/App_Data/media`. Para producción configurar `Media__StoragePath` y montar un volumen persistente; ver [portadas y almacenamiento](docs/STORE_COVER.md) y el [fragmento Compose](deploy/compose.media.example.yaml).

Las transiciones de la interfaz y sus pruebas están documentadas en [movimiento de Foodies](docs/MOTION.md). Ejecutar `npm run test:motion` dentro de `frontend` para validar los componentes animados.
