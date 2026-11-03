var AdminResultsJs = (function () {

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
        $ApiRequest('AdminResults/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('rs-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rs-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rs-open');
        }, 3400);
    }

    function openNav() {
        el('rs-nav').classList.add('rs-open');
        el('rs-scrim').classList.add('rs-open');
    }

    function closeNav() {
        el('rs-nav').classList.remove('rs-open');
        el('rs-scrim').classList.remove('rs-open');
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
        var open = !box.classList.contains('rs-sopen');
        box.classList.toggle('rs-sopen', open);
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
        var all = document.querySelectorAll('.rs-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('rs-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.rs-sec.rs-shere');
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
        var m = el('rs-menu');
        var w = el('rs-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminResultsJs.').join('{js}.').split('rs-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('rs-').join('{p}-'));
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
        var n = el('rs-nav');
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
        var a = n.querySelector('.rs-mi.rs-on');
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
        if (el('rs-menu')) {
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
            if (el('rs-menu')) {
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
        var n = el('rs-nav');
        var m = el('rs-menu');
        var w = el('rs-who');
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
            w.innerHTML = wh.split('{p}-').join('rs-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminResultsJs.').split('{p}-').join('rs-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.rs-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('rs-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.rs-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.rs-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('rs-shere', here);
                all[j].classList.toggle('rs-sopen', open);
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
        el('rs-mbody').innerHTML = html;
        el('rs-modal').classList.add('rs-open');
        document.body.style.overflow = 'hidden';
        times(el('rs-mbody'));
    }

    function closeModal() {
        el('rs-modal').classList.remove('rs-open');
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
        el('rs-main').innerHTML = html;
    }

    function reveal() {
        call('Load', []);
    }

    function ready() {
        times(el('rs-main'));
    }

    function flagged() {
        var f = el('rs-fflag');
        return f && f.checked ? '1' : '';
    }

    function filters() {
        return [{ key: 'ftest', vlu: val('rs-ftest') }, { key: 'fstaff', vlu: val('rs-fstaff') }, { key: 'fflag', vlu: flagged() }];
    }

    function recent() {
        call('Recent', [{ key: 'fflag', vlu: flagged() }]);
    }

    function filter() {
        call('Filter', filters());
    }

    function shown() {
        el('rs-modal').classList.add('rs-open');
        document.body.style.overflow = 'hidden';
        times(el('rs-mbody'));
        var f = document.querySelector('#rs-rbox .rs-rin');
        if (f) {
            f.focus();
        }
    }

    function enter(id) {
        call('ResultForm', filters().concat([{ key: 'lineid', vlu: id }]));
    }

    function flag(inp) {
        var v = parseFloat(inp.value);
        var lo = parseFloat(inp.getAttribute('data-lo'));
        var hi = parseFloat(inp.getAttribute('data-hi'));
        inp.classList.remove('rs-fh', 'rs-fl');
        if (inp.value.trim() === '' || isNaN(v)) {
            return;
        }
        if (!isNaN(lo) && v < lo) {
            inp.classList.add('rs-fl');
        } else if (!isNaN(hi) && v > hi) {
            inp.classList.add('rs-fh');
        }
    }

    function unlock() {
        var b = el('rs-rsave');
        if (b) {
            b.disabled = false;
        }
    }

    function save(id) {
        var b = el('rs-rsave');
        if (b) {
            if (b.disabled) {
                return;
            }
            b.disabled = true;
        }
        var list = [];
        var ins = document.querySelectorAll('#rs-rbox [data-k]');
        for (var i = 0; i < ins.length; i++) {
            list.push({ k: ins[i].getAttribute('data-k'), v: ins[i].value });
        }
        call('ResultSave', filters().concat([{ key: 'lineid', vlu: id }, { key: 'rvals', vlu: JSON.stringify(list) }]));
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        filter: filter,
        recent: recent,
        shown: shown,
        enter: enter,
        flag: flag,
        save: save,
        unlock: unlock,
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

AdminResultsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminResultsJs.navPaint();
        AdminResultsJs.reveal();
    });
} else {
    AdminResultsJs.navPaint();
    AdminResultsJs.reveal();
}
