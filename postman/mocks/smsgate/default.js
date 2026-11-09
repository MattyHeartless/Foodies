// Local fixture only; never forwards to SMSGate.
const http = require('http');
http.createServer((req, res) => {
  const reply = (code, body) => { res.writeHead(code, {'Content-Type':'application/json'}); res.end(JSON.stringify(body)); };
  if (req.method !== 'POST' || new URL(req.url,'http://localhost').pathname !== '/3rdparty/v1/messages') return reply(501, {error:'Outside integration subset'});
  if (req.headers.authorization !== 'Basic ' + Buffer.from('fixture:fixture-only').toString('base64')) return reply(401,{error:'Invalid fixture credentials'});
  let raw = '';
  req.on('data', chunk => raw += chunk);
  req.on('end', () => {
    let body;
    try { body = JSON.parse(raw); } catch { return reply(400,{error:'Invalid JSON'}); }
    if (!body.id || !body.textMessage?.text || body.textMessage.text.length !== 180 || body.phoneNumbers?.length !== 1 || body.ttl !== 300) return reply(400,{error:'Invalid request'});
    if (body.id === 'fixture-failure') return reply(500,{error:'Provider failure'});
    return reply(202,{id:body.id,state:'Pending'});
  });
}).listen(process.env.PORT || 4500);
