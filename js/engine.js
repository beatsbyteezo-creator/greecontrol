(()=>{
const cv=$("#cv"),cx=cv.getContext("2d"),reduce=matchMedia("(prefers-reduced-motion:reduce)").matches;
const fanIcons=$$(".fan-icon"),statusIcon=$("#sIco");let motorRps=0,motorAngle=0;
// aparelho com proporção real (~90x30x20 cm): perfil curvo extrudado ao longo de x
const UW=330,hw=UW/2,UH=112,hh=UH/2,hd=39;
const PR=[[-hh,hd],[-hh,-hd+14],[-hh+8,-hd+3],[-hh+11,-hd],[hh-30,-hd],[hh-8,-hd+11],[hh,-hd+30],[hh,hd]];
const ry=-.2,rx=-.3,rl=-.05,F=1100,cy=Math.cos(ry),sy=Math.sin(ry),cxr=Math.cos(rx),sxr=Math.sin(rx),cl=Math.cos(rl),sl=Math.sin(rl),CAM=-(F+300);
let W=430,H=340,k=1,d=1,spr=null,sk="",t=0,acc=0;const ps=[];let lv=.5;const chip=n=>$$(".chip")[n].classList.contains("on");
const cfg=()=>{const m=st.m,sono=chip(1),eco=chip(0),f=st.f===0?(m===3?2.5:1.4+(30-st.t)/14*2.6):st.f+1;return{f,mul:[1,.9,.7,1.15,.85][m]*(sono?.5:eco?.8:1),tgt:!st.on?0:st.vane===1?.85+.4*Math.sin(t*.32):st.vane>1?[.4,.8,1.25][st.vane-2]:[.5,1.2,.8,.9,.75][m]-(sono?.15:0)+(m>=3?.25*Math.sin(t*(m===3?.8:.5)):0)}};
function size(){d=Math.min(devicePixelRatio||1,2);W=cv.clientWidth||430;H=cv.clientHeight||340;cv.width=W*d;cv.height=H*d;k=W/430}
function R(x,y,z){let X=x*cy+z*sy,Z=-x*sy+z*cy;const Y=y*cxr-Z*sxr;Z=y*sxr+Z*cxr;return[X*cl-Y*sl,X*sl+Y*cl,Z]}
function J(q){const s=F/(F+q[2]+300)*k*1.15;return[W/2+q[0]*s,H*.38+q[1]*s,s]}
const P=(x,y,z)=>J(R(x,y,z)),sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]],dot=(a,b)=>a[0]*b[0]+a[1]*b[1]+a[2]*b[2];
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const LV=(()=>{const v=[-.4,-.55,-.7],n=Math.hypot(...v);return v.map(a=>a/n)})();
const faces=[];
function addFace(pts,hint){
 const Q=pts.map(p=>R(...p)),c=Q.reduce((a,q)=>[a[0]+q[0],a[1]+q[1],a[2]+q[2]],[0,0,0]).map(a=>a/Q.length);
 let n=cross(sub(Q[1],Q[0]),sub(Q[Q.length-1],Q[0]));if(dot(n,c)<0)n=n.map(a=>-a);
 if(dot(n,sub(c,[0,0,CAM]))>=0)return;
 const m=Math.hypot(...n),g=Math.round(46+175*Math.max(0,dot(n,LV)/m));
 faces.push({pts,g,z:c[2]});
}
for(let i=0;i<7;i++){const a=PR[i],b=PR[i+1];addFace([[-hw,a[0],a[1]],[hw,a[0],a[1]],[hw,b[0],b[1]],[-hw,b[0],b[1]]])}
addFace(PR.map(p=>[-hw,p[0],p[1]]));
faces.sort((a,b)=>b.z-a.z);
const path=a=>{cx.beginPath();a.forEach((p,i)=>i?cx.lineTo(p[0],p[1]):cx.moveTo(p[0],p[1]));cx.closePath()};
const col=()=>{const h=modes[st.m].c2;return[1,3,5].map(i=>parseInt(h.slice(i,i+2),16))};
const fan=()=>st.f===0?2.5:st.f+1;
function drawUnit(){
 const c=col(),y3=PR[3][0],z3=PR[3][1],y4=PR[4][0],z4=PR[4][1],ph=y4-y3;
 const TL=P(-hw,y3,z3),TR=P(hw,y3,z3),BR=P(hw,y4,z4),BL=P(-hw,y4,z4);
 // luz do aparelho na parede e sombra projetada
 let g=cx.createRadialGradient(W/2,110*k,10,W/2,110*k,230*k);g.addColorStop(0,`rgba(${c},.10)`);g.addColorStop(1,`rgba(${c},0)`);cx.fillStyle=g;cx.fillRect(0,0,W,H);
 cx.save();cx.shadowColor="rgba(0,0,0,.6)";cx.shadowBlur=34*d;cx.shadowOffsetY=24*d;cx.fillStyle="#000";path([TL,TR,BR,BL]);cx.fill();cx.restore();
 for(const f of faces){
  const pp=f.pts.map(p=>P(...p));cx.fillStyle=`rgb(${f.g},${f.g+3},${f.g+9})`;path(pp);cx.fill();
  g=cx.createLinearGradient(pp[0][0],pp[0][1],pp[1][0],pp[1][1]);g.addColorStop(0,"rgba(255,255,255,.12)");g.addColorStop(1,"rgba(0,0,0,.16)");cx.fillStyle=g;path(pp);cx.fill();
 }
 // saída de ar na parte de baixo
 const A=P(-hw,hh,-hd+30),B=P(hw,hh,-hd+30),C=P(hw,hh,hd),D=P(-hw,hh,hd);
 const bl=(u,v)=>[0,1].map(i=>(1-v)*((1-u)*A[i]+u*B[i])+v*((1-u)*D[i]+u*C[i]));
 const o=[bl(.03,.06),bl(.97,.06),bl(.97,.5),bl(.03,.5)];cx.fillStyle="#05070b";path(o);cx.fill();
 if(st.on){cx.fillStyle=`rgba(${c},.35)`;path(o);cx.fill()}
 for(let i=1;i<=3;i++){const v=.06+.44*i/4,a=bl(.03,v),b=bl(.97,v);cx.strokeStyle="rgba(205,212,228,.4)";cx.lineWidth=1.2*k;cx.beginPath();cx.moveTo(a[0],a[1]);cx.lineTo(b[0],b[1]);cx.stroke()}
 const hz=hd-6,L=40,tz=hz-L*Math.cos(lv),ty=hh+L*Math.sin(lv),v=[P(-hw*.97,hh,hz),P(hw*.97,hh,hz),P(hw*.97,ty,tz),P(-hw*.97,ty,tz)],gv=Math.round(150+60*Math.cos(lv)-30*Math.sin(lv));
 cx.fillStyle=`rgb(${gv},${gv+3},${gv+9})`;path(v);cx.fill();
 g=cx.createLinearGradient(v[0][0],v[0][1],v[1][0],v[1][1]);g.addColorStop(0,"rgba(255,255,255,.12)");g.addColorStop(1,"rgba(0,0,0,.18)");cx.fillStyle=g;path(v);cx.fill();
 cx.strokeStyle="rgba(255,255,255,.55)";cx.lineWidth=1.4*k;cx.beginPath();cx.moveTo(v[2][0],v[2][1]);cx.lineTo(v[3][0],v[3][1]);cx.stroke();
 // painel frontal: logo e display (mapeamento afim)
 cx.save();path([TL,TR,BR,BL]);cx.clip();
 cx.transform((TR[0]-TL[0])/UW,(TR[1]-TL[1])/UW,(BL[0]-TL[0])/ph,(BL[1]-TL[1])/ph,TL[0],TL[1]);
 g=cx.createLinearGradient(0,0,0,ph);g.addColorStop(0,"rgba(255,255,255,.2)");g.addColorStop(.7,"rgba(0,0,0,0)");g.addColorStop(1,"rgba(0,0,0,.22)");cx.fillStyle=g;cx.fillRect(0,0,UW,ph);
 cx.fillStyle="rgba(0,0,0,.18)";cx.fillRect(0,ph*.84,UW,1.4);
 cx.textAlign="center";cx.font="italic 800 11px sans-serif";cx.fillStyle="#454852";cx.fillText("GREE",UW*.5,ph*.52);
 if(st.on){cx.textAlign="right";cx.font="600 20px 'Segoe UI',system-ui,sans-serif";cx.shadowColor=`rgb(${c})`;cx.shadowBlur=9*d;cx.fillStyle="#e4f0ff";cx.fillText(st.t+"°",UW-24,ph*.42)}
 cx.restore();
}
function spawn(c){
 const sp=(85+c.f*20)*c.mul,u=Math.random(),x=(u-.5)*UW*.92,r=()=>(Math.random()-.5)*.14;
 const a=Math.PI/2-lv+r(),dx=(u-.5)*.3+r(),dy=Math.cos(a),dz=-Math.sin(a)+r(),n=Math.hypot(dx,dy,dz);
 ps.push({x,y:hh+40*Math.sin(lv),z:hd-6-40*Math.cos(lv),vx:dx/n*sp,vy:dy/n*sp,vz:dz/n*sp,age:0,a:0,dist:0,tex:(Math.random()*6)|0,rot:Math.random()*.6-.3,life:2.8+Math.random()*1.2});
}
// campo de vento suave e coerente: todas as partículas próximas são carregadas juntas
function wind(x,y,z){return[Math.sin(y*.011+t*.9)*Math.cos(z*.009+t*.5)+Math.sin(z*.016-t*.7)*.5,Math.sin(x*.009+t*.6)*.4,Math.sin(x*.012+y*.01-t*.8)*Math.cos(y*.01+t*.4)]}
function step(p,dt){
 const m=st.m,g=m===0?75:m===1?-100:m===2?40:m===4?45:0,kd=.0046,sp=Math.hypot(p.vx,p.vy,p.vz),amp=Math.min(1,p.age*.7)*36,w=wind(p.x,p.y,p.z),mix=1-.3*dt;
 p.vx=(p.vx+(-kd*sp*p.vx+w[0]*amp)*dt)*mix;p.vy=(p.vy+(-kd*sp*p.vy+g+w[1]*amp)*dt)*mix;p.vz=(p.vz+(-kd*sp*p.vz+w[2]*amp)*dt)*mix;
 p.x+=p.vx*dt;p.y+=p.vy*dt;p.z+=p.vz*dt;p.dist+=sp*dt;p.age+=dt;p.a=p.age/p.life;
}
function sprites(c){const key=c.map(v=>Math.round(v+(255-v)*.25)).join();if(key===sk)return spr;sk=key;spr=[];
 for(let n=0;n<6;n++){const e=document.createElement("canvas");e.width=e.height=96;const s=e.getContext("2d");
  for(let i=0;i<16;i++){const a=Math.random()*6.283,rr=Math.random()*30,x=48+Math.cos(a)*rr,y=48+Math.sin(a)*rr*.8,r=8+Math.random()*16,g=s.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,`rgba(${key},${.18+Math.random()*.25})`);g.addColorStop(1,`rgba(${key},0)`);s.fillStyle=g;s.fillRect(0,0,96,96)}
  s.globalCompositeOperation="destination-in";const m=s.createRadialGradient(48,48,0,48,48,48);m.addColorStop(0,"#000");m.addColorStop(.6,"rgba(0,0,0,.6)");m.addColorStop(1,"rgba(0,0,0,0)");s.fillStyle=m;s.fillRect(0,0,96,96);spr.push(e)}
 return spr}
