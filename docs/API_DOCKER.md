# API en Docker

El Dockerfile compila con el SDK .NET 10 y copia solamente el resultado de publicación a ASP.NET Runtime 10. Ejecuta como el usuario `app`, escucha HTTP en 8080 dentro del contenedor y usa Production por defecto. No incluye credenciales, datos locales ni imágenes subidas. El frontend y SQL Server se despliegan aparte.

Desde la raíz del repositorio:

```powershell
Copy-Item .env.api.example .env.api
# Completar .env.api antes de arrancar.
docker compose -f compose.api.yaml up -d --build
```

La API queda en `http://127.0.0.1:5089/api/v1/health`. El puerto del host se cambia con `FOODIES_API_PORT`; 5089 evita colisionar con la API local en 5088. El puerto interno permanece 8080. El archivo `.env.api` está excluido de Git y del contexto de build. Para construir solamente la imagen:

Usar Docker Compose 2.30 o posterior. `format: raw` conserva los valores del archivo de entorno sin expandir `$` ni interpretar comillas: escribir los valores directamente, sin comillas envolventes. [Referencia de Docker](https://docs.docker.com/reference/compose-file/services/#format).

```powershell
docker build -f backend/Foodies.Api/Dockerfile -t foodies-api:local .
```

## Variables y proxy

Definir `ConnectionStrings__Foodies` y `PublicOrigin`. Production requiere un origen HTTPS público sin ruta: será el dominio de Foodies que envíe `/api/*` a la API. Configurar `AllowedHosts` con ese dominio, `localhost` y `127.0.0.1` (necesarios para el healthcheck). Google usa `Google__ClientId` y `Google__ClientSecret`; su callback autorizado es `{PublicOrigin}/api/v1/auth/google/callback`. Las variables de SMS están en el ejemplo y solo deben habilitarse cuando el gateway y la cola estén listos.

`localhost` en la conexión SQL del contenedor apunta a la propia API. Para SQL Server en el equipo anfitrión usar `host.docker.internal,1433`; Compose agrega el alias host-gateway para Linux. Para SQL en otro servicio de Compose usar el nombre de ese servicio y su puerto interno. En producción configurar el cifrado y la cuenta SQL del servidor de destino.

Compose publica el puerto únicamente en loopback; colocar el proxy HTTPS delante o conectar el frontend y la API por una red Docker compartida. El frontend podrá usar `API_ORIGIN=http://api:8080`. El Dockerfile de la API no configura TLS ni publica el sitio en Internet. Para probar HTTP local de manera interactiva se puede configurar Development y un PublicOrigin HTTP local; no habilitar cuentas de desarrollo en producción.

## Persistencia

- `foodies_media` → `/data/foodies/media`: imágenes de las tiendas. `Media__StoragePath` apunta allí.
- `foodies_keys` → `/data/foodies/keys`: claves Data Protection que protegen cookies, CSRF y OAuth. `DataProtection__KeyPath` apunta allí y usa el nombre de aplicación estable Foodies.

La imagen prepara ambos directorios con propietario `app`; un volumen nuevo hereda esos permisos. Los volúmenes se conservan al recrear contenedores y con `docker compose down`. No usar `down -v` si se quieren conservar. Si ya existen con otros propietarios, ajustar sus permisos para el usuario del contenedor antes de arrancar. Respaldar las imágenes, las claves y la base de datos. El fragmento anterior `deploy/compose.media.example.yaml` es alternativo para integrar imágenes como volumen externo; `compose.api.yaml` ya declara ambos volúmenes y no necesita ese fragmento.

## Migraciones y administrador inicial

No se aplican migraciones al arrancar cada contenedor. Ejecutarlas como operación explícita antes del primer inicio o al actualizar:

```powershell
docker compose -f compose.api.yaml build api
docker compose -f compose.api.yaml run --rm api --migrate
docker compose -f compose.api.yaml up -d api
```

La operación solo acepta bases cuyo nombre comience por Foodies. En Production no carga tiendas ni usuarios de prueba.

Para el administrador inicial, añadir temporalmente `FOODIES_PLATFORM_PASSWORD` al archivo privado de entorno y ejecutar:

```powershell
docker compose -f compose.api.yaml run --rm api --operator platform-admin admin@tudominio.com "Administrador"
```

Después retirar esa variable del archivo. El acceso de plataforma es independiente de Google. Ver [PLATFORM_API.md](PLATFORM_API.md).

## Estado y registros

```powershell
docker compose -f compose.api.yaml ps
docker compose -f compose.api.yaml logs --tail 100 api
docker compose -f compose.api.yaml exec api dotnet Foodies.Api.dll --healthcheck
```

El healthcheck devuelve éxito solo si la API responde y SQL Server es accesible. No envía SMS ni modifica pedidos. Si cambia el contenido de `.env.api`, recrear el contenedor para aplicar la configuración.

## Verificación realizada

Imagen construida y arrancada en Linux amd64 con Docker Desktop y SQL Server del anfitrión. Se confirmó UID 1654, ausencia de appsettings.Local.json en la imagen, escritura de ambos volúmenes y healthcheck con SQL disponible. Tras eliminar y recrear el contenedor con los mismos volúmenes, se conservó un archivo de prueba y siguió siendo válido un token CSRF emitido antes de la recreación. Se eliminaron los contenedores y volúmenes temporales de la prueba; la imagen foodies-api:local queda disponible. Las 66 pruebas del backend pasaron. No se envió SMS, no se aplicaron migraciones a la base local ni se publicó en un servidor remoto.
