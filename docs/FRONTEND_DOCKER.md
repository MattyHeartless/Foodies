# Frontend en Docker

El frontend usa Next.js standalone sobre Node 24. El build instala desde package-lock.json, compila y copia únicamente el servidor trazado, public y .next/static a la imagen final. Ejecuta como el usuario no root `node` y escucha en 3000 dentro del contenedor. No recibe las credenciales de SQL, Google ni SMS: permanecen en la API.

## Construir y arrancar ambos servicios

Desde la raíz, con Docker Compose 2.30 o posterior:

```powershell
Copy-Item .env.api.example .env.api
# Completar SQL Server, PublicOrigin, AllowedHosts y las integraciones en .env.api.
docker compose build
docker compose run --rm api --migrate
docker compose up -d
```

Si `.env.api` ya existe, editarlo sin sobrescribirlo. Las migraciones son una operación explícita y no se repiten automáticamente en cada inicio. `compose.yaml` incluye `compose.api.yaml`: no es necesario levantar por separado otro proyecto de la API. SQL Server es externo a este Compose.

Direcciones del anfitrión:

- Frontend: `http://127.0.0.1:3003` → web:3000.
- API: `http://127.0.0.1:5089/api/v1/health` → api:8080.

Ambos puertos están publicados solo en loopback. El frontend espera al healthcheck de la API. `FOODIES_WEB_PORT` y `FOODIES_API_PORT`, definidos en el entorno de Compose o en un `.env` privado de la raíz, cambian los puertos externos. No se obtienen del archivo de variables exclusivo de la API. Los valores por defecto evitan colisionar con dev.ps1, que usa 3002 y 5088.

Para producción colocar el proxy HTTPS delante del frontend en 3003 o conectarlo a la misma red Docker. `PublicOrigin` de la API debe ser el dominio HTTPS que verá el navegador, y Google debe tener autorizado `{PublicOrigin}/api/v1/auth/google/callback`. El navegador usa siempre `/api/*` en el mismo origen que Foodies; Next reenvía las solicitudes a la API por la red interna, incluidas cookies, callbacks, imágenes y eventos. Para probar login interactivo por HTTP local, usar temporalmente Development y PublicOrigin HTTP del frontend, con su callback autorizado en Google; el ejemplo y las imágenes usan Production por defecto.

## Solo construir la imagen del frontend

```powershell
docker build -f frontend/Dockerfile -t foodies-web:local .
```

El contexto de build es la raíz del repositorio. Por defecto la imagen reenvía `/api/*` a `http://api:8080`, por lo que un contenedor lanzado manualmente debe compartir red con una API cuyo alias sea `api`.

## Dirección de la API

Los rewrites se fijan al compilar. Si la dirección interna cambia, reconstruir la imagen:

```powershell
docker build -f frontend/Dockerfile --build-arg API_ORIGIN=http://otro-servicio:8080 -t foodies-web:local .
```

Con Compose se usa `FOODIES_API_ORIGIN` como argumento de build. Cambiar solamente una variable `API_ORIGIN` en un contenedor ya compilado no cambia los rewrites. Este argumento contiene una dirección de servicio, no credenciales.

`FOODIES_OUTPUT=standalone` y `LOCAL_CANONICAL_ORIGIN` vacío se fijan solo en el build de Docker. El flujo local de dev.ps1 mantiene su salida normal de Next y su redirección localhost → 127.0.0.1:3002. La imagen Docker no redirige los visitantes a esa dirección de desarrollo.

## Archivos persistentes y estado

El frontend sirve su código y recursos estáticos; las imágenes subidas permanecen en el volumen de la API y se leen mediante `/api/v1/media/*`. Los volúmenes `foodies_media` y `foodies_keys` están definidos en el Compose incluido de la API. Ver [API_DOCKER.md](API_DOCKER.md) y [STORE_COVER.md](STORE_COVER.md).

El healthcheck del frontend comprueba que su servidor responda 200 en `/`. La conexión a SQL la comprueba el healthcheck de la API, no el del frontend.

```powershell
docker compose ps
docker compose logs --tail 100 web
docker compose exec web node container-health.mjs
```

Referencias: [salida standalone de Next.js](https://nextjs.org/docs/app/api-reference/config/next-config-js/output), [Docker para Next.js](https://docs.docker.com/guides/nextjs/).

## Verificación realizada

Imagen `foodies-web:local` construida y ejecutada como UID 1000 junto a `foodies-api:local` en una red Docker de prueba, con SQL Server del anfitrión. Se comprobaron HTML, estáticos de Next, favicon, iconos, PNG público, healthcheck, consulta real de tiendas, ausencia de la redirección local, cookies y validación CSRF a través del proxy, inicio OAuth con callback y cookie de correlación, y descarga de una imagen desde el volumen de la API. El navegador cargó las dos tiendas en 390 px sin desbordamiento. Pasaron las 9 pruebas de movimiento y las 6 de horarios; Compose validó sin errores. La configuración de desarrollo mantiene su redirección original.

La prueba de OAuth comprueba la solicitud inicial con datos de prueba, no el login completo con una cuenta de Google. No se enviaron SMS, no se crearon pedidos ni se modificaron datos comerciales. Los contenedores, red y volúmenes temporales se eliminaron; las imágenes locales permanecen disponibles. No se publicó en un servidor remoto.
