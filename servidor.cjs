const http=require('node:http');
const {readFile}=require('node:fs/promises');
const {randomBytes}=require('node:crypto');
const {isIPv4}=require('node:net');
const os=require('node:os');
const gree=require('./gree.cjs');
const token=randomBytes(24).toString('hex');
const pin=String(randomBytes(4).readUInt32BE(0)%90000000+10000000);
const files={'/':'index.html','/manifest.webmanifest':'manifest.webmanifest','/gree-icon.svg':'gree-icon.svg','/gree-icon.png':'gree-icon.png','/css/style.css':'css/style.css','/js/app.js':'js/app.js','/js/engine.js':'js/engine.js','/js/main.js':'js/main.js','/js/connection.js':'js/connection.js','/js/history.js':'js/history.js','/js/preferences.js':'js/preferences.js','/js/assistant.js':'js/assistant.js','/js/automation.js':'js/automation.js'};
const types={html:'text/html',css:'text/css',js:'text/javascript',webmanifest:'application/manifest+json',svg:'image/svg+xml',png:'image/png'};
let device,busy=false;
const pairAttempts=new Map();
let lanUrl='';
function findLanAddress(){
 const found=[];
 for(const [name,rows] of Object.entries(os.networkInterfaces()))for(const net of rows||[]){
  if(net.family!=='IPv4'||net.internal||!net.address||net.address.startsWith('169.254.'))continue;
  const p=net.address.split('.').map(Number),privateIP=p[0]===10||p[0]===192&&p[1]===168||p[0]===172&&p[1]>=16&&p[1]<=31;
  if(privateIP)found.push({name,ip:net.address,wifi:/wi-?fi|wireless/i.test(name)});
 }
 found.sort((a,b)=>Number(b.wifi)-Number(a.wifi));
 return found[0]?.ip||'';
}
const server=http.createServer(async (req,res)=>{
 const path=req.url.split('?')[0];
 if(req.method==='POST'&&path==='/pair'){
  let raw='';for await(const chunk of req){raw+=chunk;if(raw.length>512)break;}
  let input;try{input=JSON.parse(raw)}catch{res.writeHead(400).end(JSON.stringify({error:'Código inválido'}));return;}
  const remote=req.socket.remoteAddress||'unknown',now=Date.now(),attempt=pairAttempts.get(remote)||{count:0,blockedUntil:0};
  if(attempt.blockedUntil>now){res.writeHead(429).end(JSON.stringify({error:'Muitas tentativas. Aguarde cinco minutos.'}));return;}
  if(input.code!==pin){attempt.count++;if(attempt.count>=5){attempt.count=0;attempt.blockedUntil=now+5*60*1000;}pairAttempts.set(remote,attempt);res.writeHead(401).end(JSON.stringify({error:'Código de pareamento incorreto'}));return;}
  pairAttempts.delete(remote);res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'}).end(JSON.stringify({token}));return;
 }
 if(req.method==='GET'&&path==='/api/info'&&req.headers['x-gree-token']===token){
  res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'}).end(JSON.stringify({lanUrl,pin}));return;
 }
 if(req.method==='GET'&&Object.hasOwn(files,path)){
  const file=files[path];
  try{const body=await readFile(__dirname+'/'+file);res.writeHead(200,{'Content-Type':types[file.split('.').pop()]+'; charset=utf-8','Cache-Control':'no-store'});res.end(body);}catch{res.writeHead(404).end();}
  return;
 }
 if(req.method!=='POST'||path!='/api'||req.headers['x-gree-token']!==token){res.writeHead(404).end();return;}
 if(busy){res.writeHead(409).end(JSON.stringify({error:'Aguarde a operação atual'}));return;}
 busy=true;
 try{
  let raw='';
  for await(const chunk of req){raw+=chunk;if(raw.length>1024)throw new Error('Pedido grande demais');}
  const input=JSON.parse(raw);
  let state;
  if(input.action==='connect'){
   if(input.ip&&!isIPv4(input.ip))throw new Error('Use um endereço IPv4 válido');
   const candidate=await gree.connect(input.ip);
   state=await gree.status(candidate);
   device=candidate;
  }else{
   if(!device)throw new Error('Conecte o aparelho primeiro');
   if(input.action==='command')state=await gree.command(device,input.name,input.value);
   else if(input.action==='status')state=await gree.status(device);
   else throw new Error('Operação inválida');
  }
  res.end(JSON.stringify({state,ip:device.ip}));
 }catch(error){res.writeHead(400).end(JSON.stringify({error:error.message}));}
 finally{busy=false;}
});
server.listen(0,'0.0.0.0',()=>{
 const port=server.address().port,ip=findLanAddress();
 lanUrl=ip?'http://'+ip+':'+port+'/':'Nenhum IPv4 privado ativo foi encontrado';
 console.log('http://127.0.0.1:'+port+'/?token='+token);
 console.log(JSON.stringify({lanUrl,pin}));
});
