# Plan de despliegue: API y frontend

Este plan usa un servidor con Docker Compose 2.30 o posterior, SQL Server accesible y un dominio HTTPS, por ejemplo `https://foodies.tudominio.com`. El navegador usa un único dominio: frontend y `/api/*`. Las imágenes se compilan desde el repositorio; no se requiere un registry de contenedores.

## 1. Publicar el commit y preparar el servidor

En el equipo de desarrollo, después del commit:

```bash
git push origin main
```

En el servidor, clonar el repositorio (o actualizar una copia existente con `git pull --ff-only`):

```bash
git clone https://github.com/MattyHeartless/Foodies.git
cd Foodies
cp .env.example .env
cp .env.api.example .env.api
```

Copiar las plantillas solo la primera vez, para no sobrescribir configuraciones existentes. Los comandos del servidor asumen una shell Linux. Ninguno de los archivos con valores reales se sube a Git.

## 2. Variables de Compose y frontend

Archivo `.env` en la raíz:

```dotenv
FOODIES_WEB_PORT=3003
FOODIES_API_PORT=5089
FOODIES_API_ORIGIN=http://api:8080
```

Los puertos son del anfitrión y se publican únicamente en 127.0.0.1. Dentro de Docker, web escucha en 3000 y api en 8080. `FOODIES_API_ORIGIN` se pasa al build del frontend: no es una variable dinámica de un contenedor ya compilado. Si cambia, reconstruir web. `PORT=3000`, `HOSTNAME=0.0.0.0`, `NODE_ENV=production`, `FOODIES_OUTPUT=standalone` y la desactivación de la redirección local se configuran en el Dockerfile y no necesitan credenciales ni valores adicionales.

## 3. Variables de la API

Archivo `.env.api`, sin comillas envolventes (Compose usa formato raw):

```dotenv
ASPNETCORE_ENVIRONMENT=Production
PublicOrigin=https://foodies.tudominio.com
AllowedHosts=foodies.tudominio.com;api;localhost;127.0.0.1
ConnectionStrings__Foodies=Server=SERVIDOR_SQL,1433;Database=Foodies;User ID=USUARIO_SQL;Password=REEMPLAZAR;Encrypt=True;TrustServerCertificate=False;
Google__ClientId=REEMPLAZAR
Google__ClientSecret=REEMPLAZAR
Sms__Enabled=false
Sms__Endpoint=https://api.sms-gate.app/3rdparty/v1/messages
Sms__Username=REEMPLAZAR
Sms__Password=REEMPLAZAR
DevelopmentAccess__Enabled=false
PhoneVerification__AllowUnverifiedOrders=false
Media__StoragePath=/data/foodies/media
DataProtection__KeyPath=/data/foodies/keys
```

Usar la conexión y la configuración TLS del SQL Server de destino. Para SQL Server en el anfitrión de Docker, el servidor es `host.docker.internal,1433`; `localhost` apunta a la propia API. Si SQL está en otro contenedor de la misma red, usar su nombre de servicio. Las migraciones solo aceptan nombres de base que comiencen por Foodies. No usar la base contribuciones.

`PublicOrigin` es el dominio público del frontend, sin ruta ni barra final; no es `http://api:8080`. `AllowedHosts` conserva api y los hosts de loopback para el proxy interno y el healthcheck. Google, SQL y SMS se configuran solamente en la API. Las credenciales reales existentes se trasladan por un canal privado, no desde el repositorio público.

## 4. Datos existentes y volúmenes

Si se quieren conservar las tiendas, cuentas y pedidos del entorno actual, restaurar un respaldo de la base Foodies en SQL Server de destino antes de ejecutar migraciones. En una base nueva, Production crea el esquema sin cargar datos de demostración.

Los volúmenes `foodies_media` y `foodies_keys` los crea Compose en el primer arranque, con los directorios preparados para el usuario app de la API. El primero conserva imágenes; el segundo, claves de cookies, CSRF y OAuth. Si se migran imágenes locales, copiar el contenido de `backend/Foodies.Api/App_Data/media` a `/data/foodies/media` en el volumen, conservando `stores/{id}/{archivo}` y los permisos de escritura para UID 1654. Las rutas SQL y los archivos deben corresponder al mismo respaldo de tiendas. El frontend no guarda las imágenes subidas en su propia imagen Docker.

