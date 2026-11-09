import assert from 'node:assert/strict';
const base=process.env.FOODIES_ORIGIN||'http://127.0.0.1:3002';
const get=async path=>{const r=await fetch(base+path);assert.equal(r.status,200,path);return r.json();};
const health=await get('/api/v1/health');assert.equal(health.databaseConnected,true);
const stores=await get('/api/v1/stores');assert.ok(stores.length>=2);
for(const s of stores){for(const key of ['id','slug','name','operationalState','minimumOrderAmount','hours'])assert.ok(key in s,key);const menu=await get(`/api/v1/stores/${s.id}/dishes`);assert.ok(Array.isArray(menu.dishes));assert.ok(menu.dishes.every(d=>d.storeId===s.id));}
const session=await get('/api/v1/auth/session');assert.ok(session.csrfToken);assert.equal(session.user,null);
const platform=await get('/api/v1/platform/session');assert.ok(platform.csrfToken);assert.equal(platform.administrator,null);
assert.equal((await fetch(base+'/api/v1/platform/stores')).status,401);
for(const path of ['/','/tiendas/'+stores[0].slug,'/perfil','/mis-pedidos','/admin','/admin/catalogo','/admin/configuracion','/plataforma']){const r=await fetch(base+path);assert.equal(r.status,200,path);assert.ok((await r.text()).includes('foodies'));}
console.log('HTTP smoke OK: SQL listo, tiendas aisladas, sesión/CSRF de clientes y plataforma, administración protegida y 8 rutas Next.js.');
