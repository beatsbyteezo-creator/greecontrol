// inicia a interface (render() está em app.js; o motor 3D em engine.js)
render();
document.querySelectorAll('.nav button[data-view]').forEach(button=>button.addEventListener('click',()=>window.dispatchEvent(new Event('gree:view'))));
