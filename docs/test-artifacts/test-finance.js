(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));
  const setInput = (el, value) => {
    if (!el) return;
    el.focus();
    const proto = el.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, String(value)); else el.value = String(value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const go = async (label) => {
    const toggler = document.querySelector('input.navbar-toggler');
    if (toggler && !toggler.checked) toggler.click();
    await sleep(250);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1400);
  };
  const clickExact = (label) => {
    const b = Array.from(document.querySelectorAll('button')).find(x => x.textContent.trim() === label);
    if (b) b.click();
    return !!b;
  };

  await go('Finances');
  const homeBefore = text();
  const fp = document.querySelector('a[href="/finance/parametres"]');
  if (fp) { fp.click(); await sleep(1400); }

  // ensure epargne account
  setInput(document.querySelector('#compte-nom'), 'Livret test');
  const typeSel = document.querySelector('#compte-type');
  if (typeSel) {
    const opt = Array.from(typeSel.options || []).find(o => /pargne/i.test(o.textContent||''));
    if (opt) { typeSel.value = opt.value; typeSel.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  setInput(document.querySelector('#compte-solde'), '50');
  const form = document.querySelector('#compte-nom')?.closest('form');
  if (form) form.requestSubmit(); else clickExact('Ajouter');
  await sleep(1500);
  const paramsBody = text();

  await go('Finances');
  const home = text();
  const compte = Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Compte courant'));
  if (compte) { compte.click(); await sleep(1600); }
  const detail = { path: location.pathname, body: text().slice(0,800), links: Array.from(document.querySelectorAll('a,button')).slice(0,20).map(el => (el.textContent||'').trim().slice(0,50)) };

  // try nouvelle transaction
  const newTx = Array.from(document.querySelectorAll('a,button')).find(el => /nouvelle|Nouvelle transaction|Ajouter une/i.test(el.textContent||''));
  let txPage = null;
  if (newTx) {
    newTx.click(); await sleep(1600);
    txPage = { path: location.pathname, body: text().slice(0,700), fields: Array.from(document.querySelectorAll('input,select')).map(el => ({id:el.id,type:el.type,tag:el.tagName})) };
  }

  await go('Finances');
  const vir = document.querySelector('a[href="/finance/virement"]');
  if (vir) { vir.click(); await sleep(1400); }
  const virPage = { path: location.pathname, body: text().slice(0,900), fields: Array.from(document.querySelectorAll('input,select,button')).slice(0,25).map(el => ({id:el.id,type:el.type,text:(el.textContent||'').trim().slice(0,40)})) };

  // attempt virement if fields exist
  const montant = document.querySelector('#montant, #virement-montant, input[type=number]');
  let afterVir = null;
  if (montant) {
    setInput(montant, '20');
    const submits = Array.from(document.querySelectorAll('button')).filter(b => /virer|valider|enregistrer|effectuer/i.test(b.textContent||''));
    if (submits[0]) { submits[0].click(); await sleep(1800); afterVir = text().slice(0,800); }
  }

  // categories delete attempt
  await go('Finances');
  const p2 = document.querySelector('a[href="/finance/parametres"]');
  if (p2) { p2.click(); await sleep(1400); }
  // scroll categories - try delete on a user category if any
  const delCat = Array.from(document.querySelectorAll('button')).filter(b => b.textContent.trim() === 'Supprimer');
  let catDelete = { count: delCat.length };
  if (delCat.length > 1) {
    // skip first maybe compte - try last suppress in categories section
    delCat[delCat.length-1].click(); await sleep(1200);
    catDelete.after = text().slice(0,700);
    catDelete.hasReassign = /Non cat[eé]goris|réassign|utilise/i.test(text());
  }

  return { homeBefore: homeBefore.slice(0,300), paramsBody: paramsBody.slice(0,400), homeHasComptes: /Compte courant|Livret/.test(home), home: home.slice(0,500), detail, txPage, virPage, afterVir, catDelete };
})()
