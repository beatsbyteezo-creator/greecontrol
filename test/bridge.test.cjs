const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const {once}=require('node:events');
const {mkdtemp,copyFile,writeFile,rm}=require('node:fs/promises');
const {tmpdir}=require('node:os');
const {join}=require('node:path');
const {createInterface}=require('node:readline');
const {test}=require('node:test');

test('bridge serializes clients, verifies adjustments and shares routines',async t=>{
  const root=await mkdtemp(join(tmpdir(),'gree-bridge-test-'));

  await copyFile(join(__dirname,'..','servidor.cjs'),join(root,'servidor.cjs'));
  await writeFile(join(root,'gree.cjs'),`
    const state={Pow:1,Mod:1,SetTem:22};
    let active=false;
    module.exports={
      connect:async()=>({ip:'192.0.2.1'}),
      status:async()=>({...state}),
      async command(_,name,value){
        if(active)throw new Error("Overlapping commands");
        active=true;
        await new Promise(resolve=>setTimeout(resolve,30));
        active=false;
        if(value!==25)state[name]=value;
        return {...state};
      }
    };
  `);
  const child=spawn(process.execPath,['servidor.cjs'],{
    cwd:root,env:{...process.env,LOCALAPPDATA:root,GREE_PORT:'0'},stdio:['ignore','pipe','inherit']
  });
  const lines=createInterface({input:child.stdout});
  t.after(async()=>{
    lines.close();
    if(child.exitCode===null&&child.signalCode===null){
      const closed=once(child,'exit');
      child.kill();
      await closed;
    }
    await rm(root,{recursive:true,force:true});
  });
  const [line,info]=await readStartup(lines,child);
  const url=new URL(line);
  const admin=url.searchParams.get('token');
  async function request(path,credential,body){
    const headers={'content-type':'application/json'};
    if(credential)headers['x-gree-token']=credential;
    const response=await fetch(new URL(path,url),body===undefined?{headers}:{method:'POST',headers,body:JSON.stringify(body)});
    return {status:response.status,data:await response.json().catch(()=>null)};
  }
  const pin=(await request('/api/info',admin)).data.pin;
  assert.ok(info.lanUrl);
  const mobile=(await request('/pair',null,{code:pin})).data.token;
  assert.notEqual(mobile,admin);
  assert.equal((await request('/api/info',mobile)).data.pin,null);
  assert.equal((await request('/api',{},{action:'status'})).status,404);
  assert.equal((await request('/api',admin,{action:'connect'})).status,200);
  const [first,second]=await Promise.all([
    request('/api',admin,{action:'command',name:'SetTem',value:23}),
    request('/api',mobile,{action:'command',name:'SetTem',value:24})
  ]);
  assert.equal(first.data.state.SetTem,23);
  assert.equal(second.data.state.SetTem,24);
  const current=await request('/api',admin,{action:'status'});
  const rejected=await request('/api',admin,{action:'plan',on:true,mode:'Frio',temp:25});
  assert.equal(rejected.data.outcome.ok,false);
  assert.equal(rejected.data.state.SetTem,current.data.state.SetTem);
  const adjusted=await request('/api',admin,{action:'plan',on:true,mode:'Auto',temp:26});
  assert.equal(adjusted.data.outcome.ok,true);
  assert.equal(adjusted.data.state.Mod,0);
  assert.equal(adjusted.data.state.SetTem,26);
  const routine={id:'test',type:'time',time:'22:00',action:'off',mode:'Frio',temp:24};
  assert.equal((await request('/api/routines',mobile,{action:'add',routine})).status,200);
  assert.equal((await request('/api/routines',admin)).data[0].id,routine.id);
  const {id}= (await request('/api/access',admin)).data.sessions[0];
  assert.equal((await request('/api/access',admin,{revoke:id})).status,200);
  assert.equal((await request('/api/routines',mobile)).status,404);
});

async function readStartup(lines,child){
  const entries=[];
  const startup=new Promise((resolve,reject)=>{
    lines.on('line',line=>{
      entries.push(line);
      if(entries.length===2)resolve([entries[0],JSON.parse(entries[1])]);
    });
    child.once('exit',code=>reject(new Error('Bridge exited before startup: '+code)));
    child.once('error',reject);
  });
  return startup;
}
