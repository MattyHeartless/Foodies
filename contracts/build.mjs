// Source for the local OpenAPI contract. Run: node contracts/build.mjs
import { writeFileSync } from 'node:fs';
const str = (maxLength) => ({ type:'string', ...(maxLength ? {maxLength} : {}) });
const uuid = {type:'string',format:'uuid'};
const date = {type:'string',format:'date-time'};
const num = {type:'number',multipleOf:.01,minimum:0,maximum:100000};
const bool = {type:'boolean'};
const integer = {type:'integer'};
const ref = name => ({$ref:`#/components/schemas/${name}`});
const array = item => ({type:'array',items:typeof item==='string'?ref(item):item});
const object = (properties, required = Object.keys(properties)) => ({type:'object',properties,required});
const coverImage = {type:['string','null'],maxLength:500,pattern:'^(https://|/api/v1/media/stores/[a-f0-9]{32}/[a-f0-9]{32}[.](jpg|png|webp)$)',description:'External HTTPS URL or relative path for an uploaded image owned by this store. Null clears the cover.'};
const schemas = {
 Error:object({code:str(),message:str(),traceId:str()},['code','message']),
 Hour:object({dayOfWeek:{type:'integer',minimum:0,maximum:6},openingTime:{type:'string',example:'08:00:00'},closingTime:{type:'string',example:'18:00:00'},isClosed:bool}),
 Store:object({id:uuid,slug:str(80),name:str(100),description:str(500),addressText:str(300),timeZoneId:str(100),isActive:bool,isPaused:bool,minimumOrderAmount:num,averagePreparationMinutes:{type:'integer',minimum:1,maximum:240},operationalState:{type:'string',enum:['Abierta','Cerrada','Pausada']},hours:array('Hour')}),
 SettingsInput:object({name:str(100),description:str(500),addressText:str(300),timeZoneId:str(100),isActive:bool,isPaused:bool,minimumOrderAmount:num,averagePreparationMinutes:{type:'integer',minimum:1,maximum:240},hours:{...array('Hour'),maxItems:28}}),
 Category:object({id:uuid,storeId:uuid,name:str(80),sortOrder:integer,isActive:bool}),
 CategoryInput:object({name:str(80),sortOrder:integer,isActive:bool}),
 DishInput:object({categoryId:uuid,name:str(120),description:str(500),price:{...num,exclusiveMinimum:0},imageUrl:{type:['string','null'],maxLength:500},isActive:bool,isAvailable:bool,sortOrder:integer}),
 Dish:object({id:uuid,storeId:uuid,categoryId:uuid,name:str(),description:str(),price:num,imageUrl:{type:['string','null']},isActive:bool,isAvailable:bool,sortOrder:integer,createdAt:date,updatedAt:date}),
 OrderInput:object({storeId:uuid,items:{type:'array',minItems:1,maxItems:30,items:object({dishId:uuid,quantity:{type:'integer',minimum:1,maximum:50},expectedUnitPrice:num})}}),
 OrderItem:object({dishId:uuid,nameSnapshot:str(),unitPriceSnapshot:num,quantity:integer,lineTotal:{type:'number',multipleOf:.01}}),
 OrderHistory:object({previousStatus:{type:['string','null']},newStatus:str(),reason:{type:['string','null']},changedAt:date}),
 Order:object({id:uuid,storeId:uuid,storeName:{type:['string','null']},publicCode:str(),status:{type:'string',enum:['Pendiente','Aceptado','EnPreparacion','Listo','Entregado','Rechazado','Cancelado','NoRecogido']},total:{type:'number',multipleOf:.01},createdAt:date,acceptedAt:{type:['string','null'],format:'date-time'},readyAt:{type:['string','null'],format:'date-time'},deliveredAt:{type:['string','null'],format:'date-time'},estimatedPreparationSnapshotMin:{type:['integer','null']},activeOrdersAheadSnapshot:{type:['integer','null']},items:array('OrderItem'),history:array('OrderHistory')}),
 User:object({id:uuid,displayName:str(100),email:str(254),phoneE164:{type:['string','null']},phoneVerifiedAt:{type:['string','null'],format:'date-time'}}),
 Session:object({csrfToken:str(),user:{anyOf:[ref('User'),{type:'null'}]},memberships:array(object({storeId:uuid,role:{type:'string',enum:['Manager','Kitchen']},name:str()})),googleConfigured:bool,smsConfigured:bool,phoneVerificationRequired:bool,developmentAccess:bool}),
};
schemas.Store.properties.coverImageUrl = coverImage;
schemas.SettingsInput.properties.coverImageUrl = coverImage;
const paths={};
function add(method,path,summary,{schema,body,auth=false,created=false,description='',headers=[],parameters=[],noContent=false,sse=false}={}) {
 const params=[...path.matchAll(/\{(\w+)\}/g)].map(([,name])=>({name,in:'path',required:true,schema:name==='slug'||name==='account'?str():uuid}));
 const response=noContent?{description:'Operación completada.'}:sse?{description:'Stream autenticado. Reconectar y recargar REST al abrir o reconectar.',content:{'text/event-stream':{schema:{type:'string'},example:'retry: 5000\nevent: connected\ndata: {}\n\n: heartbeat\n\nevent: order.created\ndata: {"resourceId":"..."}\n\n'}}}:{description:'Operación completada.',...(schema?{content:{'application/json':{schema:typeof schema==='string'?ref(schema):schema}}}:{})};
 const security=auth?{SessionCookie:[],...(method!=='get'?{CsrfToken:[]}:{})}:method!=='get'?{CsrfToken:[]}:null;
 paths[path]??={};paths[path][method]={summary,description,operationId:method+'_'+path.replace(/[^a-zA-Z0-9]/g,'_'),tags:[path.includes('/admin/')?'Administración':path.includes('/auth/')?'Identidad':path.includes('/me')?'Cuenta':'Público y pedidos'],parameters:[...params,...headers.map(h=>({in:'header',required:true,name:h,schema:str(100)})),...parameters],...(security?{security:[security]}:{security:[]}),...(body?{requestBody:{required:true,content:{'application/json':{schema:typeof body==='string'?ref(body):body}}}}:{}),responses:{[noContent?204:created?201:200]:response,...Object.fromEntries([400,401,403,404,409,422,429,502,503].map(s=>[s,{description:({400:'Entrada o CSRF inválido.',401:'Inicia sesión.',403:'Sin membresía/rol para esta tienda.',404:'Recurso inexistente o no pertenece al usuario/tienda.',409:'Estado, disponibilidad, precio o idempotencia en conflicto.',422:'Teléfono sin verificar, mínimo incumplido u OTP inválido/vencido.',429:'Límite de frecuencia alcanzado; esperar antes de reintentar.',502:'Gateway no confirmó el envío.',503:'Integración no configurada o servicio/base no disponible.'})[s],...(s===429?{}:{content:{'application/json':{schema:ref('Error')}}})}]))}};
}
add('get','/health','Conectividad de SQL Server',{schema:object({status:str(),databaseConnected:bool})});
add('get','/auth/session','Sesión, membresías y token CSRF',{schema:'Session',description:'Consultar sin autenticación para obtener la cookie CSRF y csrfToken. Repetir después de iniciar/cerrar sesión. No guarda tokens OAuth.'});
add('get','/auth/google/start','Iniciar Google OAuth con PKCE',{parameters:[{name:'returnUrl',in:'query',schema:str()}],description:'Redirige a Google (302). Devuelve 503 hasta configurar ClientId/ClientSecret. returnUrl solo acepta rutas locales.'});
paths['/auth/google/start'].get.responses['302']={description:'Redirección al proveedor Google.'};delete paths['/auth/google/start'].get.responses['200'];
add('get','/auth/google/callback','Callback interno de Google',{description:'Gestionado por el middleware OAuth. No se invoca manualmente. Valida state/correlation, intercambia el código y emite sesión HttpOnly.'});
paths['/auth/google/callback'].get.responses={'302':{description:'Regresa a la ruta local solicitada o a /perfil?authError=1.'}};
add('post','/auth/logout','Cerrar sesión',{auth:true,noContent:true});
add('post','/auth/development/{account}','Acceso local de desarrollo',{description:'SOLO Development y DevelopmentAccess:Enabled. Cuentas permitidas: customer, manager, kitchen, manager-two. No verifica teléfono ni envía SMS.',schema:object({displayName:str()})});
add('get','/stores','Tiendas activas y estado operativo',{schema:array('Store')});
add('get','/stores/{slug}','Datos y horarios de tienda',{schema:'Store'});
add('get','/stores/{storeId}/dishes','Menú público por tienda',{schema:object({categories:array('Category'),dishes:array('Dish')}),description:'Solo categorías y platillos activos; isAvailable=false permite mostrar agotados sin poder solicitarlos.'});
add('get','/me','Perfil actual',{auth:true,schema:'User'});
add('patch','/me','Actualizar nombre visible',{auth:true,body:object({displayName:str(100)}),noContent:true});
add('patch','/me/phone','Guardar teléfono sin verificar',{auth:true,body:object({phoneE164:{type:'string',pattern:'^\\+[1-9]\\d{7,14}$'}}),noContent:true,description:'Disponible solo con PhoneVerification:AllowUnverifiedOrders=true y Sms:Enabled=false. Valida E.164, guarda sin marcarlo verificado e invalida OTP anteriores al cambiar de número. Repetir el número conserva su verificación. Si se exige PIN devuelve 409 phone_verification_required.'});
add('post','/me/phone-verifications','Enviar código de teléfono',{auth:true,body:object({phoneE164:{type:'string',pattern:'^\\+[1-9]\\d{7,14}$'}}),schema:object({expiresAt:date}),description:'Caduca en 5 minutos. Máximo 5 envíos por hora y 1 por minuto. Invalida los códigos anteriores. 503 si SMS está deshabilitado. No cambia el teléfono verificado todavía.'});
add('post','/me/phone-verifications/confirm','Verificar teléfono',{auth:true,body:object({code:{type:'string',pattern:'^[0-9]{6}$'}}),noContent:true,description:'Máximo 5 intentos por código, un solo uso. Confirmación atómica. Guarda únicamente hash con salt.'});
add('get','/me/orders','Últimos 100 pedidos propios',{auth:true,schema:array('Order')});
add('get','/me/orders/{orderId}','Detalle de pedido propio',{auth:true,schema:'Order'});
add('post','/orders','Confirmar carrito',{auth:true,body:'OrderInput',schema:'Order',created:true,headers:['Idempotency-Key'],description:'La clave es única por cliente. Reintentos idénticos regresan el mismo pedido (201); reutilizarla con otro carrito devuelve 409. Cantidades 1–50, hasta 30 platillos distintos. Revalida teléfono guardado, horarios, pausa, categorías, disponibilidad y mínimo. Por defecto exige teléfono verificado; temporalmente admite sin verificar solo si PhoneVerification:AllowUnverifiedOrders=true y Sms:Enabled=false. Habilitar SMS restaura la exigencia de verificación. expectedUnitPrice debe coincidir; ante price_changed (409), actualizar menú y exigir revisión antes de reintentar. Calcula totales con decimal MXN y guarda snapshots.'});
const admin='/admin/stores/{storeId}';
const uploadSchema=object({file:{type:'string',format:'binary'} });
add('post',admin+'/cover-image','Subir portada de tienda',{auth:true,schema:object({url:str(500)}),description:'Solo Manager de esta tienda y CSRF. Un archivo JPG, PNG o WebP, hasta 5 MB. Devuelve una ruta relativa; guardar settings.coverImageUrl para publicarla. Nombre aleatorio y carpeta por tienda.'});
paths[admin+'/cover-image'].post.requestBody={required:true,content:{'multipart/form-data':{schema:uploadSchema}}};
for(const code of [413,415])paths[admin+'/cover-image'].post.responses[code]={description:code===413?'Archivo de más de 5 MB.':'Formato no admitido o firma inválida.',content:{'application/json':{schema:ref('Error')}}};
add('get','/media/stores/{storeId}/{fileName}','Leer una imagen pública',{description:'Solo nombres UUID generados por la API. Imagen pública con tipo explícito, nosniff y caché inmutable.'});
paths['/media/stores/{storeId}/{fileName}'].get.parameters.find(p=>p.name==='fileName').schema={type:'string',pattern:'^[a-f0-9]{32}[.](jpg|png|webp)$'};
paths['/media/stores/{storeId}/{fileName}'].get.responses[200]={description:'Archivo de imagen.',content:Object.fromEntries(['image/jpeg','image/png','image/webp'].map(type=>[type,{schema:{type:'string',format:'binary'}}]))};

