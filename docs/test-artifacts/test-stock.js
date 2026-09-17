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

  await go('Stock');
  const params = document.querySelector('a[href="/stock/parametres"]');
  if (params) { params.click(); await sleep(1400); }
  setInput(document.querySelector('#stock-cat-nom'), 'Alimentaire');
  const form = document.querySelector('#stock-cat-nom')?.closest('form');
  if (form) form.requestSubmit(); else clickExact('Ajouter');
  await sleep(1400);
  const afterCat = text();

  await go('Stock');
  setInput(document.querySelector('#stock-nom'), 'Riz test');
  setInput(document.querySelector('#stock-qte'), '5');
  setInput(document.querySelector('#stock-unite'), 'kg');
  const addForm = document.querySelector('#stock-nom')?.closest('form');
  if (addForm) addForm.requestSubmit(); else clickExact('Ajouter');
  await sleep(1600);
  const afterAdd = text();

  setInput(document.querySelector('#stock-nom'), ' riz TEST ');
  setInput(document.querySelector('#stock-qte'), '2');
  const addForm2 = document.querySelector('#stock-nom')?.closest('form');
  if (addForm2) addForm2.requestSubmit(); else clickExact('Ajouter');
  await sleep(1600);
  const afterDup = text();

  // positive inventaire adjust 5 -> 8
  const mod = byText('button', 'Modifier');
  let afterPos = null, afterNeg = null;
  if (mod) {
    mod.click(); await sleep(600);
    const edits = Array.from(document.querySelectorAll('tbody input'));
    if (edits[1]) {
      setInput(edits[1], '8');
      clickExact('Enregistrer');
      await sleep(1400);
      afterPos = text();
    }
    const mod2 = byText('button', 'Modifier');
    if (mod2) {
      mod2.click(); await sleep(600);
      const edits2 = Array.from(document.querySelectorAll('tbody input'));
      if (edits2[1]) {
        setInput(edits2[1], '-1');
        clickExact('Enregistrer');
        await sleep(1400);
        afterNeg = text();
      }
    }
  }

  return {
    afterCatHasAlim: afterCat.includes('Alimentaire'),
    hasRiz: afterAdd.includes('Riz test'),
    afterAdd: afterAdd.slice(0,500),
    fusionVisible: !!byText('button', 'Fusionner'),
    dupMsg: afterDup.slice(0,700),
    qtyAfterPos: afterPos && (afterPos.match(/Riz test[\s\S]{0,40}?([\d.,]+)/) || [])[1],
    afterPosSnippet: afterPos && afterPos.slice(0,400),
    afterNegSnippet: afterNeg && afterNeg.slice(0,600),
    negAllowedViaInventaire: afterNeg && afterNeg.includes('-1'),
    errorNeg: afterNeg && !!document.querySelector('.alert-danger')
  };
})()
