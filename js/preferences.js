(()=>{
 const root=document.querySelector('.comfort-profiles');if(!root)return;
 const key='gree.comfortProfiles.v1',defaults={sono:{mode:'Frio',temp:24},trabalho:{mode:'Auto',temp:23},relaxar:{mode:'Frio',temp:24}};
 let profiles={...defaults};try{profiles={...profiles,...JSON.parse(localStorage.getItem(key)||'{}')}}catch{}
 const save=()=>{try{for(const row of root.querySelectorAll('[data-profile]')){const name=row.dataset.profile;profiles[name]={mode:row.querySelector('select').value,temp:Math.max(16,Math.min(30,Number(row.querySelector('input').value)||23))} }localStorage.setItem(key,JSON.stringify(profiles))}catch{}};
 for(const row of root.querySelectorAll('[data-profile]')){const item=profiles[row.dataset.profile]||defaults[row.dataset.profile];row.querySelector('select').value=item.mode;row.querySelector('input').value=item.temp;row.querySelectorAll('select,input').forEach(control=>control.addEventListener('change',save));row.querySelector('.profile-apply').addEventListener('click',async()=>{save();const button=row.querySelector('.profile-apply'),old=button.textContent;button.disabled=true;button.textContent='Enviando...';const result=await window.greeActions?.applyPlan({on:true,...profiles[row.dataset.profile]});button.textContent=result?.ok?'Aplicado':result?.message||'Indisponivel';setTimeout(()=>{button.disabled=false;button.textContent=old},1800)})}
 window.greePresetApply=async name=>{const item=profiles[name]||defaults[name];if(!item)return {ok:false};return window.greeActions?.applyPlan({on:true,...item})};
})();
