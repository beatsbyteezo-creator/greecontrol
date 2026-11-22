(()=>{
 const canvas=document.querySelector('#historyCanvas'),empty=document.querySelector('#historyEmpty'),latest=document.querySelector('#historyLatest'),summary=document.querySelector('#historyRangeSummary');
 if(!canvas)return;
 const key='gree.history.v1',maxPoints=2500,maxAge=30*24*60*60*1000;
 let points=[],hours=24;
 try{points=JSON.parse(localStorage.getItem(key)||'[]').filter(p=>Number.isFinite(p.at)&&Number.isFinite(p.temp)&&Date.now()-p.at<=maxAge)}catch{points=[]}
 function persist(){try{localStorage.setItem(key,JSON.stringify(points.slice(-maxPoints)))}catch{}}
 function visible(){return points.filter(p=>Date.now()-p.at<=hours*3600000)}
 function resize(){const rect=canvas.getBoundingClientRect();if(!rect.width||!rect.height)return;const ratio=window.devicePixelRatio||1;canvas.width=Math.round(rect.width*ratio);canvas.height=Math.round(rect.height*ratio);const ctx=canvas.getContext('2d');ctx.setTransform(ratio,0,0,ratio,0,0);draw(ctx,rect.width,rect.height)}
 function draw(ctx,w,h){
  ctx.clearRect(0,0,w,h);const data=visible();empty.hidden=data.length>0;canvas.hidden=data.length===0;
  if(!data.length){latest.textContent='Aguardando dados reais do aparelho';summary.textContent='';return}
  const pad={l:29,r:8,t:10,b:20},cw=w-pad.l-pad.r,ch=h-pad.t-pad.b,lo=16,hi=30;
  ctx.font='9px Segoe UI, sans-serif';ctx.lineWidth=1;
  for(const v of [16,20,24,28,30]){const y=pad.t+(hi-v)/(hi-lo)*ch;ctx.strokeStyle='rgba(220,232,249,.10)';ctx.beginPath();ctx.moveTo(pad.l,y);ctx.lineTo(w-pad.r,y);ctx.stroke();ctx.fillStyle='rgba(213,226,245,.55)';ctx.fillText(v+'°',2,y+3)}
  const start=data[0].at,end=Math.max(data[data.length-1].at,start+1),x=t=>pad.l+(t-start)/(end-start)*cw,y=v=>pad.t+(hi-v)/(hi-lo)*ch;
  ctx.beginPath();data.forEach((p,i)=>i?ctx.lineTo(x(p.at),y(p.temp)):ctx.moveTo(x(p.at),y(p.temp)));ctx.strokeStyle='#73baff';ctx.lineWidth=2;ctx.lineJoin='round';ctx.lineCap='round';ctx.stroke();
  const last=data[data.length-1];ctx.beginPath();ctx.arc(x(last.at),y(last.temp),3.5,0,Math.PI*2);ctx.fillStyle='#d9f1ff';ctx.fill();
  ctx.fillStyle='rgba(213,226,245,.52)';ctx.textAlign='left';ctx.fillText(new Date(start).toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'}),pad.l,h-4);ctx.textAlign='right';ctx.fillText(new Date(end).toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'}),w-pad.r,h-4);ctx.textAlign='left';
  latest.textContent=`Ultima confirmacao: ${last.temp}°C · ${last.on?'ligado':'desligado'} · ${new Date(last.at).toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'})}`;
  summary.textContent=`${data.length} registros neste periodo`;
 }
 function record(state){if(!state?.connected||!Number.isFinite(state.temp))return;const now=Date.now(),last=points.at(-1),changed=!last||last.temp!==state.temp||last.on!==state.on||last.mode!==state.mode||last.fan!==state.fan;if(!changed&&now-last.at<5*60000)return;points.push({at:now,temp:state.temp,on:state.on,mode:state.mode,fan:state.fan});points=points.filter(p=>now-p.at<=maxAge).slice(-maxPoints);persist();resize()}
 document.querySelectorAll('[data-history-range]').forEach(button=>button.addEventListener('click',()=>{hours=Number(button.dataset.historyRange)||24;document.querySelectorAll('[data-history-range]').forEach(b=>b.classList.toggle('on',b===button));resize()}));
 window.addEventListener('gree:state',e=>record(e.detail));window.addEventListener('gree:view',resize);window.addEventListener('resize',resize);document.addEventListener('visibilitychange',resize);setTimeout(resize,250);
})();
