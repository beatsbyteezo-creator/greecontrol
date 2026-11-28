{
  let token=new URLSearchParams(location.search).get('token');
  if(token){try{localStorage.setItem('gree.sessionToken',token)}catch{}}
  if(!token){try{token=localStorage.getItem('gree.sessionToken')||sessionStorage.getItem('gree.sessionToken')}catch{}}
  window.greeLocalRequest=async(path,body)=>{
    if(!token)throw new Error('Pareie este dispositivo para continuar.');
    const headers={'x-gree-token':token};
    const request=body===undefined?{headers}:{method:'POST',headers:{...headers,'content-type':'application/json'},body:JSON.stringify(body)};
    const response=await fetch(path,request);
    const data=await response.json().catch(()=>null);
    if(!response.ok)throw new Error(data?.error||'Não foi possível acessar o Gree Control.');
    return data;
  };
  const dialog=$('#connectDialog'), ipInput=$('#deviceIP'), message=$('#connectMessage');
  const connection=document.createElement('button');
  connection.id='connection';
  connection.type='button';
  connection.setAttribute('aria-live','polite');
  connection.textContent='Buscando o Gree...';
  $('.top').after(connection);
  const modesWire=[1,4,2,3,0], fansWire=[0,1,3,5], vanesWire=[0,1,2,4,6];
  const labels=['Auto','Balanco','Alto','Medio','Baixo'];
  const pairDialog=$('#pairDialog'),pairForm=$('#pairForm'),pairCode=$('#pairCode'),pairMessage=$('#pairMessage');
  if(!token){setTimeout(()=>{if(!pairDialog.open)pairDialog.showModal()},80)}
  pairForm.onsubmit=async event=>{
    event.preventDefault();pairMessage.textContent='Validando código…';
    try{
      const response=await fetch('/pair',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({code:pairCode.value.trim()})});
      const data=await response.json();if(!response.ok)throw new Error(data.error||'Não foi possível parear este Safari.');
      token=data.token;localStorage.setItem('gree.sessionToken',token);sessionStorage.setItem('gree.sessionToken',token);location.reload();
    }catch(error){pairMessage.textContent=error.message;}
  };
  const controls=$$('#pw,#tempSlider,#velBtn,.mode,#fanSlider,.chip');
  let state=null, pending=0, requests=Promise.resolve(), reconnectTimer=0, retryDelay=5000, connecting=false;
  const rememberedIP=()=>{try{return localStorage.getItem('gree.lastIP')||''}catch{return ''}};
  const rememberIP=ip=>{try{if(ip)localStorage.setItem('gree.lastIP',ip)}catch{}};
  st.on=false;
  if(token)fetch('/api/info',{headers:{'x-gree-token':token}}).then(response=>{
    if(!response.ok){
      if(!new URLSearchParams(location.search).has('token')){
        try{localStorage.removeItem('gree.sessionToken');sessionStorage.removeItem('gree.sessionToken')}catch{}
        token=null;
        if(!pairDialog.open)pairDialog.showModal();
        connection.textContent='Pareie este iPhone novamente';
      }
      return null;
    }
    return response.json();
  }).then(info=>{
    if(!info)return;
    $('#lanAddress').textContent=info.lanUrl;
    $('#lanPairCode').textContent=info.pin||'';
    if(info.admin)setupAccess();
  }).catch(()=>{});
  function setupAccess(){
    const panel=$('#lanManagement'),toggle=$('#lanEnabled'),sessionList=$('#lanSessions'),status=$('#lanStatus');
    panel.hidden=false;
    async function update(body){
      toggle.disabled=true;
      try{
        const access=await window.greeLocalRequest('/api/access',body);
        toggle.checked=access.enabled;
        $('#lanPairCode').hidden=!access.enabled;
        $('#lanAddress').hidden=!access.enabled;
        sessionList.replaceChildren();
        for(const session of access.sessions){
          const button=document.createElement('button');
          button.className='settings-action';
          button.type='button';
          button.textContent='Revogar pareamento de '+new Date(session.createdAt).toLocaleString('pt-BR',{dateStyle:'short',timeStyle:'short'});
          button.onclick=async()=>{button.disabled=true;await update({revoke:session.id});button.disabled=false};
          sessionList.append(button);
        }
        status.textContent=access.enabled?'Acesso pela rede ativado.':'Acesso pela rede desativado.';
        return true;
      }catch(error){status.textContent=error.message;return false}
      finally{toggle.disabled=false}
    }
    toggle.onchange=async()=>{
      const checked=toggle.checked;
      if(!await update({enabled:checked}))toggle.checked=!checked;
    };
    void update();
  }
  function apply(result){
    state=result.state;
    st.connected=true;
    st.ip=result.ip||'';
    st.on=state.Pow===1;
    st.t=state.SetTem;
    st.confirmedT=state.SetTem;
    st.m=Math.max(0,modesWire.indexOf(state.Mod));
    st.f=state.WdSpd===0?0:state.WdSpd===1?1:state.WdSpd<=3?2:3;
    st.vane=Math.max(0,vanesWire.indexOf(state.SwUpDn));
    $('#vaneT').textContent=labels[st.vane];
    $('.chip[data-t="eco"]').classList.toggle('on',state.SvSt===1);
    $('.chip[data-t="sono"]').classList.toggle('on',state.SwhSlp===1);
    $('.chip[data-t="aletas"]').classList.toggle('on',st.vane>0);
    render();
    window.dispatchEvent(new CustomEvent('gree:state',{detail:{connected:true,on:st.on,temp:st.t,mode:st.m,fan:st.f,ip:st.ip}}));
    rememberIP(result.ip);
    connection.textContent='Gree conectado · '+result.ip;
    connection.title='Conectado ao aparelho Gree em '+result.ip;
  }
  function updateControls(){
    // Keep controls visually steady during network round-trips; dim only when disconnected.
    for(const button of controls)button.disabled=!state;
    const assistantApply=$('#applyRecommendation');if(assistantApply&&!assistantApply.dataset.sending)assistantApply.disabled=!state;
    connection.disabled=false;
    for(const [selector,key] of [['.chip[data-t="eco"]','SvSt'],['.chip[data-t="sono"]','SwhSlp'],['.chip[data-t="aletas"]','SwUpDn']]){
      if(state?.[key]==null)$(selector).disabled=true;
    }
    if(!state){
      $('#sT').textContent='-';
      $('#sL').textContent='Desconectado';
    }
  }
  function scheduleReconnect(){
    if(reconnectTimer||state||!token)return;
    connection.textContent='Sem conexao · nova tentativa em '+Math.round(retryDelay/1000)+'s';
    reconnectTimer=setTimeout(()=>{
      reconnectTimer=0;
      retryDelay=Math.min(20000,Math.round(retryDelay*1.35));
      void connectAutomatically();
    },retryDelay);
  }
  function run(input){
    if(pending&&input.action==='status')return Promise.resolve(null);
    pending++;
    const request=requests.then(()=>send(input)).finally(()=>{
      pending--;
      if(!pending&&!state&&!connecting)scheduleReconnect();
    });
    requests=request.catch(()=>{});
    return request;
  }
  async function send(input){
    if(input.action==='command'&&!state)return null;
    try{
      const response=await fetch('/api',{method:'POST',headers:{'x-gree-token':token,'content-type':'application/json'},body:JSON.stringify(input)});
      const result=await response.json();
      if(response.status===409){message.textContent=result.error;return null}
      if(!response.ok)throw new Error(result.error||'Falha na comunicacao com o Gree');
      apply(result);
      message.textContent=result.outcome?.ok===false?result.outcome.message:'';
      if(input.action==='command'&&message.textContent){connection.textContent=message.textContent;connection.title=message.textContent}
      if(input.action==='connect')dialog.close();
      return result;
    }catch(error){
      message.textContent=error.message;
      connection.title=error.message;
      connection.textContent=input.action==='connect'?'Nao conectado · tentando novamente':'Conexao perdida';
      state=null;
      st.on=false;
      st.connected=false;
      st.ip='';
      window.dispatchEvent(new CustomEvent('gree:state',{detail:{connected:false}}));
      if(input.action==='command'&&input.name==='SetTem')st.t=st.confirmedT;
      st.tempDragging=false;
      render();
      return null;
    }finally{updateControls();}
  }
  async function connectAutomatically(){
    if(!token){connection.textContent='Abra pelo arquivo .bat para conectar';return}
    if(state||pending||connecting)return;
    connecting=true;
    try{
      const saved=rememberedIP();
      if(saved){
        connection.textContent='Tentando o ultimo IP salvo...';
        let result=await run({action:'connect',ip:saved});
        if(result){retryDelay=5000;return}
      }
      connection.textContent='Procurando o Gree em toda a rede Wi-Fi...';
      const result=await run({action:'connect'});
      if(result){
        retryDelay=5000;
        if(reconnectTimer){clearTimeout(reconnectTimer);reconnectTimer=0}
        return;
      }
    }finally{
      connecting=false;
      if(!state)scheduleReconnect();
    }
  }
  function command(name,value){if(state)return run({action:'command',name,value});return Promise.resolve(null)}
  async function applyPlan(plan){
    if(!state)return {ok:false,message:'O Gree está desconectado; não enviei comandos.'};
    const result=await run({...plan,action:'plan',condition:plan.action,mode:plan.mode||'Frio',temp:Number(plan.temp)||23});
    return result?.outcome||{ok:false,message:'Não consegui confirmar os ajustes com o Gree.'};
  }
  window.greeActions={
    getState:()=>({...st}),
    applyPlan,
    applyRecommendation:recommendation=>applyPlan({on:true,mode:recommendation.mode,temp:recommendation.temp}),
    togglePower:()=>command('Pow',st.on?0:1),
    refreshStatus:()=>state?(pending?Promise.resolve(null):run({action:'status'})):connectAutomatically()
  };
  function open(){
    if(!dialog.open)dialog.showModal();
    if(!token)message.textContent='Abra pelo arquivo .bat para ativar a conexao.';
  }
  connection.onclick=open;
  $('#closeConnection').onclick=()=>dialog.close();
  $('#connectForm').onsubmit=e=>{
    e.preventDefault();
    if(!token)return;
    if(reconnectTimer){clearTimeout(reconnectTimer);reconnectTimer=0}
    message.textContent='Procurando o aparelho.';
    void (async()=>{
      const result=await run({action:'connect',ip:ipInput.value.trim()||undefined});
      if(!result)scheduleReconnect();
    })();
  };
  $('#pw').onclick=()=>command('Pow',st.on?0:1);
  const tempSlider=$('#tempSlider');
  tempSlider.oninput=()=>{
    st.tempDragging=true;
    st.t=Number(tempSlider.value);
    tempSlider.style.setProperty('--temp-progress',`${((st.t-16)/14)*100}%`);
    render();
  };
  tempSlider.onchange=()=>{
    st.tempDragging=false;
    st.t=Number(tempSlider.value);
    void command('SetTem',st.t);
  };
  tempSlider.onpointercancel=()=>{st.tempDragging=false;st.t=st.confirmedT;render()};
  $$('.mode').forEach(b=>b.onclick=()=>command('Mod',modesWire[+b.dataset.m]));
  const fanSlider=$('#fanSlider');
  fanSlider.oninput=()=>{st.f=Number(fanSlider.value);render();fanSlider.style.setProperty('--fan-progress',`${st.f*100/3}%`)};
  fanSlider.onchange=()=>command('WdSpd',fansWire[Number(fanSlider.value)]);
  const fan=()=>command('WdSpd',fansWire[(st.f+1)%fansWire.length]);
  $('#velBtn').onclick=$('.fan .v').onclick=fan;
  $('.chip[data-t="eco"]').onclick=()=>{if(state)command('SvSt',state.SvSt===1?0:1)};
  $('.chip[data-t="sono"]').onclick=()=>{if(state)command('SwhSlp',state.SwhSlp===1?0:1)};
  $('.chip[data-t="aletas"]').onclick=()=>{if(state)command('SwUpDn',vanesWire[(st.vane+1)%vanesWire.length])};
  $('#openConnectionSettings').onclick=open;
  setTimeout(()=>{updateControls();void connectAutomatically()},150);
  setInterval(()=>{if(state&&!document.hidden)void run({action:'status'})},10000);
}
