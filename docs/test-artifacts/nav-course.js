(async () => {
  const links = Array.from(document.querySelectorAll('a')).map(a => ({
    href: a.getAttribute('href'),
    text: (a.textContent || '').trim().replace(/\s+/g, ' ')
  }));
  const toggler = document.querySelector('input.navbar-toggler');
  if (toggler && !toggler.checked) toggler.click();
  await new Promise(r => setTimeout(r, 300));
  const byText = (t) => Array.from(document.querySelectorAll('a')).find(a => (a.textContent||'').includes(t));
  const course = byText('Courses');
  if (course) course.click();
  await new Promise(r => setTimeout(r, 2000));
  return {
    title: document.title,
    href: location.href,
    pathname: location.pathname,
    links,
    body: (document.body && document.body.innerText || '').slice(0, 1200)
  };
})()
