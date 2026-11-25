// 모든 페이지가 같이 쓰는 스크립트: 다운로드 주소, OS 알아보기, 상단 메뉴, 나타나기 효과.
(function(){
  var C = window.DOWNLOAD_CONFIG;
  var ICON = {
    windows:'<svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 5.5 10.5 4.5v7H3zM11.5 4.3 21 3v8.5h-9.5zM3 12.5h7.5v7L3 18.5zM11.5 12.5H21V21l-9.5-1.3z"/></svg>',
    mac:'<svg viewBox="0 0 24 24" fill="currentColor"><path d="M16.4 12.6c0-2.4 2-3.6 2.1-3.6-1.1-1.7-2.9-1.9-3.5-1.9-1.5-.2-2.9.9-3.7.9-.8 0-1.9-.9-3.2-.8-1.6 0-3.1 1-4 2.4-1.7 3-.4 7.3 1.2 9.7.8 1.2 1.8 2.5 3 2.4 1.2 0 1.7-.8 3.1-.8 1.5 0 1.9.8 3.2.8 1.3 0 2.2-1.2 3-2.4.9-1.4 1.3-2.7 1.3-2.8 0 0-2.5-1-2.5-3.9zM14 5.5c.7-.8 1.1-1.9 1-3-1 0-2.1.7-2.8 1.5-.6.7-1.2 1.8-1 2.9 1.1.1 2.1-.6 2.8-1.4z"/></svg>'
  };
  var NAME = { windows:'Windows', mac:'macOS' };

  function url(os){ return (C.overrideUrls && C.overrideUrls[os]) || 'https://github.com/' + C.repo + '/releases/latest/download/' + C.files[os]; }
  function detect(){
    var ua = navigator.userAgent || '';
    if (/iPhone|iPad|iPod|Android/i.test(ua) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua))) return 'mobile';
    if (/Mac/i.test(ua)) return 'mac';
    if (/Win/i.test(ua)) return 'windows';
    return 'other';
  }
  var os = detect();
  var desktop = os === 'windows' || os === 'mac';

  window.Site = { config: C, icon: ICON, osName: NAME, url: url, os: os, desktop: desktop,
    reduce: matchMedia('(prefers-reduced-motion: reduce)').matches,
    releasesApi: 'https://api.github.com/repos/' + C.repo + '/releases',
    releasesPage: 'https://github.com/' + C.repo + '/releases' };

  // 다운로드 버튼들: data-dl="windows|mac" 은 그 OS 파일, data-dl="auto" 는 내 OS 파일(모르면 다운로드 절로)
  document.querySelectorAll('[data-dl]').forEach(function(a){
    var k = a.dataset.dl;
    if (k === 'auto'){ if (desktop) a.href = url(os); }
    else a.href = url(k);
  });
  document.querySelectorAll('[data-icon]').forEach(function(s){ s.innerHTML = ICON[s.dataset.icon] || ''; });
  document.querySelectorAll('[data-os-icon]').forEach(function(s){ if (desktop) s.innerHTML = ICON[os]; });
  document.querySelectorAll('[data-os-label]').forEach(function(s){ if (desktop) s.textContent = NAME[os] + s.dataset.osLabel; });
  document.querySelectorAll('[data-releases]').forEach(function(a){ a.href = Site.releasesPage; });

  // 상단 메뉴: 스크롤하면 배경, 휴대폰에서는 펼침 메뉴
  var nav = document.querySelector('nav.site');
  if (nav){
    var home = document.body.classList.contains('home');
    var onScroll = function(){ nav.classList.toggle('solid', scrollY > (home ? innerHeight * 0.6 : 40)); };
    addEventListener('scroll', onScroll, {passive:true}); onScroll();
    var btn = nav.querySelector('.menu-btn');
    if (btn) btn.addEventListener('click', function(){
      var open = nav.classList.toggle('open');
      btn.setAttribute('aria-expanded', open);
    });
    nav.querySelectorAll('.nav-links a').forEach(function(a){ a.addEventListener('click', function(){ nav.classList.remove('open'); }); });
  }

  // 나타나기
  var io = new IntersectionObserver(function(es){
    es.forEach(function(e){ if (e.isIntersecting){ e.target.classList.add('in'); io.unobserve(e.target); } });
  }, {threshold:.12});
  document.querySelectorAll('.reveal').forEach(function(el){ io.observe(el); });
})();
