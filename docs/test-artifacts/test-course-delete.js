(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));

  const hist = byText('button', 'Voir les articles achet');
  if (hist) { hist.click(); await sleep(1200); }
  const before = text();
  const del = byText('button', 'Supprimer');
  if (del) { del.click(); await sleep(1500); }
  const afterHist = text();
  const back = byText('button', 'Retour');
  if (back) { back.click(); await sleep(800); }
  return {
    beforeHasLait: before.includes('Lait test emulator'),
    afterHistHasLait: afterHist.includes('Lait test emulator'),
    listHasLait: text().includes('Lait test emulator'),
    afterSnippet: text().slice(0, 700)
  };
})()
