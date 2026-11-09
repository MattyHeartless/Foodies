# Google Login en localhost

No necesitas comprar un dominio, publicar la web ni contratar un tunel para probar Google Login localmente. Google permite URI de retorno HTTP en localhost y direcciones de loopback como 127.0.0.1. Foodies utiliza OAuth de servidor mediante ASP.NET Core, con PKCE y sesion HttpOnly.

## 1. Crear el cliente en Google

1. Abre [Google Cloud Console](https://console.cloud.google.com/) con tu cuenta y selecciona o crea un proyecto para Foodies.
2. En **Google Auth Platform**, configura los datos de la aplicacion en **Branding** (nombre Foodies, correo de soporte/contacto) y su audiencia en **Audience**. Para un piloto con cuentas personales, usa audiencia externa y mantenlo en pruebas. Agrega tu correo en usuarios de prueba cuando corresponda a la configuracion de audiencia.
3. En **Clients**, crea un cliente OAuth de tipo **Web application / Aplicacion web**. No selecciones aplicacion de escritorio ni una API key.
4. Registra esta **URI de redireccion autorizada**, exactamente:

```text
http://127.0.0.1:3002/api/v1/auth/google/callback
```

5. Guarda y descarga el JSON del cliente. Contiene Client ID y Client Secret; guardalo localmente fuera del repositorio.

El flujo actual hace la autenticacion desde el servidor. No utiliza Google Identity Services en JavaScript, por lo que no necesita configurar origenes JavaScript autorizados. Si mas adelante se agrega un boton GIS o One Tap, esa integracion necesitara sus propios ajustes de origen.

No uses `http://127.0.0.1:5088` como retorno: el navegador debe volver por Next.js, en el origen publico del sitio. `localhost` y `127.0.0.1` no son intercambiables para la coincidencia del retorno ni para las cookies. En esta configuracion utiliza siempre **http://127.0.0.1:3002**.

## 2. Importar la configuracion local

Desde la carpeta Foodies:

```powershell
.\configure-google.ps1 -CredentialsPath 'C:\ruta\client_secret_descargado.json'
```

El script valida que sea un cliente web y que el archivo incluya el retorno esperado. Importa unicamente Client ID/Secret y el origen publico al archivo `backend/Foodies.Api/appsettings.Local.json`. Conserva conexion SQL y otras opciones. No imprime los secretos ni los agrega al frontend; el archivo local esta excluido de Git y de publicacion.

Tambien puedes editar ese archivo local directamente, agregando:

```json
"PublicOrigin": "http://127.0.0.1:3002",
"Google": {
  "ClientId": "CLIENT_ID_DE_GOOGLE",
  "ClientSecret": "CLIENT_SECRET_DE_GOOGLE"
}
```

No pegues el JSON ni el secreto en archivos publicos. Si necesitas ayuda con la importacion, indica la ruta al archivo descargado.

## 3. Reiniciar y comprobar

Reinicia la API despues de importar: el proveedor Google se registra al arrancar. Abre Foodies en el origen indicado y selecciona **Iniciar sesion → Continuar con Google**. Si estabas usando una cuenta local de demostracion, cierra primero esa sesion.

Comprueba que vuelva a Foodies, muestre tu nombre y mantenga el carrito. En el primer acceso se crea una cuenta real independiente de las cuentas de prueba. Ser cliente no otorga acceso a cocina: el operador debe asignar una membresia a tu cuenta si administraras una tienda.

La sesion expone `googleConfigured=true` cuando ambos valores estan configurados. Esto solo confirma configuracion local, no sustituye la prueba real con Google.

## Errores frecuentes

- **redirect_uri_mismatch**: comparar esquema, host, puerto y ruta con la URI registrada en Google. Importar nuevamente si se cambiaron credenciales o retorno.
- **Google sigue deshabilitado**: revisar ambos campos y reiniciar la API. Las variables de entorno `Google__ClientId` / `Google__ClientSecret` tienen prioridad sobre el archivo local.
- **Cuenta no permitida**: revisar audiencia/usuarios de prueba del proyecto, o restricciones de Google Workspace.
- **Vuelve con error de autenticacion**: iniciar de nuevo usando siempre el mismo host; no alternar localhost y 127.0.0.1 ni abrir el retorno manualmente.

Para produccion se configurara un origen HTTPS y su retorno correspondiente. No se necesita resolver ese dominio para terminar y probar la configuracion local.

Fuentes oficiales: [OAuth para aplicaciones web](https://developers.google.com/identity/protocols/oauth2/web-server), [gestion de clientes OAuth](https://support.google.com/cloud/answer/15549257).
