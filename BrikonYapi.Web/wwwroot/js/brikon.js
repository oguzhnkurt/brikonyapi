/* ========================================================
   BRIKON YAPI — Main JavaScript
   ======================================================== */

document.addEventListener('DOMContentLoaded', () => {

  /* ── HEADER SCROLL ──────────────────────────────────── */
  const header = document.getElementById('site-header');
  if (header) {
    const hasDarkHero = document.body.classList.contains('has-dark-hero');
    const update = () => {
      if (window.scrollY > 80) {
        header.classList.remove('transparent');
        header.classList.add('scrolled');
      } else if (hasDarkHero) {
        header.classList.remove('scrolled');
        header.classList.add('transparent');
      } else {
        header.classList.add('scrolled');
      }
    };
    window.addEventListener('scroll', update, { passive: true });
    update();
  }

  /* ── MOBILE MENU ────────────────────────────────────── */
  const mobileNav   = document.getElementById('mobile-nav');
  const hamburger   = document.getElementById('hamburger-btn');
  const mobileClose = document.getElementById('mobile-nav-close') || document.getElementById('mobile-close');
  hamburger?.addEventListener('click', () => {
    const isOpen = mobileNav?.classList.toggle('open');
    hamburger.classList.toggle('open', isOpen);
  });
  mobileClose?.addEventListener('click', () => {
    mobileNav?.classList.remove('open');
    hamburger?.classList.remove('open');
  });

  /* ── DİL SEÇİCİ ─────────────────────────────────────── */
  document.querySelectorAll('.lang-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.lang-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      // İleride i18n entegrasyonu için localStorage'a kaydet
      localStorage.setItem('lang', btn.dataset.lang);
    });
  });
  // Sayfa yüklenince kayıtlı dili yansıt
  const savedLang = localStorage.getItem('lang');
  if (savedLang) {
    document.querySelectorAll('.lang-btn').forEach(b => {
      b.classList.toggle('active', b.dataset.lang === savedLang);
    });
  }

  /* ── HEADER DROPDOWNS ───────────────────────────────── */
  document.querySelectorAll('.nav-dropdown').forEach(dd => {
    const toggle = dd.querySelector('.nav-dropdown-toggle');
    toggle?.addEventListener('click', (e) => {
      e.stopPropagation();
      const isOpen = dd.classList.contains('open');
      // close all others
      document.querySelectorAll('.nav-dropdown.open').forEach(o => o.classList.remove('open'));
      if (!isOpen) dd.classList.add('open');
    });
  });
  document.addEventListener('click', () => {
    document.querySelectorAll('.nav-dropdown.open').forEach(o => o.classList.remove('open'));
  });

  /* ── HERO SLIDER ────────────────────────────────────── */
  const heroSection = document.getElementById('hero');
  if (heroSection) {
    const videos   = [...heroSection.querySelectorAll('.hero-video')];
    const slides   = [...heroSection.querySelectorAll('.hero-slide')];
    const tabs     = [...heroSection.querySelectorAll('.hero-tab')];
    const tabWrap  = heroSection.querySelector('.hero-tabs');
    const curNum   = document.getElementById('hero-cur-num');
    const progFill = document.getElementById('hero-progress-fill');
    const total    = Math.max(videos.length, slides.length);

    if (total === 0) return;

    let current = 0;
    let timer   = null;

    const resetProgress = () => {
      if (!progFill) return;
      progFill.style.transition = 'none';
      progFill.style.width = '0%';
      requestAnimationFrame(() => requestAnimationFrame(() => {
        progFill.style.transition = 'width 7s linear';
        progFill.style.width = '100%';
      }));
    };

    const goTo = (raw) => {
      const next = ((raw % total) + total) % total;
      if (next === current && slides[current]?.classList.contains('in')) return;

      slides[current]?.classList.remove('active', 'in');
      videos[current]?.classList.remove('active');
      tabs[current]?.classList.remove('active');

      current = next;

      const slide = slides[current];
      const video = videos[current];

      slide?.classList.add('active');
      video?.classList.add('active');
      tabs[current]?.classList.add('active');

      /* Tab'ı görünür alana kaydır */
      if (tabWrap && tabs[current]) {
        const t = tabs[current];
        const wL = tabWrap.scrollLeft, wR = wL + tabWrap.clientWidth;
        const tL = t.offsetLeft, tR = tL + t.offsetWidth;
        if (tL < wL) tabWrap.scrollLeft = tL;
        else if (tR > wR) tabWrap.scrollLeft = tR - tabWrap.clientWidth;
      }

      /* Sayaç güncelle */
      if (curNum) curNum.textContent = String(current + 1).padStart(2, '0');

      /* Animate-in */
      requestAnimationFrame(() => requestAnimationFrame(() => slide?.classList.add('in')));

      /* Video */
      if (video?.tagName === 'VIDEO') {
        video.currentTime = 0;
        video.play().catch(() => {});
      }

      /* Progress bar */
      if (total > 1) resetProgress();
    };

    const startTimer = () => {
      clearInterval(timer);
      if (total > 1) timer = setInterval(() => goTo(current + 1), 7000);
    };

    const stopTimer = () => clearInterval(timer);

    /* Tab tıklamaları */
    tabs.forEach((t, i) => {
      t.addEventListener('click', () => { stopTimer(); goTo(i); startTimer(); });
    });

    /* Başlat */
    goTo(0);
    startTimer();
  }

  /* ── SCROLL REVEAL ──────────────────────────────────── */
  const revealEls = document.querySelectorAll('.reveal, .reveal-left, .reveal-right');
  if (revealEls.length) {
    const obs = new IntersectionObserver((entries) => {
      entries.forEach(e => { if (e.isIntersecting) { e.target.classList.add('visible'); obs.unobserve(e.target); } });
    }, { threshold: 0.12 });
    revealEls.forEach(el => obs.observe(el));
  }

  /* ── COUNTER ANİMASYON ──────────────────────────────── */
  const counters = document.querySelectorAll('[data-count]');
  if (counters.length) {
    const obs = new IntersectionObserver((entries) => {
      entries.forEach(e => {
        if (!e.isIntersecting) return;
        const el     = e.target;
        const target = parseInt(el.dataset.count);
        const suffix = el.dataset.suffix || '';
        const dur    = 1800;
        const start  = performance.now();
        const tick   = (now) => {
          const p = Math.min((now - start) / dur, 1);
          const eased = 1 - Math.pow(1 - p, 3);
          el.textContent = Math.round(eased * target).toLocaleString('tr-TR') + suffix;
          if (p < 1) requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
        obs.unobserve(el);
      });
    }, { threshold: 0.5 });
    counters.forEach(c => obs.observe(c));
  }

  /* ── TAB FİLTRE (projeler sayfası) ─────────────────── */
  document.querySelectorAll('.project-tab').forEach(tab => {
    tab.addEventListener('click', () => {
      const url = new URL(window.location);
      url.searchParams.set('tab', tab.dataset.tab);
      window.location.href = url.toString();
    });
  });

  /* ── ALERT AUTO CLOSE ───────────────────────────────── */
  document.querySelectorAll('.alert[data-auto]').forEach(el => {
    setTimeout(() => {
      el.style.transition = 'opacity .5s';
      el.style.opacity = '0';
      setTimeout(() => el.remove(), 500);
    }, 4500);
  });

  /* ── LİGHTBOX ───────────────────────────────────────── */
  const galleryImgs = document.querySelectorAll('.gallery-grid img');
  if (galleryImgs.length) {
    const lb = document.createElement('div');
    lb.id = 'lightbox';
    lb.style.cssText = 'display:none;position:fixed;inset:0;background:rgba(0,0,0,.93);z-index:9999;align-items:center;justify-content:center;';
    lb.innerHTML = `
      <button id="lb-close" style="position:absolute;top:1.5rem;right:1.5rem;background:none;border:none;color:#fff;font-size:3rem;cursor:pointer;line-height:1;">&times;</button>
      <img id="lb-img" style="max-width:90vw;max-height:88vh;object-fit:contain;" src="" alt="">`;
    document.body.appendChild(lb);

    const lbImg = lb.querySelector('#lb-img');
    const open  = (src) => { lbImg.src = src; lb.style.display = 'flex'; document.body.style.overflow = 'hidden'; };
    const close = ()    => { lb.style.display = 'none'; document.body.style.overflow = ''; };

    galleryImgs.forEach(img => img.addEventListener('click', () => open(img.src)));
    lb.querySelector('#lb-close').addEventListener('click', close);
    lb.addEventListener('click', e => { if (e.target === lb) close(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
  }

  /* ── SMOOTH SCROLL (anchor links) ──────────────────── */
  document.querySelectorAll('a[href^="/#"]').forEach(a => {
    a.addEventListener('click', e => {
      if (window.location.pathname !== '/') return;
      const id = a.getAttribute('href').slice(2);
      const el = document.getElementById(id);
      if (el) { e.preventDefault(); el.scrollIntoView({ behavior:'smooth' }); }
    });
  });

  /* ── CONTACT HASH SCROLL ────────────────────────────── */
  if (window.location.hash === '#contact') {
    const el = document.getElementById('contact');
    if (el) setTimeout(() => el.scrollIntoView({ behavior:'smooth' }), 300);
  }

});
