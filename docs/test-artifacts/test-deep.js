(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));
  const setInput = (el, value) => {
    if (!el) return;
    el.focus();
    const proto = el.tagName === 'SELECT' ? HTMLSelectElement.prototype
      : el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype
      : HTMLInputElement.prototype;
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, String(value)); else el.value = String(value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const go = async (label) => {
    const toggler = document.querySelector('input.navbar-toggler');
    if (toggler && !toggler.checked) toggler.click();
    await sleep(300);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1600);
  };
  const clickExact = (label) => {
    const b = Array.from(document.querySelectorAll('button')).find(x => x.textContent.trim() === label);
    if (b) b.click();
    return !!b;
  };

  const out = {};

  // STOCK category
  await go('Stock');
  const params = document.querySelector('a[href="/stock/parametres"]');
  if (params) { params.click(); await sleep(1600); }
  setInput(document.querySelector('#stock-cat-nom'), 'Alimentaire');
  clickExact('Ajouter');
  await sleep(1500);
  out.afterCat = text().slice(0,500);

  await go('Stock');
  setInput(document.querySelector('#stock-nom'), 'Riz test');
  setInput(document.querySelector('#stock-qte'), '5');
  setInput(document.querySelector('#stock-unite'), 'kg');
  clickExact('Ajouter');
  await sleep(1800);
  out.afterAdd = text().slice(0,700);
  out.hasRiz = text().includes('Riz test');

  // duplicate
  setInput(document.querySelector('#stock-nom'), ' riz TEST ');
  setInput(document.querySelector('#stock-qte'), '2');
  clickExact('Ajouter');
  await sleep(1800);
  out.dupBody = text().slice(0,900);
  out.fusionVisible = !!byText('button', 'Fusionner');
  out.dupBlockedMsg = /existe déjà|fusion|identique/i.test(text());

  // adjust quantity via Modifier
  const mod = byText('button', 'Modifier');
  if (mod) {
    mod.click(); await sleep(800);
    // inventaire field - set to 8 (+3) then try negative path
    const nums = Array.from(document.querySelectorAll('input[type=number], input.form-control-sm'));
    // find inventaire quantity input - first number in edit row often
    const qEdit = document.querySelector('tbody input[type=number], tbody .form-control-sm');
    // Blazor InputNumber may be type=number
    const editInputs = Array.from(document.querySelectorAll('tbody input'));
    out.editInputs = editInputs.map(i => ({type:i.type, value:i.value, title:i.title}));
    if (editInputs[1]) {
      // positive adjust to 8
      setInput(editInputs[1], '8');
      clickExact('Enregistrer');
      await sleep(1500);
      out.afterPosAdjust = text().slice(0,500);
    }
    // try go below zero without inventaire motif - reopen edit set  -1?
    const mod2 = byText('button', 'Modifier');
    if (mod2) {
      mod2.click(); await sleep(700);
      const edits = Array.from(document.querySelectorAll('tbody input'));
      if (edits[1]) {
        setInput(edits[1], '-1');
        clickExact('Enregistrer');
        await sleep(1500);
        out.afterNegAttempt = text().slice(0,800);
        out.negBlocked = /négativ|inventaire|impossible|erreur/i.test(text()) || !!document.querySelector('.alert-danger');
      }
    }
  }

  // FINANCE: add epargne + transactions + virement
  await go('Finances');
  const fp = document.querySelector('a[href="/finance/parametres"]');
  if (fp) { fp.click(); await sleep(1600); }
  setInput(document.querySelector('#compte-nom'), 'Livret test');
  const typeSel = document.querySelector('#compte-type');
  if (typeSel) { setInput(typeSel, 'Epargne'); /* may need enum value */ 
    // try options
    const opt = Array.from(typeSel.options || []).find(o => /pargne/i.test(o.text));
    if (opt) { typeSel.value = opt.value; typeSel.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  setInput(document.querySelector('#compte-solde'), '50');
  clickExact('Ajouter');
  await sleep(1800);
  out.afterEpargne = text().slice(0,500);

  await go('Finances');
  // open compte courant
  const compteLink = Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Compte courant test'));
  if (compteLink) { compteLink.click(); await sleep(1800); }
  out.compteDetail = { path: location.pathname, body: text().slice(0,700) };
  const newTx = Array.from(document.querySelectorAll('a,button')).find(el => /nouvelle|transaction|Ajouter/i.test(el.textContent||''));
  out.txControls = Array.from(document.querySelectorAll('a,button')).slice(0,25).map(el => (el.textContent||'').trim().slice(0,40));

  // Virement page
  await go('Finances');
  const vir = document.querySelector('a[href="/finance/virement"]') || byText('a', 'Virement');
  if (vir) { vir.click(); await sleep(1600); }
  out.virementPage = { path: location.pathname, body: text().slice(0,800), inputs: Array.from(document.querySelectorAll('input,select,button')).slice(0,20).map(el => ({id:el.id,tag:el.tagName,text:(el.textContent||'').trim().slice(0,30),type:el.type})) };

  // TRAVAIL detail / conges / planning
  await go('Travail');
  const emp = Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Employeur Test'));
  if (emp) { emp.click(); await sleep(1800); }
  out.empDetail = { path: location.pathname, body: text().slice(0,1000) };
  out.empLinks = Array.from(document.querySelectorAll('a,button')).slice(0,30).map(el => ({text:(el.textContent||'').trim().slice(0,50), href: el.getAttribute && el.getAttribute('href')}));

  return out;
})()
