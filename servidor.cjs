const http=require('node:http');
const {readFile}=require('node:fs/promises');
const fs=require('node:fs');
const path=require('node:path');
const {randomBytes}=require('node:crypto');
const {isIPv4}=require('node:net');
const os=require('node:os');
const gree=require('./gree.cjs');
const appData=process.env.LOCALAPPDATA||os.homedir();
const dataDir=path.join(appData,'GreeControl');
const tokenFile=path.join(dataDir,'lan-token');
let token;
try{
 fs.mkdirSync(dataDir,{recursive:true});
 const saved=fs.readFileSync(tokenFile,'utf8').trim();
 token=/^[a-f0-9]{48}$/.test(saved)?saved:null;
}catch{}
if(!token){
 token=randomBytes(24).toString('hex');
 try{fs.mkdirSync(dataDir,{recursive:true});fs.writeFileSync(tokenFile,token,{encoding:'utf8',mode:0o600})}catch{}
}
function readData(name,fallback){
 try{return JSON.parse(fs.readFileSync(path.join(dataDir,name),'utf8'))}catch{return fallback}
}
function writeData(name,value){
 const target=path.join(dataDir,name),temporary=target+'.tmp';
 fs.writeFileSync(temporary,JSON.stringify(value),{mode:0o600});
 fs.renameSync(temporary,target);
}
let access=readData('lan-access.json',{enabled:true,sessions:[]});
let routines=readData('routines.json',[]);
const pin=String(randomBytes(4).readUInt32BE(0)%90000000+10000000);
const port=process.env.GREE_PORT==='0'?0:Number(process.env.GREE_PORT)||47821;
const files={'/':'index.html','/manifest.webmanifest':'manifest.webmanifest','/gree-icon.svg':'gree-icon.svg','/gree-icon.png':'gree-icon.png','/css/style.css':'css/style.css','/js/app.js':'js/app.js','/js/engine.js':'js/engine.js','/js/main.js':'js/main.js','/js/connection.js':'js/connection.js','/js/history.js':'js/history.js','/js/preferences.js':'js/preferences.js','/js/assistant.js':'js/assistant.js','/js/automation.js':'js/automation.js'};
const types={html:'text/html',css:'text/css',js:'text/javascript',webmanifest:'application/manifest+json',svg:'image/svg+xml',png:'image/png'};
let device,operations=Promise.resolve(),pending=0;
function enqueue(operation){
 if(pending>=32)return null;
 pending++;
 const result=operations.then(operation).finally(()=>{pending--});
 operations=result.catch(()=>{});
 return result;
}
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
async function readJson(req,limit){
 const chunks=[];
 let length=0;
 for await(const chunk of req){
  length+=chunk.length;
  if(length<=limit)chunks.push(chunk);
 }
 if(length>limit)throw new Error('Pedido grande demais');
 const input=JSON.parse(Buffer.concat(chunks));
 if(!input||typeof input!=='object'||Array.isArray(input))throw new Error('Pedido inválido');
 return input;
}
function validRoutine(item){
 return item&&typeof item.id==='string'&&item.id.length<=80&&
  ['on','off','adjust'].includes(item.action)&&['Frio','Calor','Auto'].includes(item.mode)&&
  Number.isInteger(item.temp)&&item.temp>=16&&item.temp<=30&&
  (item.type==='time'?/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(item.time):
   ['hot','cold'].includes(item.type)&&Number.isFinite(item.threshold)&&item.threshold>=-5&&item.threshold<=45);
}
async function applyPlan(device,plan){
 const modes={Auto:0,Frio:1,Calor:4};
 if(!Object.hasOwn(modes,plan.mode)||!Number.isInteger(plan.temp)||plan.temp<16||plan.temp>30)
  throw new Error('Modo ou temperatura inválidos.');
 let state=await gree.status(device);
 const finish=(ok,message,skipped=false)=>({state,outcome:{ok,message,skipped}});
 if(plan.condition==='adjust'&&state.Pow===0)
  return finish(false,'Gree desligado. O ajuste foi ignorado.',true);
 const requested=plan.on===false?[['Pow',0]]:[
  ...(plan.on===true?[['Pow',1]]:[]),['Mod',modes[plan.mode]],['SetTem',plan.temp]
 ];
 const labels={Pow:'o estado de energia',Mod:'o modo',SetTem:'a temperatura'};
 for(const [name,value] of requested){
  if(state[name]!==value){
   try{state=await gree.command(device,name,value)}
   catch{return finish(false,'Não foi possível confirmar '+labels[name]+'.')}
  }
  if(state[name]!==value)return finish(false,'O Gree não confirmou '+labels[name]+'.');
 }
 return finish(true,plan.on===false?'O Gree confirmou o desligamento.':
  'O Gree confirmou '+plan.mode+' a '+plan.temp+' °C.');
}
const server=http.createServer(async (req,res)=>{
 const path=req.url.split('?')[0];
 const loopback=req.socket.remoteAddress==='127.0.0.1';
 const credential=req.headers['x-gree-token'];
 const admin=loopback&&credential===token;
 const authorized=admin||access.enabled&&access.sessions.some(session=>session.token===credential);
 const json=(status,data)=>res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'}).end(JSON.stringify(data));
 if(!loopback&&!access.enabled){res.writeHead(403).end();return;}
 if(path==='/api/access'&&admin){
  try{
   if(req.method==='POST'){
    const input=await readJson(req,1024);
    const next={enabled:typeof input.enabled==='boolean'?input.enabled:access.enabled,
     sessions:access.sessions.filter(session=>session.id!==input.revoke)};
    writeData('lan-access.json',next);
    access=next;
   }else if(req.method!=='GET'){res.writeHead(405).end();return;}
   json(200,{enabled:access.enabled,sessions:access.sessions.map(({id,createdAt})=>({id,createdAt}))});
  }catch(error){json(400,{error:error.message});}
  return;
 }
 if(path==='/api/routines'&&authorized){
  try{
   if(req.method==='POST'){
    const {action,routine,id,enabled,outcome}=await readJson(req,4096);
    let next;
    if(action==='add'&&validRoutine(routine)&&routines.length<64){
     const {id,type,time,threshold,action,mode,temp,enabled}=routine;
     next=routines.some(item=>item.id===id)?routines:[...routines,{id,type,time,threshold,action,mode,temp,enabled:enabled!==false}];
    }else if(action==='remove')next=routines.filter(item=>item.id!==id);
    else if(action==='toggle'&&typeof enabled==='boolean')next=routines.map(item=>item.id===id?{...item,enabled}:item);
    else if(action==='outcome'&&admin){
     const {lastDay,lastCondition,lastRunAt,lastResult}=outcome;
     next=routines.map(item=>item.id===id?{...item,lastDay,lastCondition,lastRunAt,lastResult}:item);
    }else throw new Error('Rotina inválida ou limite atingido.');
    writeData('routines.json',next);
    routines=next;
   }else if(req.method!=='GET'){res.writeHead(405).end();return;}
   json(200,routines);
  }catch(error){json(400,{error:error.message});}
  return;
 }
 if(req.method==='POST'&&path==='/pair'){
  let input;try{input=await readJson(req,512)}catch{res.writeHead(400).end(JSON.stringify({error:'Código inválido'}));return;}
  const remote=req.socket.remoteAddress||'unknown',now=Date.now(),attempt=pairAttempts.get(remote)||{count:0,blockedUntil:0};
  if(attempt.blockedUntil>now){res.writeHead(429).end(JSON.stringify({error:'Muitas tentativas. Aguarde cinco minutos.'}));return;}
  if(input.code!==pin){attempt.count++;if(attempt.count>=5){attempt.count=0;attempt.blockedUntil=now+5*60*1000;}pairAttempts.set(remote,attempt);res.writeHead(401).end(JSON.stringify({error:'Código de pareamento incorreto'}));return;}
  if(!access.enabled||access.sessions.length>=32){json(403,{error:'Pareamento indisponível.'});return;}
  const session={id:randomBytes(8).toString('hex'),token:randomBytes(24).toString('hex'),createdAt:Date.now()};
  try{
   const next={...access,sessions:[...access.sessions,session]};
   writeData('lan-access.json',next);
   access=next;
   pairAttempts.delete(remote);
   json(200,{token:session.token});
  }catch{json(500,{error:'Não foi possível salvar o pareamento.'});}
  return;
 }
 if(req.method==='GET'&&path==='/api/info'&&authorized){
  res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'}).end(JSON.stringify({lanUrl,pin:admin?pin:null,admin}));return;
 }
 if(req.method==='GET'&&Object.hasOwn(files,path)){
  const file=files[path];
  try{const body=await readFile(__dirname+'/'+file);res.writeHead(200,{'Content-Type':types[file.split('.').pop()]+'; charset=utf-8','Cache-Control':'no-store'});res.end(body);}catch{res.writeHead(404).end();}
  return;
 }
 if(req.method!=='POST'||path!='/api'||!authorized){res.writeHead(404).end();return;}
 try{
  const input=await readJson(req,1024);
  const operation=enqueue(async()=>{
   let state,outcome;
   if(input.action==='connect'){
    if(input.ip&&!isIPv4(input.ip))throw new Error('Use um endereço IPv4 válido');
    const candidate=await gree.connect(input.ip);
    state=await gree.status(candidate);
    device=candidate;
   }else{
    if(!device)throw new Error('Conecte o aparelho primeiro');
    if(input.action==='command'){
     state=await gree.command(device,input.name,input.value);
     if(state[input.name]!==input.value)outcome={ok:false,message:'O Gree não confirmou o ajuste solicitado.'};
    }else if(input.action==='plan')({state,outcome}=await applyPlan(device,input));
    else if(input.action==='status')state=await gree.status(device);
    else throw new Error('Operação inválida');
   }
   return {state,ip:device.ip,outcome};
  });
  if(!operation){res.writeHead(409).end(JSON.stringify({error:'Aguarde a operação atual'}));return;}
  res.end(JSON.stringify(await operation));
 }catch(error){res.writeHead(400).end(JSON.stringify({error:error.message}));}
});
server.listen(port,'0.0.0.0',()=>{
 const ip=findLanAddress();
 const listenPort=server.address().port;
 lanUrl=ip?'http://'+ip+':'+listenPort+'/':'Nenhum IPv4 privado ativo foi encontrado';
 console.log('http://127.0.0.1:'+listenPort+'/?token='+token);
 console.log(JSON.stringify({lanUrl,pin}));
});
