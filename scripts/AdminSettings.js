var AdminSettingsJs = (function () {

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
        $ApiRequest('AdminSettings/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('st-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('st-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('st-open');
        }, 3400);
    }

    function openNav() {
        el('st-nav').classList.add('st-open');
        el('st-scrim').classList.add('st-open');
    }

    function closeNav() {
        el('st-nav').classList.remove('st-open');
        el('st-scrim').classList.remove('st-open');
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
        var open = !box.classList.contains('st-sopen');
        box.classList.toggle('st-sopen', open);
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
        var all = document.querySelectorAll('.st-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('st-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.st-sec.st-shere');
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
        var m = el('st-menu');
        var w = el('st-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminSettingsJs.').join('{js}.').split('st-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('st-').join('{p}-'));
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
        var n = el('st-nav');
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
        var a = n.querySelector('.st-mi.st-on');
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
        if (el('st-menu')) {
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
            if (el('st-menu')) {
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
        var n = el('st-nav');
        var m = el('st-menu');
        var w = el('st-who');
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
            w.innerHTML = wh.split('{p}-').join('st-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminSettingsJs.').split('{p}-').join('st-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.st-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('st-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.st-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.st-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('st-shere', here);
                all[j].classList.toggle('st-sopen', open);
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
        el('st-mbody').innerHTML = html;
        el('st-modal').classList.add('st-open');
        document.body.style.overflow = 'hidden';
        times(el('st-mbody'));
    }

    function closeModal() {
        el('st-modal').classList.remove('st-open');
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
        el('st-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var pkey = '';

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function hex() {
        var a = new Uint8Array(16);
        crypto.getRandomValues(a);
        return Array.prototype.map.call(a, function (b) {
            return ('0' + b.toString(16)).slice(-2);
        }).join('');
    }

    function lock(b) {
        if (locked) {
            return false;
        }
        locked = true;
        if (b) {
            b.disabled = true;
            b.setAttribute('data-lk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-lk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-lk');
        }
    }

    function shown(focus) {
        el('st-modal').classList.add('st-open');
        document.body.style.overflow = 'hidden';
        times(el('st-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function checked(cls) {
        var list = document.querySelectorAll('.' + cls);
        var out = [];
        for (var i = 0; i < list.length; i++) {
            if (list[i].checked) {
                out.push(list[i].value);
            }
        }
        return out.join(',');
    }

    function bit(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    var TABS = ['hospital', 'hours', 'bookings', 'boarding', 'reminders', 'billing', 'other', 'species'];

    function reveal() {
        call('Load', []);
    }

    function remembered() {
        var t = '';
        try {
            t = localStorage.getItem('ah_sttab') || '';
        } catch (e) {
            t = '';
        }
        var h = (location.hash || '').replace('#', '');
        if (TABS.indexOf(h) >= 0) {
            t = h;
        }
        return TABS.indexOf(t) >= 0 ? t : 'hospital';
    }

    function tab(k) {
        if (TABS.indexOf(k) < 0) {
            k = 'hospital';
        }
        for (var i = 0; i < TABS.length; i++) {
            var on = TABS[i] === k;
            var b = el('st-tab-' + TABS[i]);
            var p = el('st-p-' + TABS[i]);
            if (b) {
                b.setAttribute('aria-selected', on ? 'true' : 'false');
                b.classList.toggle('st-tabon', on);
                b.tabIndex = on ? 0 : -1;
            }
            if (p) {
                p.hidden = !on;
            }
        }
        try {
            localStorage.setItem('ah_sttab', k);
        } catch (e) {
            k = k + '';
        }
        history.replaceState(null, '', 'AdminSettings#' + k);
        var cur = el('st-tab-' + k);
        if (cur && cur.scrollIntoView && cur.parentNode.scrollWidth > cur.parentNode.clientWidth) {
            cur.parentNode.scrollLeft = Math.max(0, cur.offsetLeft - 16);
        }
    }

    function keys(e) {
        var b = e.target;
        if (!b || b.getAttribute('role') !== 'tab') {
            return;
        }
        var i = TABS.indexOf(b.id.replace('st-tab-', ''));
        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
            var n = (i + (e.key === 'ArrowRight' ? 1 : TABS.length - 1)) % TABS.length;
            tab(TABS[n]);
            el('st-tab-' + TABS[n]).focus();
            e.preventDefault();
        }
    }

    function ready() {
        unlock();
        tab(remembered());
        var t = document.querySelector('.st-tabs');
        if (t) {
            t.addEventListener('keydown', keys);
        }
    }

    function colour() {
        var v = el('st-accv');
        if (v) {
            v.textContent = val('st-f-accent').toUpperCase();
        }
    }

    function day(d) {
        var r = el('st-hr-' + d);
        var c = el('st-f-h' + d + '-on');
        if (r && c) {
            r.classList.toggle('st-closed', !c.checked);
        }
    }

    function collect(t) {
        var p = el('st-p-' + t);
        var out = [{ key: 'stab', vlu: t }];
        if (!p) {
            return out;
        }
        var list = p.querySelectorAll('[data-sk]');
        for (var i = 0; i < list.length; i++) {
            var e = list[i];
            var k = 'f_' + e.getAttribute('data-sk').replace(/\./g, '_');
            var v = '';
            if (e.getAttribute('data-multi') === '1') {
                var boxes = e.querySelectorAll('input[type="checkbox"]');
                var picked = [];
                for (var j = 0; j < boxes.length; j++) {
                    if (boxes[j].checked) {
                        picked.push(boxes[j].value);
                    }
                }
                v = picked.join(',');
            } else if (e.type === 'checkbox') {
                v = e.checked ? '1' : '0';
            } else {
                v = e.value || '';
            }
            out.push({ key: k, vlu: v });
        }
        return out;
    }

    function save(t, b) {
        if (!lock(b)) {
            return;
        }
        var e = el('st-e-tab-' + t);
        if (e) {
            e.textContent = '';
        }
        call('SaveTab', collect(t));
    }

    function saved(t, accent, name) {
        unlock();
        var s = el('st-site');
        if (s && accent) {
            s.style.setProperty('--st-accent', accent);
        }
        var b = document.querySelector('.st-abrand b');
        if (b && name) {
            b.textContent = name;
        }
        if (name) {
            document.title = 'Settings | ' + name;
        }
    }

    function spForm(k) {
        if (!lock(null)) {
            return;
        }
        call('SpForm', [{ key: 'spkey', vlu: k || '' }]);
    }

    function spDel(k) {
        if (!lock(null)) {
            return;
        }
        call('SpDelAsk', [{ key: 'spkey', vlu: k }]);
    }

    function spDelGo(k) {
        if (!lock(el('st-spdelgo'))) {
            return;
        }
        call('SpDelete', [{ key: 'spkey', vlu: k }]);
    }

    function spSave() {
        var f = el('st-spf');
        if (!lock(el('st-spgo'))) {
            return;
        }
        call('SpSave', [
            { key: 'spkey', vlu: f ? (f.getAttribute('data-spkey') || '') : '' },
            { key: 'spnew', vlu: val('st-spk').toLowerCase() },
            { key: 'spname', vlu: val('st-spn') },
            { key: 'spplural', vlu: val('st-spp') },
            { key: 'sporder', vlu: val('st-spo') },
            { key: 'spsize', vlu: bit('st-sps') },
            { key: 'spact', vlu: bit('st-spa') }
        ]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        tab: tab,
        colour: colour,
        day: day,
        save: save,
        saved: saved,
        shown: shown,
        spForm: spForm,
        spSave: spSave,
        spDel: spDel,
        spDelGo: spDelGo,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        sec: sec,
        secs: secs,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired
    };
})();

AdminSettingsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminSettingsJs.navPaint();
        AdminSettingsJs.reveal();
    });
} else {
    AdminSettingsJs.navPaint();
    AdminSettingsJs.reveal();
}
