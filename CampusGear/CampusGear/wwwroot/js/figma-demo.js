(() => {
  const page = document.querySelector('.figma-page');
  if (!page) return;

  const stage = page.querySelector('.figma-stage');
  const viewport = page.querySelector('.figma-stage-viewport');
  const mobile = page.querySelector('.figma-mobile');
  const route = page.dataset.route;
  const state = page.dataset.state;
  const area = route.split('/')[0];
  const demoUrl = path => path && /^\/(auth|borrower|custodian|admin)\//.test(path) ? `/demo${path}` : path;
  let role = state === 'administrator' || state === 'invalid-administrator' ? 'admin'
    : state === 'custodian' || state === 'invalid-custodian' ? 'custodian'
    : state === 'borrower' || state === 'invalid-borrower' ? 'borrower'
    : sessionStorage.getItem('campusgear-demo-role') || 'borrower';

  function fitStage() {
    if (window.innerWidth < 1000) {
      stage.style.transform = '';
      viewport.style.width = '1440px';
      viewport.style.height = '900px';
      return;
    }
    const scale = Math.min(window.innerWidth / 1440, window.innerHeight / 900);
    stage.style.transform = `scale(${scale})`;
    viewport.style.width = `${1440 * scale}px`;
    viewport.style.height = `${900 * scale}px`;
  }
  fitStage();
  window.addEventListener('resize', fitStage);

  const nav = {
    borrower: [
      ['Dashboard', '/borrower/dashboard'], ['Equipment Reservation', '/borrower/reservation'],
      ['Borrowing History', '/borrower/history'], ['Availability Calendar', '/borrower/calendar']
    ],
    custodian: [
      ['Dashboard', '/custodian/dashboard'], ['Approval & Release', '/custodian/approvals'],
      ['Return & Condition Check', '/custodian/returns'], ['Availability Calendar', '/custodian/calendar']
    ],
    admin: [
      ['Dashboard', '/admin/dashboard'], ['Reservation Management', '/admin/reservations'],
      ['Approval & Release', '/admin/approvals'], ['Return & Condition Check', '/admin/returns'],
      ['Equipment Categories', '/admin/categories'], ['Equipment Items', '/admin/equipment'],
      ['Borrower Profiles', '/admin/borrowers'], ['User & Role Management', '/admin/users'],
      ['Borrowing History', '/admin/history'], ['Availability Calendar', '/admin/calendar'],
      ['System Audit Log', '/admin/audit']
    ]
  };

  const normalize = value => (value || '').toLowerCase().replace(/&amp;/g, '&').replace(/[^a-z0-9]+/g, ' ').trim();
  function navTarget(label) {
    const key = normalize(label);
    const choices = nav[area] || [];
    return choices.find(([name]) => normalize(name) === key)?.[1]
      || choices.find(([name]) => key.includes(normalize(name)) || normalize(name).includes(key))?.[1]
      || null;
  }

  function roleDestination() {
    return role === 'admin' ? '/auth/two-factor' : `/${role}/dashboard`;
  }

  function actionTarget(element, index) {
    const name = element.getAttribute('data-name') || '';
    const label = `${name} ${element.textContent || ''}`;
    const key = normalize(label);
    if (key.includes('logout') || key.includes('log out')) return '/auth/login';
    if (key.includes('canonical nav')) return navTarget(name.replace(/^.*?CANONICAL NAV\s*•\s*/i, '').replace(/\s*•\s*HIT AREA.*$/i, '').trim());

    if (route === 'auth/login') {
      if (key.includes('forgot password')) return '/auth/password-reset-request';
      if (key.includes('new account')) return '/auth/signup';
      if (key.includes('try again')) return '/auth/login';
      if (['borrower', 'custodian', 'administrator'].includes(state) && (key.includes('verified') || key.includes('primary') || key.includes('continue'))) return roleDestination();
      if (key.includes('sign in')) return state === 'default' || state.startsWith('invalid-') ? `/auth/login?state=${role === 'admin' ? 'administrator' : role}` : roleDestination();
    }
    if (route === 'auth/two-factor') return key.includes('back') ? '/auth/login' : '/admin/dashboard';
    if (route === 'auth/access-denied') return '/auth/login';
    if (route === 'auth/password-reset-request') return key.includes('back') ? '/auth/login' : '/auth/password-reset-code';
    if (route === 'auth/password-reset-code') return '/auth/password-reset-new';
    if (route === 'auth/password-reset-new') return '/auth/password-reset-success';
    if (route === 'auth/password-reset-success') return '/auth/login';
    if (route === 'auth/signup') return '/auth/phone-verification';
    if (route === 'auth/phone-verification') return key.includes('recovery') ? '/auth/phone-verification?state=failed' : '/auth/account-created';
    if (route === 'auth/account-created') return '/auth/login';
    if (key.includes('back') || key.includes('cancel')) return state === 'default' ? `/${area}/dashboard` : `/${route}`;

    if (route === 'borrower/dashboard') return key.includes('history') ? '/borrower/history' : '/borrower/reservation';
    if (route === 'borrower/reservation') {
      if (key.includes('submit') || name === 'Rectangle') return state === 'conflict' ? '/borrower/reservation' : '/borrower/reservation?state=submitting';
      return '/borrower/reservation';
    }
    if (route === 'borrower/history') return state === 'default' ? '/borrower/history?state=filtered' : state === 'filtered' ? '/borrower/history?state=empty' : '/borrower/history';
    if (route === 'borrower/calendar') return '/borrower/reservation';

    if (route === 'custodian/dashboard') return key.includes('overdue') ? '/custodian/returns?state=late' : '/custodian/approvals';
    if (route === 'custodian/approvals') {
      if (element.dataset.nodeId === '695:1574' || key.includes('reject')) return '/custodian/approvals?state=rejected';
      if (state === 'approved') return '/custodian/approvals?state=released';
      if (state === 'rejected' || state === 'released') return '/custodian/approvals';
      return '/custodian/approvals?state=approved';
    }
    if (route === 'custodian/returns') {
      if (state === 'recording') {
        if (key.includes('damaged')) return '/custodian/returns?state=damaged';
        if (key.includes('late')) return '/custodian/returns?state=late';
        return '/custodian/returns?state=good';
      }
      return state === 'default' ? '/custodian/returns?state=recording' : '/custodian/returns';
    }
    if (route === 'custodian/calendar') return '/custodian/returns';

    if (route === 'admin/dashboard') {
      if (key.includes('active borrowings')) return '/admin/returns';
      if (key.includes('pending approvals')) return '/admin/approvals';
      if (key.includes('borrowers')) return '/admin/borrowers';
      return '/admin/reservations';
    }
    if (route === 'admin/reservations') return '/admin/reservations?state=created';
    if (route === 'admin/approvals') {
      if (key.includes('reject')) return '/admin/approvals?state=rejected';
      if (state === 'approved') return '/admin/approvals?state=released';
      return '/admin/approvals?state=approved';
    }
    if (route === 'admin/returns') return '/admin/returns?state=recorded';
    if (route === 'admin/calendar') return '/admin/reservations';
    if (['admin/categories', 'admin/equipment', 'admin/borrowers', 'admin/users'].includes(route)) {
      if (key.includes('edit')) return `/${route}?state=edit`;
      if (key.includes('add') || key.includes('create')) return `/${route}?state=add`;
      if (key.includes('deactivate')) return '/admin/users?state=deactivated';
      if (state === 'add' || state === 'edit') return route === 'admin/users' ? '/admin/users?state=access' : `/${route}?state=validation`;
      if (state !== 'default') return `/${route}`;
    }
    if (route === 'admin/history') return state === 'default' ? '/admin/history?state=filtered' : '/admin/history';
    return null;
  }

  const roles = [...stage.querySelectorAll('[data-name^="AUTH • ROLE ACCESS CARD"]')];
  function selectRole(nextRole) {
    role = nextRole;
    sessionStorage.setItem('campusgear-demo-role', role);
    roles.forEach(card => {
      const selected = normalize(card.dataset.name).includes(role === 'admin' ? 'administrator' : role);
      card.setAttribute('aria-pressed', selected ? 'true' : 'false');
    });
    const signIn = stage.querySelector('[data-name="AUTH • Button • SIGN IN"]');
    if (signIn) signIn.href = demoUrl(actionTarget(signIn, 0));
    const mobileSignIn = mobile.querySelector('.mobile-login-form .mobile-primary');
    if (mobileSignIn) mobileSignIn.href = demoUrl(`/auth/login?state=${role === 'admin' ? 'administrator' : role}`);
    mobile.querySelectorAll('[data-demo-role]').forEach(button => button.setAttribute('aria-pressed', button.dataset.demoRole === role ? 'true' : 'false'));
  }
  roles.forEach(card => {
    const cardRole = normalize(card.dataset.name).includes('administrator') ? 'admin' : normalize(card.dataset.name).includes('custodian') ? 'custodian' : 'borrower';
    card.tabIndex = 0;
    card.setAttribute('role', 'button');
    card.setAttribute('aria-label', `Select ${cardRole} demo role`);
    card.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); selectRole(cardRole); });
    card.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); selectRole(cardRole); } });
  });

  const interactive = [...stage.querySelectorAll('a, [data-name^="CANONICAL NAV"]')];
  interactive.forEach((element, index) => {
    const target = demoUrl(actionTarget(element, index));
    if (!target) return;
    if (element.tagName === 'A') element.href = target;
    else {
      element.setAttribute('role', 'link');
      element.tabIndex = 0;
      element.dataset.demoHref = target;
      element.addEventListener('keydown', event => { if (event.key === 'Enter') location.assign(target); });
    }
    const label = (element.textContent || element.dataset.name || 'Open demo screen').trim();
    element.setAttribute('aria-label', label === 'Rectangle' ? 'Continue demo flow' : label);
  });
  selectRole(role);

  stage.addEventListener('click', event => {
    if (event.target.closest('[data-name^="AUTH • ROLE ACCESS CARD"]')) return;
    let target = event.target.closest('a[href], [data-demo-href]');
    if (!target) {
      const candidates = interactive.filter(el => (el.href || el.dataset.demoHref) && (() => {
        const r = el.getBoundingClientRect();
        return event.clientX >= r.left && event.clientX <= r.right && event.clientY >= r.top && event.clientY <= r.bottom;
      })());
      candidates.sort((a, b) => {
        const x = a.getBoundingClientRect(), y = b.getBoundingClientRect();
        return x.width * x.height - y.width * y.height;
      });
      target = candidates[0];
    }
    if (!target) return;
    const href = target.href || target.dataset.demoHref;
    if (!href) return;
    event.preventDefault();
    location.assign(href);
  });

  if (route === 'borrower/reservation' && state === 'submitting') {
    window.setTimeout(() => location.replace(demoUrl('/borrower/reservation?state=submitted')), 900);
  }
  const humanTitle = title => title.replace(/^\d+[A-Z]?\s*[—–-]\s*/, '').replace(/\s*[—–]\s*(?:Conflict State|Request Submitted|Submitting|Filtered Results|No Matching Records|Recording|Success States).*$/i, '');
  function mobileSourceRows(options = {}) {
    const stageRect = stage.getBoundingClientRect();
    const items = [...stage.querySelectorAll('p')].map(element => {
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return { text: (element.textContent || '').replace(/\s+/g, ' ').trim(), x: (rect.left - stageRect.left) / (window.innerWidth < 1000 ? 1 : Math.min(window.innerWidth / 1440, window.innerHeight / 900)), y: (rect.top - stageRect.top) / (window.innerWidth < 1000 ? 1 : Math.min(window.innerWidth / 1440, window.innerHeight / 900)), size: parseFloat(style.fontSize) || 0 };
    }).filter(item => item.text && item.x >= (area === 'auth' ? 550 : 235) && item.y >= (options.minY || 80) && item.y < 850 && item.size >= 10 && item.text.length < 210 && (options.maxX == null || item.x < options.maxX) && (options.minX == null || item.x >= options.minX));
    const skipped = /^(SYSTEM READY|SESSION ACTIVE|CAMPUS ACCESS \/ SIGN IN|CAMPUS EQUIPMENT WORKSPACE|BORROWER WORKSPACE|CUSTODIAN WORKSPACE|ADMINISTRATOR WORKSPACE)$/i;
    const unique = new Set();
    return items.filter(item => {
      if (skipped.test(item.text)) return false;
      const key = `${item.text}|${Math.round(item.y / 12)}`;
      if (unique.has(key)) return false;
      unique.add(key);
      return true;
    }).sort((a, b) => a.y - b.y || a.x - b.x);
  }

  function mobileDashboardMetrics() {
    const boxes = [...stage.querySelectorAll('[data-name]')].filter(element => {
      const rect = element.getBoundingClientRect();
      const stageRect = stage.getBoundingClientRect();
      return rect.width >= 220 && rect.width <= 300 && rect.height >= 85 && rect.height <= 130 &&
        rect.top - stageRect.top > 180 && rect.top - stageRect.top < 330 && rect.left - stageRect.left >= 230;
    }).sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
    const unique = new Set();
    return boxes.filter(box => {
      const x = Math.round(box.getBoundingClientRect().left);
      if (unique.has(x)) return false;
      unique.add(x); return true;
    }).map(box => {
      const rect = box.getBoundingClientRect();
      const text = [...stage.querySelectorAll('p')].filter(p => {
        const r = p.getBoundingClientRect();
        return r.left >= rect.left && r.left < rect.right && r.top >= rect.top && r.top < rect.bottom;
      }).sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top).map(p => (p.textContent || '').trim()).filter(Boolean);
      return { name: box.dataset.name || '', text };
    }).filter(card => card.text.length);
  }

  function make(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text != null) element.textContent = text;
    return element;
  }

  function visibleActionLabel(element) {
    const rect = element.getBoundingClientRect();
    const labels = [...stage.querySelectorAll('p')].filter(candidate => {
      const position = candidate.getBoundingClientRect();
      return position.left >= rect.left - 8 && position.left < rect.right + 8 &&
        position.top >= rect.top - 8 && position.top < rect.bottom + 8;
    }).map(candidate => (candidate.textContent || '').replace(/\s+/g, ' ').trim()).filter(value => value && value.length <= 70);
    return labels[0] || (element.textContent || '').replace(/\s+/g, ' ').trim();
  }

  async function buildMobile() {
    const catalog = await fetch('/figma/catalog.json').then(response => response.json());
    window.CampusGearCatalog = catalog;
    const current = catalog.find(item => item.id === page.dataset.figmaNodeId);
    const shell = make('div', 'mobile-shell');
    const header = make('header', 'mobile-header');
    const brand = make('a', 'mobile-brand');
    brand.href = demoUrl('/auth/login');
    const logo = document.createElement('img');
    logo.src = '/figma/assets/b95f3.svg';
    logo.alt = '';
    brand.append(logo, make('span', '', 'CampusGear'));
    header.append(brand, make('span', 'mobile-role', area === 'auth' ? 'Campus access' : `${area} workspace`));
    shell.append(header);

    if (nav[area]) {
      const menu = make('details', 'mobile-menu');
      menu.append(make('summary', '', 'Menu'));
      const links = make('nav', 'mobile-nav');
      for (const [label, url] of nav[area]) {
        const link = make('a', url === `/${route}` ? 'active' : '', label);
        link.href = demoUrl(url);
        links.append(link);
      }
      const logout = make('a', '', 'Log out'); logout.href = demoUrl('/auth/login'); links.append(logout);
      menu.append(links); shell.append(menu);
    }

    const hero = make('section', 'mobile-hero');
    hero.append(make('div', 'mobile-eyebrow', area === 'auth' ? 'CAMPUS ACCESS' : `${area.toUpperCase()} • WORKSPACE`));
    const sourceHeading = route === 'auth/login' && state !== 'default'
      ? mobileSourceRows().find(item => item.size >= 20 && item.y < 250)?.text : null;
    hero.append(make('h1', '', route === 'auth/login' && state === 'default' ? 'Sign in to CampusGear' : sourceHeading || humanTitle(current?.title || route.split('/')[1])));
    shell.append(hero);

    if (route === 'auth/login' && (state === 'default' || state.startsWith('invalid-'))) {
      const rolesBox = make('div', 'mobile-role-cards');
      for (const [value, label, description] of [['borrower','Borrower','Reserve & borrow'],['custodian','Custodian','Approve & release'],['admin','Administrator','Govern access']]) {
        const button = make('button', 'mobile-role-card', '');
        button.type = 'button'; button.dataset.demoRole = value;
        button.append(make('strong', '', label), make('small', '', description));
        button.addEventListener('click', () => selectRole(value));
        rolesBox.append(button);
      }
      shell.append(rolesBox);
      const form = make('div', 'mobile-login-form');
      if (state.startsWith('invalid-')) form.append(make('p', 'mobile-error', 'Invalid credentials. Please try again.'));
      for (const [label, placeholder, type] of [['CAMPUS EMAIL','name@campus.edu','email'],['PASSWORD','••••••••','password']]) {
        const field = make('label', 'mobile-field');
        field.append(make('span', '', label));
        const input = document.createElement('input');
        input.type = type; input.placeholder = placeholder; input.autocomplete = 'off';
        field.append(input); form.append(field);
      }
      const login = make('a', 'mobile-primary', 'SIGN IN');
      login.href = demoUrl(`/auth/login?state=${role === 'admin' ? 'administrator' : role}`);
      form.append(login);
      const forgot = make('a', 'mobile-text-link', 'Forgot password?'); forgot.href = demoUrl('/auth/password-reset-request'); form.append(forgot);
      const signup = make('a', 'mobile-secondary', 'CREATE A NEW ACCOUNT'); signup.href = demoUrl('/auth/signup'); form.append(signup);
      shell.append(form);
    } else {
      const dashboard = route.endsWith('/dashboard');
      if (dashboard) {
        const metrics = mobileDashboardMetrics();
        if (metrics.length) {
          const cards = make('section', 'mobile-metrics');
          for (const metric of metrics) {
            const card = make('div', 'mobile-metric');
            metric.text.forEach((line, index) => card.append(make(index === 0 ? 'strong' : 'span', '', line)));
            cards.append(card);
          }
          shell.append(cards);
        }
      }
      const rows = mobileSourceRows(dashboard ? { minY: 320, maxX: 1000 } : {});
      const content = make('section', 'mobile-content');
      let band = null, previousY = -100;
      for (const item of rows) {
        if (item.size >= 20 && item.text.length < 90) {
          content.append(make('h2', 'mobile-section-title', item.text));
          band = null; previousY = -100; continue;
        }
        if (!band || Math.abs(item.y - previousY) > 16) {
          band = make('div', 'mobile-data-row');
          content.append(band);
        }
        band.append(make('span', item.size <= 11 ? 'mobile-data-label' : 'mobile-data-text', item.text));
        previousY = item.y;
      }
      shell.append(content);
      if (dashboard) {
        const sideRows = mobileSourceRows({ minY: 320, minX: 1000 });
        if (sideRows.length) {
          const side = make('section', 'mobile-content mobile-side-content');
          side.append(make('h2', 'mobile-section-title', 'Workflow'));
          for (const item of sideRows) side.append(make('div', 'mobile-data-row', item.text));
          shell.append(side);
        }
      }
      const actions = make('section', 'mobile-actions');
      const seen = new Set();
      for (const element of interactive) {
        const href = element.href || element.dataset.demoHref;
        if (!href || (element.dataset.name || '').includes('CANONICAL NAV') || normalize(element.dataset.name).includes('logout') || normalize(element.dataset.name).startsWith('field')) continue;
        if (seen.has(href)) continue;
        const raw = (visibleActionLabel(element) || element.dataset.name || '').replace(/^(AUTH|ACTION)\s*•\s*/i, '').replace(/•/g, ' ').trim();
        const label = raw && raw !== 'Rectangle' && raw !== 'Frame' ? raw : 'Continue';
        seen.add(href);
        const link = make('a', actions.childElementCount ? 'mobile-secondary' : 'mobile-primary', label);
        link.href = href; actions.append(link);
      }
      if (actions.childElementCount) shell.append(actions);
    }

    const states = catalog.filter(item => item.route === route);
    if (states.length > 1) {
      const preview = make('details', 'mobile-states');
      preview.append(make('summary', '', 'Preview Figma states'));
      for (const item of states) {
        const link = make('a', item.state === state ? 'active' : '', item.state.replace(/-/g, ' '));
        link.href = demoUrl(item.url); preview.append(link);
      }
      shell.append(preview);
    }
    mobile.replaceChildren(shell);
    const catalogData = document.createElement('script');
    catalogData.id = 'figma-screen-catalog';
    catalogData.type = 'application/json';
    catalogData.textContent = JSON.stringify(catalog);
    mobile.append(catalogData);
    selectRole(role);
  }
  buildMobile().catch(error => console.error('CampusGear mobile view:', error));
})();
