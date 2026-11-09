# Administración de plataforma

Contrato de la vista `/plataforma`. Fuente de verdad: `backend/Foodies.Api/openapi.json`; pruebas de integración contra Foodies_Tests. Sesión de administración independiente de clientes y membresías de tiendas, con cookie HttpOnly `foodies.platform`, expiración de dos horas, CSRF y comprobación de acceso vigente en cada petición. No hay registro público de administradores. El primer acceso se provisiona mediante una operación local y contraseña por variable de entorno.

Todas las rutas siguientes usan `/api/v1/platform`. Las mutaciones requieren `X-CSRF-TOKEN`, obtenido de `/session` antes del login y renovado después del login/logout. Ninguna respuesta incluye hashes o contraseñas.

| Método y ruta | Resultado |
| --- | --- |
| GET /session | 200 `{csrfToken, administrator: null o {id,displayName,email}}` |
| POST /login | `{email,password}` → 204; 401 credenciales inválidas/bloqueadas; 429 límite de intentos |
| POST /logout | 204; requiere sesión de plataforma |
| POST /password | `{currentPassword,newPassword}` → 204, invalida sesiones anteriores; contraseña nueva de 12–128 caracteres |
| GET /stores | 200 listado de todas las tiendas, configuración, horarios y miembros activos; solo administradores |
| POST /stores | `{slug, settings: SettingsInput}` → 201 tienda; 409 slug ocupado; 400 campos inválidos |
| PATCH /stores/{id} | `SettingsInput` → 200 tienda actualizada; slug inmutable; 404 desconocida |
| PUT /stores/{id}/members | `{email,role: Manager o Kitchen}` → 200 membresía; usuario de Google registrado y activo; 404 si no existe |
| DELETE /stores/{id}/members/{membershipId} | 204 desactiva membresía; 404 si no pertenece a esa tienda |

`SettingsInput`: nombre, descripción, dirección, zona horaria, visibilidad, pausa, mínimo en MXN, preparación en minutos y lista de horarios. Mismas reglas que el panel de encargados. No se publican tiendas activas sin dirección y horarios de apertura. El formulario crea borradores ocultos y pausados; no hay borrado de tiendas ni pedidos. Asignar un correo no crea ni invita usuarios automáticamente. Un encargado o cliente carece de acceso a estas rutas, incluso teniendo su propia sesión activa.
