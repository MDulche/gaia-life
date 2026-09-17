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

  await go('Stock');
  const stockBefore = text();
  const rizQty = (stockBefore.match(/Riz test\s+(\S+)/)||[])[1];

  await go('Courses');
  setInput(document.querySelector('#article-nom'), 'Riz courses lien');
  setInput(document.querySelector('#article-qte'), '1');
  const stockSel = document.querySelector('#article-stock');
  if (stockSel) {
    const o = Array.from(stockSel.options||[]).find(x => /Riz/i.test(x.textContent||''));
    if (o) { stockSel.value = o.value; stockSel.dispatchEvent(new Event('change',{bubbles:true})); }
  }
  document.querySelector('#article-nom')?.closest('form')?.requestSubmit();
  await sleep(1600);
  const afterAdd = text();

  // mark bought
  const row = Array.from(document.querySelectorAll('table tbody tr')).find(r => (r.textContent||'').includes('Riz courses lien'));
  const cb = row && row.querySelector('input[type=checkbox], .form-check-input, button');
  if (cb) { cb.click(); await sleep(1800); }
  const afterBuy = text();

  await go('Stock');
  const stockAfter = text();
  const rizQtyAfter = (stockAfter.match(/Riz test\s+(\S+)/)||[])[1];

  return {
    rizQty, rizQtyAfter,
    stockLinkedOption: !!document.querySelector('#article-stock'),
    afterAdd: afterAdd.slice(0,400),
    afterBuy: afterBuy.slice(0,400),
    stockAfter: stockAfter.slice(0,500),
    liaisonOk: rizQty && rizQtyAfter && rizQtyAfter !== rizQty
  };
})()
