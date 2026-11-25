const $=s=>document.querySelector(s),$$=s=>[...document.querySelectorAll(s)];
const st={t:22,m:0,f:0,on:true,vane:0,connected:false,ip:"",tempDragging:false,confirmedT:22};
window.greeState=st;
const modes=[
 {l:"Resfriando",i:"snow",c:"#3b82f6",c2:"#60a5fa"},
 {l:"Aquecendo",i:"sun",c:"#f59e0b",c2:"#fbbf24"},
 {l:"Desumidificando",i:"drop",c:"#14b8a6",c2:"#2dd4bf"},
 {l:"Ventilando",i:"fan",c:"#8b9bb8",c2:"#b6c3da"},
 {l:"Automático",i:"auto",c:"#8b5cf6",c2:"#a78bfa"}];
const fans=["Auto","Baixo","Médio","Alto"];
function render(){
  const m=modes[st.m],r=document.documentElement.style;
  r.setProperty("--ac",m.c);r.setProperty("--ac2",m.c2);
  const app=$("#app");
  app.classList.toggle("off",!st.on);
  app.dataset.on=String(st.on);app.dataset.mode=String(st.m);
  app.dataset.sleep=$('.chip[data-t="sono"]').classList.contains("on");
  app.dataset.eco=$('.chip[data-t="eco"]').classList.contains("on");
  app.dataset.vane=$('.chip[data-t="aletas"]').classList.contains("on");
  setTemperatureDisplay(st.t);
  const tempSlider=$("#tempSlider");
  if(tempSlider){if(!st.tempDragging)tempSlider.value=st.t;tempSlider.style.setProperty('--temp-progress',`${((st.t-16)/14)*100}%`)}
  $("#sL").textContent=st.on?m.l:"Desligado";
  $("#sIco use").setAttribute("href","#"+m.i);
  $$(".mode").forEach(b=>b.classList.toggle("on",+b.dataset.m===st.m));
  const fanSlider=$("#fanSlider");
  if(fanSlider){fanSlider.value=st.f;fanSlider.style.setProperty('--fan-progress',`${st.f*100/3}%`);updateFanSegments(st.f)}
  app.style.setProperty('--fan-duration',["2.4s","2.1s","1.45s",".9s"][st.f]);
  $("#fT").textContent=$("#velT").textContent=fans[st.f];
  $("#detailPower").textContent=st.connected?(st.on?"Ligado":"Desligado"):"Indisponível";
  $("#detailTemp").textContent=st.connected?`${st.t} °C`:"Indisponível";
  $("#detailMode").textContent=st.connected?m.l:"Indisponível";
  $("#detailFan").textContent=st.connected?fans[st.f]:"Indisponível";
  $("#detailIP").textContent=st.connected?(st.ip||"Conectado na rede local"):"Aguardando conexão";
  $("#settingsConnection").textContent=st.connected?`Conectado · ${st.ip||"rede local"}`:"Buscando na rede local";
}
function setTemperatureDisplay(value){
  const current=$("#sT");
  if(!current)return;
  const next=String(value),previous=current.textContent;
  if(previous===next)return;
  const window=current.parentElement;
  const direction=Number(value)>Number(previous)?"up":"down";
  const outgoing=document.createElement("span");
  outgoing.className="temperature-value outgoing";
  outgoing.textContent=previous;
  const incoming=document.createElement("span");
  incoming.className="temperature-value incoming";
  incoming.id="sT";
  incoming.textContent=next;
  window.dataset.direction=direction;
  window.replaceChildren(outgoing,incoming);
  requestAnimationFrame(()=>window.classList.add("rolling"));
  setTimeout(()=>{
    if(!window.contains(outgoing))return;
    incoming.className="temperature-value";
    window.replaceChildren(incoming);
    window.classList.remove("rolling");
  },230);
}
function updateFanSegments(value){
  const segments=$$(".fan-segments i");
  const lit=value+1;
  segments.forEach((segment,index)=>segment.classList.toggle("lit",index<lit));
}
$("#tempSlider").oninput=e=>{st.tempDragging=true;st.t=Number(e.target.value);render()};
$("#tempSlider").onchange=e=>{st.tempDragging=false;st.t=Number(e.target.value);render()};
$$(".mode").forEach(b=>b.onclick=()=>{st.m=+b.dataset.m;st.on=true;render()});
$("#fanSlider").oninput=e=>{st.f=Number(e.target.value);render()};
$("#velBtn").onclick=()=>{st.f=(st.f+1)%4;render()};
$(".fan .v").onclick=()=>{st.f=(st.f+1)%4;render()};
$("#pw").onclick=()=>{st.on=!st.on;render()};
$$(".chip").forEach(c=>c.onclick=()=>c.classList.toggle("on"));
{const vc=$('.chip[data-t="aletas"]');vc.onclick=()=>{st.vane=(st.vane+1)%5;vc.classList.toggle("on",st.vane>0);$("#vaneT").textContent=["Auto","Balanço","Alto","Médio","Baixo"][st.vane]}}
$$('.nav button').forEach(button=>button.onclick=()=>{
  const view=button.dataset.view;
  $('#app').dataset.view=view;
  $$('.nav button').forEach(item=>item.classList.toggle('on',item===button));
});