Conservar ambos volúmenes en actualizaciones, respaldarlos junto a SQL y no ejecutar `docker compose down -v` si se quieren mantener los datos. No es necesario aplicar además el antiguo fragmento compose.media.example.yaml: el Compose conjunto ya declara los volúmenes.

## 5. Construcción, migraciones y arranque

```bash
docker compose config --quiet
docker compose build
docker compose run --rm api --migrate
docker compose up -d
docker compose ps
```

Web espera a que la API esté saludable; el healthcheck de API comprueba SQL Server. El frontend se comprueba por HTTP en `/`. Para diagnóstico:

```bash
docker compose logs --tail 100 api web
curl --fail http://127.0.0.1:5089/api/v1/health
curl --fail http://127.0.0.1:3003/api/v1/health
```

Mantener inicialmente `Sms__Enabled=false` mientras se revisan datos restaurados y la cola de notificaciones, pues los avisos pendientes se procesan al habilitar el gateway.

## 6. Dominio, HTTPS y Google

Apuntar el dominio al servidor y configurar el proxy HTTPS existente hacia `127.0.0.1:3003` si el proxy corre en el anfitrión. Si el proxy está en Docker, compartir red con Foodies y usar `web:3000`. No exponer directamente los puertos internos ni enviar el tráfico de Google a api:8080 desde el navegador.

El proxy debe admitir solicitudes de al menos 6 MB para las imágenes de hasta 5 MB más multipart, y conservar cookies y redirecciones. Para eventos de cocina, no almacenar en búfer las respuestas `text/event-stream`; por ejemplo, en Nginx configurar el límite de cuerpo y respetar `X-Accel-Buffering: no` que envía la API. Foodies funciona por HTTPS en Production, incluidas las cookies Secure.

En el cliente OAuth web de Google agregar exactamente:

- Origen autorizado: `https://foodies.tudominio.com`.
- Redirección autorizada: `https://foodies.tudominio.com/api/v1/auth/google/callback`.

El JSON descargado no cambia automáticamente por actualizar el repositorio. Configurar en `.env.api` el Client ID y secreto del cliente OAuth correcto. Verificar que la audiencia/publicación de Google permita las cuentas que usarán el piloto. Referencia: [OAuth para aplicaciones web de Google](https://developers.google.com/identity/protocols/oauth2/web-server).

## 7. Primer administrador y activación

Si no se restauró una cuenta de plataforma, provisionar una. En una shell del servidor:

```bash
read -s -p 'Contraseña del administrador: ' FOODIES_PLATFORM_PASSWORD
echo
export FOODIES_PLATFORM_PASSWORD
docker compose run --rm -e FOODIES_PLATFORM_PASSWORD api --operator platform-admin admin@tudominio.com "Administrador"
unset FOODIES_PLATFORM_PASSWORD
```

No poner el valor de esa contraseña en la línea de comandos ni en Git. La cuenta de plataforma es independiente de Google; las cuentas de encargado/cocina se vinculan desde Responsables después del primer login Google de cada persona.

Validar inicio de sesión Google, alta/asignación de tienda, carga de portada, teléfono/PIN y flujo de pedido desde móvil. Configurar el celular de avisos por tienda. Cuando el gateway y la cola estén listos, cambiar `Sms__Enabled=true` y recrear API:

```bash
docker compose up -d --force-recreate api
```

Probar un pedido nuevo (aviso a cocina), listo (aviso al cliente) y una cancelación antes de Listo (aviso al cliente). Verificar entrega real al celular; los textos de pedidos tienen exactamente 180 caracteres.

## Actualizaciones

Antes de actualizar, respaldar SQL y los dos volúmenes y revisar las migraciones del commit. Después de traer el nuevo commit, reconstruir, aplicar migraciones una vez y recrear los servicios. Cambios de variables de API requieren recreación del contenedor; cambios de API_ORIGIN requieren además recompilar web. Una actualización con migraciones no se revierte únicamente volviendo a la imagen anterior: coordinar cualquier restauración con el respaldo SQL.
