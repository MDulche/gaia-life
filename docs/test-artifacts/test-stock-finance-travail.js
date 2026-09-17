(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));
  const setInput = (el, value) => {
    el.focus();
    const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, value); else el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const go = async (label) => {
    const toggler = document.querySelector('input.navbar-toggler');
    if (toggler && !toggler.checked) toggler.click();
    await sleep(300);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1800);
  };

  const out = {};

  // ===== STOCK: create category via parametres, then article, adjust qty =====
  await go('Stock');
  out.stockHome = { path: location.pathname, body: text().slice(0,500) };
  const params = byText('a', 'Catégories') || document.querySelector('a[href="/stock/parametres"]');
  if (params) { params.click(); await sleep(1800); }
  out.stockParams = { path: location.pathname, body: text().slice(0,700), controls: Array.from(document.querySelectorAll('input,button')).slice(0,30).map(el => ({id:el.id,type:el.type,text:(el.textContent||'').trim().slice(0,40)})) };

  // try add category - look for common ids
  const catNom = document.querySelector('#categorie-nom, #cat-nom, input[placeholder*="atégor"], form input[type=text]');
  if (catNom) {
    setInput(catNom, 'Alimentaire');
    const addCat = byText('button', 'Ajouter') || byText('button', 'Créer');
    if (addCat) { addCat.click(); await sleep(1500); }
    out.catAdd = text().slice(0,600);
  }

  // back to stock home
  await go('Stock');
  const stockNom = document.querySelector('#stock-nom');
  out.stockFormReady = !!stockNom;
  if (stockNom) {
    setInput(stockNom, 'Riz test');
    const q = document.querySelector('#stock-qte');
    if (q) setInput(q, '5');
    const u = document.querySelector('#stock-unite');
    if (u) setInput(u, 'kg');
    const add = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === 'Ajouter');
    if (add) { add.click(); await sleep(2000); }
    out.afterStockAdd = text().slice(0,800);

    // duplicate name uniqueness/fusion
    setInput(document.querySelector('#stock-nom'), ' riz test ');
    const q2 = document.querySelector('#stock-qte');
    if (q2) setInput(q2, '1');
    const add2 = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === 'Ajouter');
    if (add2) { add2.click(); await sleep(2000); }
    out.afterDup = text().slice(0,900);

    // accept fusion or note block
    const fusion = byText('button', 'Fusionner');
    if (fusion) { out.fusionShown = true; /* leave for report */ }
  }

  // ===== FINANCE =====
  await go('Finances');
  out.financeHome = { path: location.pathname, body: text().slice(0,700) };
  const finParams = document.querySelector('a[href="/finance/parametres"]') || byText('a', 'Paramètres');
  if (finParams) { finParams.click(); await sleep(1800); }
  out.financeParams = { path: location.pathname, body: text().slice(0,700) };
  const compteNom = document.querySelector('#compte-nom');
  if (compteNom) {
    setInput(compteNom, 'Compte courant test');
    const solde = document.querySelector('#compte-solde');
    if (solde) setInput(solde, '100');
    const addC = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === 'Ajouter');
    if (addC) { addC.click(); await sleep(2000); }
    out.afterCompte = text().slice(0,700);
  }

  // ===== TRAVAIL =====
  await go('Travail');
  out.travailHome = { path: location.pathname, body: text().slice(0,500) };
  const addEmp = byText('button', 'Ajouter un employeur');
  if (addEmp) { addEmp.click(); await sleep(800); }
  const empNom = document.querySelector('#emp-nom');
  if (empNom) {
    setInput(empNom, 'Employeur Test SA');
    const debut = document.querySelector('#emp-debut');
    if (debut) setInput(debut, '2026-01-01');
    const creer = byText('button', 'Créer');
    if (creer) { creer.click(); await sleep(2000); }
    out.afterEmployeur = text().slice(0,800);
  }

  const all = JSON.stringify(out);
  out.flags = {
    hasIA: /OpenAI|ChatGPT|intelligence artificielle/i.test(all),
    hasLogin: /se connecter|mot de passe|\blogin\b/i.test(all),
    hasRolesAdminMembreLecture: /\bAdmin\b|\bMembre\b|Lecture seule/i.test(all),
    mentionsAdministrateur: /administrateur/i.test(all)
  };
  return out;
})()
