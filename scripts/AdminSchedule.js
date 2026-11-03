var AdminScheduleJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function call(method, list) {
        var data = (list || []).slice();
        data.push({ key: 'tok', vlu: getCookie('ah_tok') });
        $ApiRequest('AdminSchedule/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('sc-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('sc-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sc-open');
        }, 3400);
    }

    function openNav() {
        el('sc-nav').classList.add('sc-open');
        el('sc-scrim').classList.add('sc-open');
    }

    function closeNav() {
        el('sc-nav').classList.remove('sc-open');
        el('sc-scrim').classList.remove('sc-open');
    }

    function openSecs() {
        try {
            return JSON.parse(localStorage.getItem('ah_secs') || '[]');
        } catch (e) {
            return [];
        }
    }

    function sec(btn) {
        var box = btn.parentNode;
        var open = !box.classList.contains('sc-sopen');
        box.classList.toggle('sc-sopen', open);
        btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        var key = box.getAttribute('data-sec');
        var list = openSecs().filter(function (k) { return k !== key; });
        if (open) {
            list.push(key);
        }
        try {
            localStorage.setItem('ah_secs', JSON.stringify(list));
        } catch (e) {
            return;
        }
    }

    function secs() {
        var list = openSecs();
        var all = document.querySelectorAll('.sc-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('sc-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.sc-sec.sc-shere');
        var k = h ? h.getAttribute('data-sec') : '';
        if (!k || list.indexOf(k) >= 0) {
            return;
        }
        list.push(k);
        try {
            localStorage.setItem('ah_secs', JSON.stringify(list));
        } catch (e) {
            return;
        }
    }

    function navSave() {
        var m = el('sc-menu');
        var w = el('sc-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminScheduleJs.').join('{js}.').split('sc-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('sc-').join('{p}-'));
            }
        } catch (e) {
            return;
        }
    }

    function navClear() {
        try {
            sessionStorage.removeItem('ah_menu');
            sessionStorage.removeItem('ah_who');
            sessionStorage.removeItem('ah_navy');
        } catch (e) {
            return;
        }
    }

    function navY() {
        var n = el('sc-nav');
        if (!n) {
            return;
        }
        var y = 0;
        try {
            y = parseInt(sessionStorage.getItem('ah_navy') || '0', 10) || 0;
        } catch (e) {
            y = 0;
        }
        n.scrollTop = y;
        var a = n.querySelector('.sc-mi.sc-on');
        if (!a) {
            return;
        }
        var r = a.getBoundingClientRect();
        var b = n.getBoundingClientRect();
        if (r.top < b.top) {
            n.scrollTop -= b.top - r.top + 12;
        } else if (r.bottom > b.bottom) {
            n.scrollTop += r.bottom - b.bottom + 12;
        }
    }

    function navWatch() {
        if (el('sc-menu')) {
            navPaint();
            return;
        }
        if (!window.MutationObserver) {
            return;
        }
        var o = new MutationObserver(function () {
            if (navDone) {
                o.disconnect();
                return;
            }
            if (el('sc-menu')) {
                o.disconnect();
                navPaint();
            }
        });
        o.observe(document.documentElement, { childList: true, subtree: true });
    }

    function navPaint() {
        if (navDone) {
            return;
        }
        var n = el('sc-nav');
        var m = el('sc-menu');
        var w = el('sc-who');
        if (!n || !m) {
            return;
        }
        navDone = true;
        var mh = '';
        var wh = '';
        try {
            mh = sessionStorage.getItem('ah_menu') || '';
            wh = sessionStorage.getItem('ah_who') || '';
        } catch (e) {
            mh = '';
        }
        if (w && wh && !w.innerHTML.trim()) {
            w.innerHTML = wh.split('{p}-').join('sc-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminScheduleJs.').split('{p}-').join('sc-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.sc-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('sc-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.sc-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.sc-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('sc-shere', here);
                all[j].classList.toggle('sc-sopen', open);
                all[j].firstChild.setAttribute('aria-expanded', open ? 'true' : 'false');
            }
        }
        navY();
        if (window.requestAnimationFrame) {
            requestAnimationFrame(navY);
        }
        n.addEventListener('scroll', function () {
            try {
                sessionStorage.setItem('ah_navy', String(n.scrollTop));
            } catch (e) {
                return;
            }
        });
    }

    function openModal(html) {
        el('sc-mbody').innerHTML = html;
        el('sc-modal').classList.add('sc-open');
        document.body.style.overflow = 'hidden';
        times(el('sc-mbody'));
    }

    function closeModal() {
        el('sc-modal').classList.remove('sc-open');
        document.body.style.overflow = '';
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    function signOut() {
        call('SignOut', []);
    }

    function out() {
        navClear();
        setCookie('ah_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function expired() {
        out();
    }

    function denied(html) {
        el('sc-main').innerHTML = html;
    }

    var st = { day: '', staff: '', today: '', def: '', all: false, cxl: false, keep: null };
    var loadedAt = Date.now();

    function remember(k, v) {
        try {
            localStorage.setItem(k, v);
        } catch (e) {
            return;
        }
    }

    function recall(k) {
        try {
            return localStorage.getItem(k) || '';
        } catch (e) {
            return '';
        }
    }

    function pull(quiet) {
        var w = el('sc-wrap');
        st.keep = quiet && w ? { t: w.scrollTop, l: w.scrollLeft } : null;
        call('Load', [
            { key: 'day', vlu: st.day },
            { key: 'staff', vlu: st.staff },
            { key: 'everyone', vlu: st.all ? '1' : '' },
            { key: 'cancelled', vlu: st.cxl ? '1' : '' },
            { key: 'quiet', vlu: quiet ? '1' : '' }
        ]);
    }

    function go(day, staff) {
        st.day = day;
        st.staff = staff;
        pull(false);
    }

    function shift(ymd, n) {
        var p = (ymd || st.today).split('-');
        var d = new Date(Date.UTC(+p[0], +p[1] - 1, +p[2] + n));
        return d.toISOString().slice(0, 10);
    }

    function step(n) {
        go(shift(st.day, n * (st.staff ? 7 : 1)), st.staff);
    }

    function today() {
        go(st.today, st.staff);
    }

    function pick() {
        var v = val('sc-date');
        if (/^\d{4}-\d{2}-\d{2}$/.test(v)) {
            go(v, st.staff);
        }
    }

    function who() {
        go(st.day, val('sc-staff'));
    }

    function mode(m) {
        if (m === 'day') {
            go(st.day, '');
        } else {
            go(st.day, st.staff || st.def);
        }
    }

    function toggles() {
        if (el('sc-all')) {
            st.all = el('sc-all').checked;
            remember('sc_all', st.all ? '1' : '');
        }
        if (el('sc-cxl')) {
            st.cxl = el('sc-cxl').checked;
            remember('sc_cxl', st.cxl ? '1' : '');
        }
        pull(true);
    }

    function now() {
        var g = el('sc-grid');
        if (!g || g.getAttribute('data-live') !== '1') {
            return;
        }
        var from = parseFloat(g.getAttribute('data-from'));
        var to = parseFloat(g.getAttribute('data-to'));
        var ppm = parseFloat(g.getAttribute('data-ppm'));
        var m = parseFloat(g.getAttribute('data-now')) + (Date.now() - loadedAt) / 60000;
        var list = g.querySelectorAll('.sc-now');
        for (var i = 0; i < list.length; i++) {
            list[i].hidden = m < from || m > to;
            list[i].style.top = ((m - from) * ppm).toFixed(1) + 'px';
        }
    }

    function ready(day, staff, td, def) {
        st.day = day;
        st.staff = staff;
        st.today = td;
        st.def = def;
        loadedAt = Date.now();
        history.replaceState(null, '', 'AdminSchedule?d=' + day + (staff ? '&s=' + encodeURIComponent(staff) : ''));
        var w = el('sc-wrap');
        if (w) {
            if (st.keep) {
                w.scrollTop = st.keep.t;
                w.scrollLeft = st.keep.l;
            } else {
                w.scrollTop = parseInt(w.getAttribute('data-focus') || '0', 10);
                var tc = w.querySelector('.sc-chtoday');
                var cn = w.querySelector('.sc-corner');
                if (tc && cn) {
                    w.scrollLeft = Math.max(0, tc.offsetLeft - cn.offsetWidth);
                }
            }
        }
        st.keep = null;
        now();
    }

    function poll() {
        var g = el('sc-grid');
        var m = el('sc-modal');
        if (document.visibilityState !== 'visible' || !g || g.getAttribute('data-live') !== '1' || (m && m.classList.contains('sc-open'))) {
            return;
        }
        pull(true);
    }

    function reveal() {
        var site = el('sc-site');
        st.day = site ? site.getAttribute('data-d') || '' : '';
        st.staff = site ? site.getAttribute('data-s') || '' : '';
        st.all = recall('sc_all') === '1';
        st.cxl = recall('sc_cxl') === '1';
        pull(false);
        setInterval(now, 30000);
        setInterval(poll, 120000);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        mode: mode,
        step: step,
        today: today,
        pick: pick,
        who: who,
        toggles: toggles,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        openModal: openModal,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminScheduleJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminScheduleJs.navPaint();
        AdminScheduleJs.reveal();
    });
} else {
    AdminScheduleJs.navPaint();
    AdminScheduleJs.reveal();
}
