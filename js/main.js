// inicia a interface (render() está em app.js; o motor 3D em engine.js)
render();
document.querySelectorAll('.nav button[data-view]').forEach(button=>button.addEventListener('click',()=>window.dispatchEvent(new Event('gree:view'))));

let updateNoticeTimer;
window.greeUpdateNotice = (message, actionLabel = null, persistent = false) => {
  const notice = document.getElementById('updateNotice');
  if (!notice) return;
  const messageNode = document.getElementById('updateNoticeMessage');
  const action = document.getElementById('updateNoticeAction');
  messageNode.textContent = message;
  action.hidden = !actionLabel;
  action.disabled = actionLabel === 'Baixando…';
  action.textContent = actionLabel || '';
  notice.hidden = false;
  notice.classList.remove('show');
  requestAnimationFrame(() => notice.classList.add('show'));
  clearTimeout(updateNoticeTimer);
  if (!persistent) updateNoticeTimer = setTimeout(() => {
    notice.classList.remove('show');
    setTimeout(() => { notice.hidden = true; }, 260);
  }, 7000);
};
document.getElementById('updateNoticeAction')?.addEventListener('click', () => {
  window.chrome?.webview?.postMessage('install-update');
});
