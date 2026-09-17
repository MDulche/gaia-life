(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const text = () => (document.body && document.body.innerText || '');
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent||'').includes(t));

  // Ensure on course
  if (!location.pathname.includes('course')) {
    const toggler = document.querySelector('input.navbar-toggler');
    if (toggler && !toggler.checked) toggler.click();
    await sleep(200);
    const a = byText('a', 'Courses');
    if (a) a.click();
    await sleep(1500);
  }

  const nom = document.querySelector('#article-nom');
  const qte = document.querySelector('#article-qte');
  if (!nom) return { step: 'form', error: 'no #article-nom', body: text().slice(0,500) };

  // set input values the Blazor-friendly way
  const setInput = (el, value) => {
    el.focus();
    el.value = value;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  };
  setInput(nom, 'Lait test emulator');
  if (qte) setInput(qte, '2');

  const addBtn = byText('button', 'Ajouter');
  if (!addBtn) return { step: 'addbtn', error: 'no Ajouter button' };
  addBtn.click();
  await sleep(2000);

  const afterAdd = text();
  const appears = afterAdd.includes('Lait test emulator');

  // find checkbox for the article row
  const rows = Array.from(document.querySelectorAll('table tbody tr'));
  const row = rows.find(r => (r.textContent||'').includes('Lait test emulator'));
  let checkInfo = null;
  if (row) {
    const cb = row.querySelector('input[type=checkbox], button, .form-check-input');
    if (cb) {
      // rapid multi-tap for idempotence
      cb.click();
      cb.click();
      cb.click();
      await sleep(1500);
      checkInfo = { clicked: true, after: text().slice(0,800) };
    } else {
      checkInfo = { clicked: false, rowHtml: row.innerHTML.slice(0,400) };
    }
  }

  return {
    step: 'add+check',
    appears,
    afterAddSnippet: afterAdd.slice(0,900),
    checkInfo,
    pathname: location.pathname
  };
})()
