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

  // Navigate via href if table link broken
  await go('Travail');
  const links = Array.from(document.querySelectorAll('a')).map(a => ({href:a.getAttribute('href'), text:(a.textContent||'').trim()}));
  const empLink = links.find(l => l.href && l.href.includes('employeurs'));
  if (empLink) {
    location.href = empLink.href.startsWith('http') ? empLink.href : ('https://0.0.0.1/' + empLink.href.replace(/^\//,''));
    await sleep(2500);
  } else {
    // try click row
    const a = Array.from(document.querySelectorAll('a')).find(x => (x.textContent||'').includes('Employeur Test'));
    if (a) { a.click(); await sleep(2500); }
  }
  return {
    links,
    path: location.pathname,
    body: text().slice(0,1200),
    errorBanner: text().includes('unhandled error'),
    blazorError: (document.querySelector('#blazor-error-ui') || {}).textContent || null
  };
})()
