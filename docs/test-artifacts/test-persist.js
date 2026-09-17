(async () => {
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  const byText = (sel, t) => Array.from(document.querySelectorAll(sel)).find(el => (el.textContent || '').includes(t));
  const go = async (label) => {
    const tg = document.querySelector('input.navbar-toggler');
    if (tg && !tg.checked) tg.click();
    await sleep(200);
    const a = byText('a', label);
    if (a) a.click();
    await sleep(1200);
  };
  await go('Finances');
  const fin = document.body.innerText;
  await go('Travail');
  const trav = document.body.innerText;
  await go('Courses');
  const course = document.body.innerText;
  return {
    finHasCompte: fin.includes('Compte courant'),
    travHasEmp: trav.includes('Employeur Test'),
    courseNoLait: !course.includes('Lait test'),
    courseAchatsEmpty: course.includes('Aucun achat'),
    fin: fin.slice(0, 400),
    trav: trav.slice(0, 300),
    course: course.slice(0, 300)
  };
})()
