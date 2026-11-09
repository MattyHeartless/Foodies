import { readFileSync, writeFileSync } from 'node:fs';
const path = 'backend/Foodies.Api/openapi.json';
const spec = JSON.parse(readFileSync(path, 'utf8'));
const ref = name => ({ $ref: `#/components/schemas/${name}` });
const object = (properties, required = Object.keys(properties)) => ({ type: 'object', properties, required });
const string = { type: 'string' };
const uuid = { type: 'string', format: 'uuid' };
const email = { type: 'string', format: 'email', maxLength: 254 };
const password = { type: 'string', format: 'password', writeOnly: true, maxLength: 128 };
Object.assign(spec.components.schemas, {
  PlatformAdministrator: object({ id: uuid, displayName: string, email }),
  PlatformSession: object({ csrfToken: string, administrator: { anyOf: [ref('PlatformAdministrator'), { type: 'null' }] } }),
  PlatformLoginInput: object({ email, password }),
  PlatformPasswordInput: object({ currentPassword: password, newPassword: { ...password, minLength: 12 } }),
  PlatformStoreInput: object({ slug: { type: 'string', maxLength: 80, pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' }, settings: ref('SettingsInput') }),
  PlatformMemberInput: object({ email, role: { type: 'string', enum: ['Manager','Kitchen'] } }),
  PlatformMember: object({ id: uuid, storeId: uuid, userId: uuid, role: { type: 'string', enum: ['Manager','Kitchen'] }, displayName: string, email, userActive: { type: 'boolean' } }, ['id','storeId','userId','role','displayName','email']),
  PlatformStore: object({ store: ref('Store'), members: { type: 'array', items: ref('PlatformMember') } }),
});
spec.components.securitySchemes.PlatformCookie = { type: 'apiKey', in: 'cookie', name: 'foodies.platform', description: 'Sesión de plataforma independiente de foodies.session. HttpOnly, SameSite=Strict, dos horas.' };
const response = (description, schema) => ({ description, ...(schema ? { content: { 'application/json': { schema } } } : {}) });
const definitions = [
  ['get','/platform/session','Sesión de administración y CSRF',null,ref('PlatformSession'),200,false],
  ['post','/platform/login','Iniciar sesión de plataforma','PlatformLoginInput',null,204,false],
  ['post','/platform/logout','Cerrar sesión de plataforma',null,null,204,true],
  ['post','/platform/password','Cambiar contraseña y revocar otras sesiones','PlatformPasswordInput',null,204,true],
  ['get','/platform/stores','Listar tiendas con configuración y responsables',null,{ type:'array',items:ref('PlatformStore') },200,true],
  ['post','/platform/stores','Crear tienda','PlatformStoreInput',ref('Store'),201,true],
  ['patch','/platform/stores/{id}','Configurar tienda','SettingsInput',ref('Store'),200,true],
  ['put','/platform/stores/{id}/members','Asignar o actualizar responsable','PlatformMemberInput',ref('PlatformMember'),200,true],
  ['delete','/platform/stores/{id}/members/{memberId}','Retirar acceso a una tienda',null,null,204,true],
];
for (const [method, url, summary, input, output, status, secured] of definitions) {
  const mutation = method !== 'get';
  const operation = {
    summary, operationId: `${method}_${url.replace(/[^a-zA-Z0-9]+/g,'_')}`, tags: ['Plataforma'],
    description: 'Acceso exclusivo de administración de plataforma. No admite la cookie de clientes ni membresías Manager/Kitchen. Las mutaciones requieren CSRF de /platform/session, renovado al cambiar de identidad. Altas y cambios se validan como SettingsInput; publicar requiere dirección y horario de apertura.',
    security: secured ? [{ PlatformCookie: [], ...(mutation ? { CsrfToken: [] } : {}) }] : mutation ? [{ CsrfToken: [] }] : [],
    parameters: [...url.matchAll(/\{(\w+)\}/g)].map(match => ({ name: match[1], in: 'path', required: true, schema: uuid })),
    ...(input ? { requestBody: { required: true, content: { 'application/json': { schema: ref(input) } } } } : {}),
    responses: { [status]: response('Operación completada', output), ...Object.fromEntries([400,401,403,404,409,429,503].map(code => [code,response(({400:'Datos inválidos o CSRF ausente',401:'Sesión ausente, revocada o credenciales incorrectas',403:'Sin permiso',404:'Tienda o usuario registrado no encontrado',409:'Enlace ocupado o asignación concurrente',429:'Límite de intentos',503:'Servicio no disponible'})[code],ref('Error'))])) },
  };
  spec.paths[url] = { ...(spec.paths[url] || {}), [method]: operation };
}
const upload=structuredClone(spec.paths['/admin/stores/{storeId}/cover-image'].post);
upload.summary='Subir portada desde plataforma';upload.operationId='post_platform_store_cover_image';upload.tags=['Plataforma'];
upload.description='Administrador de plataforma autenticado y CSRF. Tienda existente. JPG, PNG o WebP hasta 5 MB; devuelve una ruta relativa para guardar en coverImageUrl.';
upload.parameters=[{name:'id',in:'path',required:true,schema:uuid}];upload.security=[{PlatformCookie:[],CsrfToken:[]}];
spec.paths['/platform/stores/{id}/cover-image']={post:upload};
spec.info.version = '0.2.0';
spec.info.description = spec.info.description.replace('Google y SMS pendientes de credenciales/contrato externo.', 'Google configurable en localhost; SMS pendiente del gateway. Plataforma con acceso independiente de administración.');
writeFileSync(path, JSON.stringify(spec, null, 2) + '\n');
console.log('Platform contract updated: 10 operations, independent cookie and CSRF.');
