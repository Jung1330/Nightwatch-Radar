/* ==========================================================================
   Nightwatch site — davranis katmani
   - 4 dil degistirme (localStorage + tarayici dili)
   - ozellik kartlari / SSS uretimi
   - sekme + ekran goruntusu degistirme
   - changelog render (data/changelog.js)
   - surum: App/version.txt (varsa) yoksa gomulu yedek
   - scroll reveal, sticky nav, mobil menu
   ========================================================================== */
(function () {
  'use strict';

  var SURUM_YEDEK = '1.5.5';           // App/version.txt okunamazsa (file://) kullanilir
  var SURUM = SURUM_YEDEK;
  var DILLER = ['tr', 'en', 'ru', 'zh'];
  var VARSAYILAN = 'tr';

  /* --- hangi ozellik kartlari gosterilecek (i18n anahtarlari) --- */
  var OZELLIKLER = [
    { k: 'f1' }, { k: 'f2' }, { k: 'f4' }, { k: 'f5' }, { k: 'f6' }, { k: 'f7' },
    { k: 'f9' }, { k: 'f10' }, { k: 'f11' }, { k: 'f12' },
    { k: 'f13', yeni: true }, { k: 'f14', yeni: true }
  ];

  var SEKMELER = {
    res: { n: 1 }, mobs: { n: 2 }, players: { n: 3 }, device: { n: 4 },
    config: { n: 5 }, dev: { n: 6 }, settings: { n: 7 }
  };

  var SSS = ['faq1', 'faq2', 'faq3', 'faq4', 'faq5', 'faq6', 'faq7'];
  var IKON = { add: '+', fix: '~', warn: '!', info: 'i', tip: '\u25B6' };

  var lang = VARSAYILAN;

  function $(s) { return document.querySelector(s); }
  function $$(s) { return Array.prototype.slice.call(document.querySelectorAll(s)); }

  /* sozluk: hem window.I18N hem de ust-duzey I18N degiskeni desteklenir */
  var SOZLUK = window.I18N || (typeof I18N !== 'undefined' ? I18N : {});
  var GUNLUK = window.CHANGELOG || (typeof CHANGELOG !== 'undefined' ? CHANGELOG : null);

  function t(k) {
    var d = (SOZLUK && SOZLUK[lang]) || {};
    if (d[k] != null && d[k] !== '') return d[k];
    var e = (SOZLUK && SOZLUK.en) || {};
    if (e[k] != null && e[k] !== '') return e[k];
    var tr = (SOZLUK && SOZLUK.tr) || {};
    return tr[k] != null ? tr[k] : '';
  }
  /* surum numarasini metin icinde guncelle: "Surum 1.5.4 — ..." -> "Surum <SURUM> — ..." */
  function surumMetni(s) { return String(s).replace(/\d+\.\d+\.\d+/g, SURUM); }

  /* ------------------------------------------------------------ dil */
  function dilUygula(yeni) {
    lang = DILLER.indexOf(yeni) >= 0 ? yeni : VARSAYILAN;
    try { localStorage.setItem('nw_lang', lang); } catch (e) {}

    document.documentElement.lang = lang;
    $$('[data-i18n]').forEach(function (el) {
      var k = el.getAttribute('data-i18n');
      if (!k) return;
      var v = t(k);
      if (v) el.innerHTML = v;
    });

    $$('.langs button').forEach(function (b) {
      b.classList.toggle('on', b.getAttribute('data-lang') === lang);
    });

    /* surum iceren metinler */
    var dv = $('#dlVersion');
    if (dv) dv.innerHTML = surumMetni(t('dl_version'));
    var sv = $('[data-stat="version"]');
    if (sv) sv.textContent = 'v' + SURUM;
    var cv = $('#clVer');
    if (cv) cv.textContent = 'v' + SURUM;
    var sl = $('#shotLang');
    if (sl) sl.textContent = lang.toUpperCase();

    ekranGuncelle();
    changelogCiz();
    SSS_ciz();
  }

  /* --------------------------------------------------- ozellik kartlari */
  function ozellikleriCiz() {
    var kap = $('#featGrid');
    if (!kap) return;
    kap.innerHTML = OZELLIKLER.map(function (o, i) {
      var no = (i + 1 < 10 ? '0' : '') + (i + 1);
      return '<article class="card reveal' + (o.yeni ? ' is-new' : '') + '">' +
        '<span class="no">' + no + '</span>' +
        '<h3 data-i18n="' + o.k + '_title"></h3>' +
        '<p data-i18n="' + o.k + '_desc"></p>' +
        '</article>';
    }).join('');
  }

  /* ------------------------------------------------------------ SSS */
  function SSS_ciz() {
    var kap = $('#faqList');
    if (!kap) return;
    kap.innerHTML = SSS.map(function (k, i) {
      return '<details' + (i === 0 ? ' open' : '') + '>' +
        '<summary data-i18n="' + k + '_q"></summary>' +
        '<div class="a" data-i18n="' + k + '_a"></div>' +
        '</details>';
    }).join('');
    $$('#faqList [data-i18n]').forEach(function (el) {
      var v = t(el.getAttribute('data-i18n'));
      if (v) el.innerHTML = v;
    });
  }

  /* --------------------------------------------------------- ekranlar */
  function ekranGuncelle(sekme) {
    var btn = document.querySelector('.tabs button.on');
    var aktif = sekme || (btn && btn.getAttribute('data-tab')) || 'res';
    if (!SEKMELER[aktif]) aktif = 'res';
    var bilgi = SEKMELER[aktif];
    var img = $('#shot');
    if (img) {
      img.src = 'App/' + lang.toUpperCase() + '/' + bilgi.n + '.png';
      img.alt = t('sc_' + aktif + '_title') || 'Nightwatch';
    }
    var b = $('#shotTitle');
    if (b) b.innerHTML = t('sc_' + aktif + '_title');
    var d = $('#shotDesc');
    if (d) d.innerHTML = t('sc_' + aktif + '_desc');
  }

  /* -------------------------------------------------------- changelog */
  function changelogCiz() {
    var kap = $('#clList');
    if (!kap || !GUNLUK || !GUNLUK.gruplar) return;
    var html = '';
    var ilk = true;
    GUNLUK.gruplar.forEach(function (g) {
      if (!ilk) html += '<hr class="cl-divider">';
      ilk = false;
      html += '<div class="cl-section">' + g.baslik + (g.tarih ? ' · ' + g.tarih : '') + '</div>';
      var girisler = (g.girisler && (g.girisler[lang] || g.girisler.en || g.girisler.tr)) || [];
      girisler.forEach(function (e) {
        if (e.icon === 'divider') { html += '<hr class="cl-divider">'; return; }
        if (e.icon === 'section') { html += '<div class="cl-section">' + e.text + '</div>'; return; }
        var simge = IKON[e.icon] || '+';
        html += '<div class="cl-entry"><span class="cl-icon ' + (e.icon || 'info') + '">' + simge +
          '</span><span class="cl-text">' + e.text + '</span></div>';
      });
    });
    kap.innerHTML = html;

    /* "v1.5.0 - v1.5.5" araligini veriden uret */
    var meta = $('#clMeta');
    if (meta && GUNLUK.gruplar.length > 1) {
      var son = GUNLUK.gruplar[GUNLUK.gruplar.length - 1].baslik;
      var ilk2 = GUNLUK.gruplar[0].baslik;
      meta.textContent = son + ' - ' + ilk2;
    } else if (meta) {
      meta.textContent = GUNLUK.gruplar[0].baslik;
    }
  }

  /* ------------------------------------------------------------ surum */
  function surumOku() {
    if (!window.fetch) return;
    fetch('App/version.txt', { cache: 'no-store' })
      .then(function (r) { return r.ok ? r.text() : ''; })
      .then(function (txt) {
        var m = String(txt).match(/\d+\.\d+\.\d+/);
        if (m) {
          SURUM = m[0];
          var sv = $('[data-stat="version"]');
          if (sv) sv.textContent = 'v' + SURUM;
          var cv = $('#clVer');
          if (cv) cv.textContent = 'v' + SURUM;
          var dv = $('#dlVersion');
          if (dv) dv.innerHTML = surumMetni(t('dl_version'));
        }
      })
      .catch(function () { /* file:// -> gomulu yedek kalir */ });
  }

  /* ------------------------------------------------------------ olaylar */
  function olaylariBagla() {
    $$('.langs button').forEach(function (b) {
      b.addEventListener('click', function () { dilUygula(b.getAttribute('data-lang')); });
    });

    $$('.tabs button').forEach(function (b) {
      b.addEventListener('click', function () {
        $$('.tabs button').forEach(function (x) { x.classList.remove('on'); });
        b.classList.add('on');
        ekranGuncelle(b.getAttribute('data-tab'));
      });
    });

    var burger = $('#burger');
    if (burger) {
      burger.addEventListener('click', function () {
        var nav = $('.nav-links');
        var acik = nav.classList.toggle('open');
        burger.setAttribute('aria-expanded', acik ? 'true' : 'false');
      });
      $$('.nav-links a').forEach(function (a) {
        a.addEventListener('click', function () {
          $('.nav-links').classList.remove('open');
          burger.setAttribute('aria-expanded', 'false');
        });
      });
    }

    var nav = $('#nav');
    if (nav) {
      var kaydirma = function () { nav.classList.toggle('stuck', window.scrollY > 8); };
      kaydirma();
      window.addEventListener('scroll', kaydirma, { passive: true });
    }
  }

  /* ------------------------------------------------------------ reveal */
  function revealKur() {
    var az = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var hedef = $$('.reveal');
    if (az || !('IntersectionObserver' in window)) {
      hedef.forEach(function (el) { el.classList.add('in'); });
      return;
    }
    var go = new IntersectionObserver(function (girisler) {
      girisler.forEach(function (g) {
        if (g.isIntersecting) { g.target.classList.add('in'); go.unobserve(g.target); }
      });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
    hedef.forEach(function (el) { go.observe(el); });
  }

  /* --------------------------------------------------------------- baslat */
  function baslat() {
    var kayitli = null;
    try { kayitli = localStorage.getItem('nw_lang'); } catch (e) {}
    var tarayici = (navigator.language || 'tr').slice(0, 2).toLowerCase();
    var secili = kayitli || (DILLER.indexOf(tarayici) >= 0 ? tarayici : VARSAYILAN);

    ozellikleriCiz();
    SSS_ciz();
    olaylariBagla();
    dilUygula(secili);
    changelogCiz();
    surumOku();
    revealKur();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', baslat);
  else baslat();
})();
