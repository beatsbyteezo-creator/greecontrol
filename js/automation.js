(()=>{
  const $=selector=>document.querySelector(selector);
  const form=$('#routineForm'),list=$('#routineList'),type=$('#routineType');
  if(!form||!list)return;

  const notice=$('#routineStatus');
  const legacyKey='gree.routines.v1';
  const attempts=new Map();
  let routines=[],snapshot='',running=false,initialized=false;
  const actionText={on:'ligar e ajustar',adjust:'ajustar se ligado',off:'desligar'};

  function describe(routine){
    const trigger=routine.type==='time'?`Todos os dias às ${routine.time}`:
      `Quando fora ${routine.type==='hot'?'≥':'≤'} ${routine.threshold}°C`;
    const setting=routine.action==='off'?'':` · ${routine.mode} ${routine.temp}°C`;
    return `${trigger} · ${actionText[routine.action]}${setting}`;
  }

  function draw(){
    list.replaceChildren();
    if(!routines.length){
      const empty=document.createElement('div');
      empty.className='routine-empty';
      empty.textContent='Nenhuma rotina configurada.';
      list.append(empty);
      return;
    }
    for(const routine of routines){
      const row=document.createElement('article');
      row.className='routine-row';
      const toggle=document.createElement('input');
      toggle.type='checkbox';
      toggle.checked=routine.enabled;
      toggle.setAttribute('aria-label','Ativar rotina');
      toggle.onchange=async()=>{
        const checked=toggle.checked;
        toggle.disabled=true;
        if(!await change({action:'toggle',id:routine.id,enabled:checked}))toggle.checked=!checked;
        toggle.disabled=false;
      };
      const copy=document.createElement('div');
      copy.className='routine-copy';
      const title=document.createElement('strong');
      title.textContent=describe(routine);
      copy.append(title);
      if(routine.lastRunAt){
        const date=document.createElement('small');
        date.textContent='Última execução em '+new Date(routine.lastRunAt).toLocaleString('pt-BR',{dateStyle:'short',timeStyle:'short'});
        copy.append(date);
      }
      if(routine.lastResult){
        const outcome=document.createElement('small');
        outcome.textContent=routine.lastResult;
        copy.append(outcome);
      }
      const remove=document.createElement('button');
      remove.type='button';
      remove.className='routine-remove';
      remove.textContent='Remover';
      remove.onclick=async()=>{
        remove.disabled=true;
        await change({action:'remove',id:routine.id});
        remove.disabled=false;
      };
      row.append(toggle,copy,remove);
      list.append(row);
    }
  }

  function accept(data){
    const next=JSON.stringify(data);
    if(next===snapshot)return;
    snapshot=next;
    routines=data;
    draw();
  }

  async function change(body){
    try{
      accept(await window.greeLocalRequest('/api/routines',body));
      notice.textContent='';
      return true;
    }catch(error){
      notice.textContent=error.message;
      return false;
    }
  }

  async function migrate(){
    let legacy=[];
    try{legacy=JSON.parse(localStorage.getItem(legacyKey)||'[]')}catch{}
    if(!Array.isArray(legacy))return true;
    for(const item of legacy){
      if(item?.id&&!await change({action:'add',routine:item}))return false;
    }
    try{localStorage.removeItem(legacyKey)}catch{}
    return true;
  }

  function updateFields(){
    const isTime=type.value==='time';
    $('#routineTimeLabel').hidden=$('#routineTime').hidden=!isTime;
    $('#routineTime').required=isTime;
    $('#routineThresholdLabel').hidden=$('#routineThresholdWrap').hidden=isTime;
    $('#routineThreshold').required=!isTime;
    $('#routineThresholdPrefix').textContent=type.value==='hot'?'≥':'≤';
    $('#routineThreshold').value=type.value==='cold'?'15':'29';
    $('#routineAction').value=isTime?'on':'adjust';
  }

  type.addEventListener('change',updateFields);
  updateFields();
  form.addEventListener('submit',async event=>{
    event.preventDefault();
    const submit=form.querySelector('button[type=submit]');
    submit.disabled=true;
    const routine={
      id:crypto.randomUUID?.()||Date.now().toString(36)+'-'+Math.random().toString(36).slice(2),
      type:type.value,time:$('#routineTime').value,threshold:Number($('#routineThreshold').value),
      action:$('#routineAction').value,mode:$('#routineMode').value,temp:Number($('#routineTemp').value),enabled:true
    };
    await change({action:'add',routine});
    submit.disabled=false;
  });

  async function tick(){
    if(running||!window.greeLocalRequest)return;
    running=true;
    try{
      accept(await window.greeLocalRequest('/api/routines'));
      if(!initialized)initialized=await migrate();
      if(!document.documentElement.hasAttribute('data-native-panel'))return;
      if(routines.some(routine=>routine.enabled&&routine.type!=='time'))window.greeAssistantBackgroundCheck?.(true);
      const weather=window.greeAssistantGetWeather?.();
      if(!window.greeActions?.getState().connected)return;
      for(const {id} of routines){
        const routine=routines.find(item=>item.id===id);
        if(routine?.enabled&&window.greeActions.getState().connected)await runRoutine(routine,weather);
      }
    }catch{}finally{running=false}
  }

  async function runRoutine(routine,weather){
    const now=new Date();
    const date=now.toLocaleDateString('sv-SE');
    const currentMinute=now.getHours()*60+now.getMinutes();
    let event=date;
    if(routine.type==='time'){
      const [hours,minutes]=routine.time.split(':').map(Number);
      const delay=currentMinute-(hours*60+minutes);
      if(routine.lastDay===date||delay<0||delay>=10)return;
    }else{
      const outside=weather?.temperature;
      if(!Number.isFinite(outside))return;
      const meetsThreshold=routine.type==='hot'?outside>=routine.threshold:outside<=routine.threshold;
      if(!meetsThreshold){
        attempts.delete(routine.id);
        if(routine.lastCondition)await change({action:'outcome',id:routine.id,outcome:{...routine,lastCondition:false}});
        return;
      }
      if(routine.lastCondition)return;
      event='weather';
    }
    const previous=attempts.get(routine.id);
    const count=previous?.event===event?previous.count:0;
    if(count>=3||(count&&Date.now()-previous.at<60000))return;
    attempts.set(routine.id,{event,count:count+1,at:Date.now()});

    const plan={action:routine.action,mode:routine.mode,temp:routine.temp,
      on:routine.action==='off'?false:routine.action==='on'?true:undefined};
    const result=await window.greeActions.applyPlan(plan);
    const outcome={...routine,lastResult:result.message};
    if(result.ok||result.skipped){
      if(result.ok)outcome.lastRunAt=Date.now();
      if(routine.type==='time')outcome.lastDay=date;
      else outcome.lastCondition=true;
    }
    await change({action:'outcome',id:routine.id,outcome});
  }

  window.greeAutomationTick=tick;
  window.addEventListener('gree:view',()=>void tick());
  setInterval(tick,30000);
  void tick();
})();
