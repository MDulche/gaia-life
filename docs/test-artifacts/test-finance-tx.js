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
    const tg = document.querySelector('input.navbar-toggler');
    if (tg && !tg.checked) tg.click();
    await sleep(250);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1400);
  };

  await go('Finances');
  // dismiss error if any by going home first
  await go('Accueil');
  await go('Finances');
  const compte = Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Compte courant'));
  if (!compte) return { error: 'no compte', body: text().slice(0,400) };
  compte.click(); await sleep(1400);

  // Entrée
  byText('a', 'Ajouter une transaction')?.click(); await sleep(1200);
  setInput(document.querySelector('#tx-montant'), '40');
  document.querySelector('#type-entree')?.click();
  const cat = document.querySelector('#tx-categorie');
  if (cat) {
    const o = Array.from(cat.options||[]).find(x => /Non cat/i.test(x.textContent||''));
    if (o) { cat.value = o.value; cat.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  document.querySelector('#tx-montant')?.closest('form')?.requestSubmit();
  await sleep(1600);
  const afterIn = { path: location.pathname, body: text().slice(0,500), solde: (text().match(/Solde actuel\s*:\s*([^\n]+)/)||[])[1] };

  // Sortie
  byText('a', 'Ajouter une transaction')?.click(); await sleep(1200);
  setInput(document.querySelector('#tx-montant'), '15');
  document.querySelector('#type-sortie')?.click();
  const cat2 = document.querySelector('#tx-categorie');
  if (cat2) {
    const o = Array.from(cat2.options||[]).find(x => /Non cat/i.test(x.textContent||''));
    if (o) { cat2.value = o.value; cat2.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  document.querySelector('#tx-montant')?.closest('form')?.requestSubmit();
  await sleep(1600);
  const afterOut = { path: location.pathname, body: text().slice(0,600), solde: (text().match(/Solde actuel\s*:\s*([^\n]+)/)||[])[1] };

  // Virement
  await go('Finances');
  const soldesBefore = text();
  document.querySelector('a[href="/finance/virement"]')?.click(); await sleep(1200);
  const src = document.querySelector('#vir-source');
  const dst = document.querySelector('#vir-dest');
  if (src) {
    const o = Array.from(src.options||[]).find(x => /Courant/i.test(x.textContent||''));
    if (o) { src.value = o.value; src.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  if (dst) {
    const o = Array.from(dst.options||[]).find(x => /Livret/i.test(x.textContent||''));
    if (o) { dst.value = o.value; dst.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  setInput(document.querySelector('#vir-montant'), '20');
  document.querySelector('#vir-montant')?.closest('form')?.requestSubmit();
  await sleep(1800);
  const afterVirPage = text().slice(0,500);

  await go('Finances');
  const home = text();
  Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Compte courant'))?.click();
  await sleep(1400);
  const compteTxt = text();

  // category used delete
  await go('Finances');
  document.querySelector('a[href="/finance/parametres"]')?.click(); await sleep(1400);
  // add category then use it - skip if too long; try delete Non catégorisé system
  const catsSection = text();

  return {
    afterIn, afterOut, afterVirPage,
    soldesBefore: soldesBefore.slice(0,350),
    home: home.slice(0,450),
    compteTxt: compteTxt.slice(0,800),
    expectedSoldeAfterIO: afterOut.solde,
    hasVirLabel: /Virement interne/i.test(compteTxt),
    txCountHint: (compteTxt.match(/Virement interne|Non cat/g)||[]).length,
    depsensesSnippet: (home.match(/Dépenses du mois[\s\S]{0,250}/)||[''])[0],
    catsSection: catsSection.slice(0,400)
  };
})()
