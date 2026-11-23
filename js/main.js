// inicia a interface (render() está em app.js; o motor 3D em engine.js)
render();
document.querySelectorAll('.nav button[data-view]').forEach(button=>button.addEventListener('click',()=>window.dispatchEvent(new Event('gree:view'))));

let updateNoticeTimer;
window.greeUpdateNotice = (message, actionLabel = null, persistent = false) => {
  const notice = document.getElementById('updateNotice');
  if (!notice) return;
  const messageNode = document.getElementById('updateNoticeMessage');
  const action = document.getElementById('updateNoticeAction');
  const progress = document.getElementById('updateProgressBar');
  const progressLabel = document.getElementById('updateProgressLabel');
  const match = message.match(/(\d{1,3})%/);
  const percentage = match ? Math.min(100, Number(match[1])) : null;
  messageNode.textContent = message;
  action.hidden = !actionLabel;
  action.disabled = actionLabel !== 'Instalar e reiniciar';
  action.textContent = actionLabel || 'Instalar e reiniciar';
  progress.style.width = `${percentage ?? (actionLabel === 'Instalar e reiniciar' ? 100 : 8)}%`;
  progressLabel.textContent = actionLabel === 'Instalar e reiniciar'
    ? 'Download concluído e verificado'
    : percentage === null ? 'Baixando o instalador com segurança…' : `${percentage}% baixado`;
  notice.hidden = false;
  requestAnimationFrame(() => notice.classList.add('show'));
  clearTimeout(updateNoticeTimer);
  if (!persistent) updateNoticeTimer = setTimeout(() => {
    notice.classList.remove('show');
    setTimeout(() => { notice.hidden = true; }, 260);
  }, 9000);
};
document.getElementById('updateNoticeAction')?.addEventListener('click', () => {
  window.chrome?.webview?.postMessage('install-update');
});
document.getElementById('updateNoticeLater')?.addEventListener('click', () => {
  const notice = document.getElementById('updateNotice');
  notice.classList.remove('show');
  setTimeout(() => { notice.hidden = true; }, 220);
});
