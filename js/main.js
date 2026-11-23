// inicia a interface (render() está em app.js; o motor 3D em engine.js)
render();
document.querySelectorAll('.nav button[data-view]').forEach(button=>button.addEventListener('click',()=>window.dispatchEvent(new Event('gree:view'))));

let updateNoticeTimer;
window.greeUpdateNotice = message => {
  const notice = document.getElementById('updateNotice');
  if (!notice) return;
  notice.textContent = message;
  notice.hidden = false;
  notice.classList.remove('show');
  requestAnimationFrame(() => notice.classList.add('show'));
  clearTimeout(updateNoticeTimer);
  updateNoticeTimer = setTimeout(() => {
    notice.classList.remove('show');
    setTimeout(() => { notice.hidden = true; }, 260);
  }, 7000);
};
