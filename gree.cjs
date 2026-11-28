const dgram=require('node:dgram');
const os=require('node:os');
const {createCipheriv,createDecipheriv}=require('node:crypto');
const keys={gcm:'{yxAHAY_Lm6pbC/<',ecb:'a3K8Bx%2r8Y7#xDh'};
const nonce=Buffer.from('5440784449675a516c5e6313','hex');
const aad=Buffer.from('qualcomm-test');
const cols=['Pow','Mod','SetTem','WdSpd','SwUpDn','SvSt','SwhSlp'];
function crypt(mode,key){return ['aes-128-'+mode,Buffer.from(key),mode==='gcm'?nonce:null]}
function unpack(packet,mode,key){
 if(!packet.pack||typeof packet.pack==='object')return packet.pack||packet;
 const cipher=createDecipheriv(...crypt(mode,key));
 if(mode==='gcm'){cipher.setAAD(aad);cipher.setAuthTag(Buffer.from(packet.tag,'base64'))}
 return JSON.parse(Buffer.concat([cipher.update(Buffer.from(packet.pack,'base64')),cipher.final()]));
}
function pack(mac,data,mode,key,i){
 const cipher=createCipheriv(...crypt(mode,key));if(mode==='gcm')cipher.setAAD(aad);
 const envelope={t:'pack',cid:'app',tcid:mac,uid:0,i,pack:Buffer.concat([cipher.update(JSON.stringify(data)),cipher.final()]).toString('base64')};
 if(mode==='gcm')envelope.tag=cipher.getAuthTag().toString('base64');
 return envelope;
}
function broadcastTargets(){
 const out=new Set(['255.255.255.255']);
 const toInt=a=>((a[0]<<24)|(a[1]<<16)|(a[2]<<8)|a[3])>>>0;
 const fromInt=n=>[(n>>>24)&255,(n>>>16)&255,(n>>>8)&255,n&255].join('.');
 for(const rows of Object.values(os.networkInterfaces()))for(const net of rows||[]){
  if(net.family!=='IPv4'||net.internal||!net.address||!net.netmask)continue;
  const ipParts=net.address.split('.').map(Number),maskParts=net.netmask.split('.').map(Number);
  const ip=toInt(ipParts),mask=toInt(maskParts),network=(ip&mask)>>>0,broadcast=(network|(~mask>>>0))>>>0;
  out.add(fromInt(broadcast));
  const hosts=broadcast-network-1;
  if(hosts>0&&hosts<=4094){for(let address=network+1;address<broadcast;address++)if(address!==ip)out.add(fromInt(address))}
 }
 return [...out];
}
function exchange(socket,ip,body,accept,{broadcast=false,timeoutMs=2500,destinations=null,ports=[7000]}={}){
 return new Promise((resolve,reject)=>{
  let done=false;
  const finish=(error,value)=>{if(done)return;done=true;clearTimeout(timeout);socket.off('message',onMessage);error?reject(error):resolve(value)};
  const onMessage=(bytes,remote)=>{
   if(!ports.includes(remote.port)||(!broadcast&&remote.address!==ip))return;
   let result;try{result=accept(JSON.parse(bytes),remote)}catch{return}
   if(result)finish(null,result);
  };
  const timeout=setTimeout(()=>finish(new Error('Sem resposta do Gree')),timeoutMs);
  socket.on('message',onMessage);
  const targets=destinations||(broadcast?broadcastTargets():[ip]),payload=JSON.stringify(body);
  function sendBatch(offset){
   if(done)return;
   for(const destination of targets.slice(offset,offset+64)){
    for(const port of ports)socket.send(payload,port,destination,()=>{});
   }
   if(offset+64<targets.length)setTimeout(()=>sendBatch(offset+64),20);
  }
  sendBatch(0);
 });
}
async function connect(address){
 const socket=dgram.createSocket('udp4');
 try{
  await new Promise((resolve,reject)=>{socket.once('error',reject);socket.bind(0,()=>{socket.off('error',reject);resolve()})});
  socket.setBroadcast(true);
  const scanTargets=address?null:broadcastTargets();
  const found=await exchange(socket,address||'',{t:'scan'},(packet,remote)=>{
   for(const mode of Object.keys(keys)){
    try{
     const info=unpack(packet,mode,keys[mode]),mac=info.mac||info.cid||info.device;
     if(mac&&(info.t==='dev'||info.t==='scan'||info.mac||info.device))return{ip:remote.address,mac,port:remote.port};
    }catch{}
   }
  },{broadcast:!address,destinations:scanTargets,ports:[7000,7001],timeoutMs:address?2200:16000}).catch(()=>null);
  if(!found)throw new Error(address?'O Gree nao respondeu ao IP informado nas portas 7000/7001':'Nenhum Gree respondeu em '+scanTargets.length+' enderecos IPv4 locais (portas 7000/7001)');
  const {ip,mac,port}=found;
  for(const mode of Object.keys(keys)){
   const generic=keys[mode];
   const bound=await exchange(socket,ip,pack(mode==='gcm'?'':mac,{mac,t:'bind',uid:0},mode,generic,1),packet=>{
    try{const result=unpack(packet,mode,generic);return result.t==='bindok'&&typeof result.key==='string'&&Buffer.byteLength(result.key)===16?result.key:null}catch{return null}
   },{ports:[port]}).catch(()=>null);
   if(bound)return{ip,mac,port,mode,key:bound};
  }
  throw new Error('Gree encontrado, mas nao aceitou o pareamento');
 }finally{socket.close()}
}
async function status(device){
 const {ip,mac,mode,key,port=7000}=device,socket=dgram.createSocket('udp4');
 try{
  await new Promise((resolve,reject)=>{socket.once('error',reject);socket.bind(0,()=>{socket.off('error',reject);resolve()})});
  return await exchange(socket,ip,pack(mac,{t:'status',mac,cols},mode,key,0),packet=>{
   try{const r=unpack(packet,mode,key);if(r.t!=='dat'||!Array.isArray(r.cols)||!Array.isArray(r.dat))return;const state=Object.fromEntries(r.cols.map((name,i)=>[name,r.dat[i]]));if([0,1].includes(state.Pow)&&Number.isInteger(state.SetTem))return state}catch{}
  },{ports:[port]});
 }finally{socket.close()}
}
async function command(device,name,value){
 const limits={Pow:[0,1],Mod:[0,4],SetTem:[16,30],WdSpd:[0,5],SwUpDn:[0,11],SvSt:[0,1],SwhSlp:[0,1]};
 if(!Object.hasOwn(limits,name)||!Number.isInteger(value)||value<limits[name][0]||value>limits[name][1])throw new Error('Comando invalido');
 const {ip,mac,mode,key,port=7000}=device,socket=dgram.createSocket('udp4');
 try{
  await new Promise((resolve,reject)=>{socket.once('error',reject);socket.bind(0,()=>{socket.off('error',reject);resolve()})});
  await exchange(socket,ip,pack(mac,{t:'cmd',opt:[name],p:[value]},mode,key,0),packet=>{try{return unpack(packet,mode,key).t==='res'}catch{return false}},{ports:[port]});
 }finally{socket.close()}
 return status(device);
}
module.exports={connect,status,command};
