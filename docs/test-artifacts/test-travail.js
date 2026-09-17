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

  await go('Travail');
  const home = text();
  const emp = Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes('Employeur Test'));
  if (emp) { emp.click(); await sleep(1800); }
  const detail = text();

  // fiche de paie
  const ficheBtn = byText('button', 'Entrer une fiche');
  if (ficheBtn) { ficheBtn.click(); await sleep(700); }
  setInput(document.querySelector('#fiche-brut'), '3000');
  setInput(document.querySelector('#fiche-net'), '2300');
  setInput(document.querySelector('#fiche-cotis'), '700');
  const ficheForm = document.querySelector('#fiche-brut')?.closest('form');
  if (ficheForm) ficheForm.requestSubmit();
  await sleep(1600);
  const afterFiche = text();

  // conge exceed solde
  const congeBtn = byText('button', 'Entrer un cong');
  if (congeBtn) { congeBtn.click(); await sleep(700); }
  const typeSel = document.querySelector('#conge-type');
  if (typeSel) {
    const opt = Array.from(typeSel.options||[]).find(o => /Pay/i.test(o.textContent||''));
    if (opt) { typeSel.value = opt.value; typeSel.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  setInput(document.querySelector('#conge-debut'), '2026-09-20');
  setInput(document.querySelector('#conge-fin'), '2026-12-31');
  setInput(document.querySelector('#conge-jours'), '60');
  const congeForm = document.querySelector('#conge-debut')?.closest('form');
  if (congeForm) congeForm.requestSubmit();
  await sleep(1600);
  const afterBigConge = text();

  // overlapping conges - first small then overlap
  setInput(document.querySelector('#conge-debut'), '2026-10-01');
  setInput(document.querySelector('#conge-fin'), '2026-10-05');
  setInput(document.querySelector('#conge-jours'), '3');
  if (congeForm) congeForm.requestSubmit();
  await sleep(1400);
  const afterFirst = text();
  setInput(document.querySelector('#conge-debut'), '2026-10-03');
  setInput(document.querySelector('#conge-fin'), '2026-10-08');
  setInput(document.querySelector('#conge-jours'), '4');
  if (document.querySelector('#conge-debut')?.closest('form')) document.querySelector('#conge-debut').closest('form').requestSubmit();
  await sleep(1400);
  const afterOverlap = text();

  // planning / ferie
  const planning = text();
  const hasFerie = /f[eé]ri[eé]|Assomption|Toussaint|Noël|Armistice|Victoire|Ascension|Pentecôte|Lundi de|1er mai|14 juillet/i.test(planning);

  return {
    homeHasEmp: home.includes('Employeur Test'),
    detailSnippet: detail.slice(0,600),
    afterFiche: afterFiche.slice(0,700),
    ficheVisible: /2300|3[\s ]?000|fiche/i.test(afterFiche),
    afterBigConge: afterBigConge.slice(0,700),
    soldeBlocked: /solde insuffisant|insuffisant/i.test(afterBigConge),
    afterFirst: afterFirst.slice(0,400),
    afterOverlap: afterOverlap.slice(0,700),
    overlapBlocked: /chevauche/i.test(afterOverlap),
    hasFerie,
    planningHasCal: /planning|calendrier|lundi|mardi/i.test(planning)
  };
})()