function update(dt){
 t+=dt;const c=cfg();lv+=(c.tgt-lv)*Math.min(1,dt*1.4);if(st.on&&lv>.12){acc+=dt*(90+c.f*16)*c.mul*(1+(30-st.t)*.01);while(acc>=1){acc--;if(ps.length<700)spawn(c)}}
 for(const p of ps)step(p,dt);
 for(let i=ps.length-1;i>=0;i--)if(ps[i].a>=1)ps.splice(i,1);
}
function frame(dt){
 const targetRps=st.on?[.52,.78,1.2,1.72][st.f]||.52:0;
 const coast=targetRps>motorRps?.72:1.05;
 motorRps+=(targetRps-motorRps)*(1-Math.exp(-dt/coast));
 if(!st.on&&motorRps<.004)motorRps=0;
 motorAngle=(motorAngle+motorRps*360*dt)%360;
 const spin=`rotate(${motorAngle}deg)`;
 // Inline !important keeps the motor's physical spin from being overwritten by
 // reduced-motion/theme rules. The same eased motor speed drives every fan glyph.
 fanIcons.forEach(icon=>icon.style.setProperty("transform",spin,"important"));
 statusIcon.style.setProperty("transform",st.m===3?spin:"","important");
 update(dt);cx.setTransform(d,0,0,d,0,0);cx.clearRect(0,0,W,H);drawUnit();
 const sps=sprites(col());cx.globalCompositeOperation="lighter";
 for(const p of ps){
  const q=P(p.x,p.y,p.z),q2=P(p.x+p.vx*.04,p.y+p.vy*.04,p.z+p.vz*.04),ang=Math.atan2(q2[1]-q[1],q2[0]-q[0]),v=Math.hypot(p.vx,p.vy,p.vz),r=(8+p.dist*.24)*q[2];
  const al=Math.min(1,p.a*10)*Math.pow(1-p.a,1.6)/(1+p.dist*.008),sx=r*(1+Math.min(1.2,v/110)),co=Math.cos(ang+p.rot)*d,si=Math.sin(ang+p.rot)*d;
  cx.globalAlpha=al*.13;cx.setTransform(co,si,-si,co,q[0]*d,q[1]*d);cx.drawImage(sps[p.tex],-sx,-r,sx*2,r*2);
 }
 cx.setTransform(d,0,0,d,0,0);cx.globalAlpha=1;cx.globalCompositeOperation="source-over";
}
size();addEventListener("resize",size);
for(let i=0;i<190;i++)update(1/60);
// The fan has a mechanical spin-up/coast-down, so keep its small animation loop
// alive even when the OS requests reduced motion. Other interface transitions
// still honor the user's reduced-motion preference.
{let last=0;const loop=ts=>{const dt=Math.min(.033,(ts-last)/1000||.016);last=ts;frame(dt);requestAnimationFrame(loop)};if(reduce)frame(0);requestAnimationFrame(loop)}
})();