add('post',`${admin}/orders/{orderId}/cancel`,'Cancelar desde cocina y avisar al cliente',{auth:true,body:object({reason:{type:'string',minLength:1,maxLength:500}}),schema:'Order',description:'Solo encargado o cocina de esta tienda. Desde Pendiente, Aceptado o EnPreparacion; Listo y estados finales devuelven 409. Motivo no vacío obligatorio, hasta 500 caracteres. Historial y un único OrderCancelled en la misma transacción, solo para teléfono verificado. Repetir una cancelación ya aplicada no duplica historial ni SMS. No permite reactivar el pedido. Texto SMS fijo de exactamente 180 caracteres; el motivo completo se consulta en Mis pedidos.'});
add('get',admin+'/orders','Últimos 150 pedidos de la tienda',{auth:true,schema:array('Order')});
for(const [action,label] of [['accept','Aceptar y guardar estimación'],['reject','Rechazar con motivo'],['start-preparation','Iniciar preparación'],['ready','Marcar listo y generar SMS'],['delivered','Marcar entregado']])add('post',`${admin}/orders/{orderId}/${action}`,label,{auth:true,schema:'Order',...(action==='reject'?{body:object({reason:str(500)})}:{}),description:'Membresía activa de encargado o cocina. Transición atómica. Repetir el estado actual no duplica historial ni outbox. Flujo principal Pendiente→Aceptado→EnPreparacion→Listo→Entregado y Pendiente→Rechazado; cancelación de cocina en el endpoint cancel. En accept: promedio + 2 × pedidos anteriores activos de esta tienda; snapshot estable. En ready: un registro único de outbox solo si el teléfono está verificado; no se encolan SMS a números sin verificar. Fallo SMS no revierte estado.'});
for(const entity of ['dishes','categories']){
 const model=entity==='dishes'?'Dish':'Category';
 add('get',`${admin}/${entity}`,'Catálogo administrativo: '+entity,{auth:true,schema:array(model)});
 add('post',`${admin}/${entity}`,'Crear '+entity,{auth:true,body:model+'Input',schema:model,created:true});
 add('patch',`${admin}/${entity}/{${entity==='dishes'?'dishId':'categoryId'}}`,'Editar '+entity,{auth:true,body:model+'Input',schema:model,description:'Recibe el DTO editable COMPLETO. La categoría de un platillo debe pertenecer a la misma tienda. Desactivación lógica: el historial permanece intacto.'});
}
const kitchenPhone = {type:['string','null'],pattern:'^\\+52[0-9]{10}$',description:'Número privado de avisos a cocina. Null desactiva OrderPlaced; solo Manager puede editarlo.'};
schemas.AdminSettings = {...schemas.Store,properties:{...schemas.Store.properties,kitchenPhoneE164:kitchenPhone}};
schemas.AdminSettingsInput = {...schemas.SettingsInput,properties:{...schemas.SettingsInput.properties,kitchenPhoneE164:kitchenPhone}};
add('get',admin+'/settings','Configuración de la tienda',{auth:true,schema:'AdminSettings'});
add('patch',admin+'/settings','Guardar configuración y horarios',{auth:true,body:'AdminSettingsInput',schema:'AdminSettings',description:'Solo Manager. DTO completo. kitchenPhoneE164: +52 y diez dígitos, opcional; vacío/null desactiva avisos de pedido nuevo. No se publica en el catálogo. Apertura inclusiva, cierre exclusivo; cierre anterior a apertura significa horario nocturno. Varias filas soportan horarios partidos. Zona horaria validada por el servidor. No hay política automática de feriados.'});
add('get',admin+'/events','Eventos SSE de la tienda',{auth:true,sse:true,description:'Membresía comprobada al abrir y durante heartbeat (15 s), con cierre al caducar sesión. Eventos: order.created, order.status-changed, dish.availability-changed; solo resourceId. Stream en memoria para una instancia; no tiene replay. Al abrir/reconectar, recargar REST; frontend hace respaldo cada 20 s. retry:5000.'});
const spec={openapi:'3.1.0',info:{title:'Foodies · Pedidos anticipados',version:'0.1.0',description:'Contrato local del MVP. Moneda piloto MXN, pago al recoger. La API es fuente de verdad. Seguridad con cookie HttpOnly + X-CSRF-TOKEN; sin tokens en localStorage. Cancelado y NoRecogido están modelados sin acciones habilitadas. Google y SMS pendientes de credenciales/contrato externo.'},servers:[{url:'http://127.0.0.1:3002/api/v1',description:'Next.js proxy local'},{url:'http://127.0.0.1:5088/api/v1',description:'API local directa'}],components:{schemas,securitySchemes:{SessionCookie:{type:'apiKey',in:'cookie',name:'foodies.session'},CsrfToken:{type:'apiKey',in:'header',name:'X-CSRF-TOKEN'}}},paths};
writeFileSync(new URL('../backend/Foodies.Api/openapi.json',import.meta.url),JSON.stringify(spec,null,2)+'\n');
console.log(`OpenAPI generado: ${Object.keys(paths).length} rutas, ${Object.values(paths).reduce((s,p)=>s+Object.keys(p).length,0)} operaciones.`);
await import('./update-platform-spec.mjs');
