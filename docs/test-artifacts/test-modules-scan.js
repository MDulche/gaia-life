(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));
  const setInput = (el, value) => {
    el.focus();
    el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const go = async (label) => {
    const toggler = document.querySelector('input.navbar-toggler');
    if (toggler && !toggler.checked) toggler.click();
    await sleep(250);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1600);
    // close drawer if still open
    if (toggler && toggler.checked) toggler.click();
  };

  const results = {};

  // STOCK
  await go('Stock');
  results.stock = { path: location.pathname, body: text().slice(0, 900) };
  results.stockControls = Array.from(document.querySelectorAll('button,a,input,select,label')).slice(0, 60).map(el => ({
    tag: el.tagName, id: el.id, type: el.type || '', text: (el.textContent||'').trim().slice(0,60), href: el.getAttribute && el.getAttribute('href')
  }));

  // Try open add / parametres
  const addStock = byText('button', 'Ajouter') || byText('a', 'Ajouter') || byText('button', 'Nouvel') || byText('a', 'param');
  if (addStock) { addStock.click(); await sleep(1200); results.stockAfterClick = text().slice(0,800); }

  // FINANCE
  await go('Finances');
  results.finance = { path: location.pathname, body: text().slice(0, 900) };
  results.financeControls = Array.from(document.querySelectorAll('button,a')).slice(0, 40).map(el => ({
    text: (el.textContent||'').trim().slice(0,60), href: el.getAttribute && el.getAttribute('href')
  }));

  // TRAVAIL
  await go('Travail');
  results.travail = { path: location.pathname, body: text().slice(0, 900) };
  results.travailControls = Array.from(document.querySelectorAll('button,a,input')).slice(0, 40).map(el => ({
    tag: el.tagName, text: (el.textContent||'').trim().slice(0,60), id: el.id, type: el.type||''
  }));

  // Scan flags across modules by concatenating visited bodies
  const all = [results.stock.body, results.finance.body, results.travail.body, text()].join('\n');
  results.flags = {
    hasIA: /OpenAI|ChatGPT|intelligence artificielle|\bIA\b/i.test(all),
    hasLogin: /se connecter|mot de passe|login/i.test(all),
    hasRoles: /\bAdmin\b|\bMembre\b|Lecture seule/i.test(all),
    hasNetworkUi: /synchroniser|api key|cloud sync/i.test(all)
  };
  return results;
})()
