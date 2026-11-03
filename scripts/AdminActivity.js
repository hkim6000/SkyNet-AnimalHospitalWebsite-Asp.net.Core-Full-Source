var AdminActivityJs = (function () {

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
        $ApiRequest('AdminActivity/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('al-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('al-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('al-open');
        }, 3400);
    }

    function openNav() {
        el('al-nav').classList.add('al-open');
        el('al-scrim').classList.add('al-open');
    }

    function closeNav() {
        el('al-nav').classList.remove('al-open');
        el('al-scrim').classList.remove('al-open');
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
        var open = !box.classList.contains('al-sopen');
        box.classList.toggle('al-sopen', open);
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
        var all = document.querySelectorAll('.al-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('al-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.al-sec.al-shere');
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
        var m = el('al-menu');
        var w = el('al-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminActivityJs.').join('{js}.').split('al-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('al-').join('{p}-'));
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
        var n = el('al-nav');
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
        var a = n.querySelector('.al-mi.al-on');
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
        if (el('al-menu')) {
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
            if (el('al-menu')) {
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
        var n = el('al-nav');
        var m = el('al-menu');
        var w = el('al-who');
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
            w.innerHTML = wh.split('{p}-').join('al-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminActivityJs.').split('{p}-').join('al-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.al-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('al-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.al-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.al-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('al-shere', here);
                all[j].classList.toggle('al-sopen', open);
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
        el('al-mbody').innerHTML = html;
        el('al-modal').classList.add('al-open');
        document.body.style.overflow = 'hidden';
        times(el('al-mbody'));
    }

    function closeModal() {
        el('al-modal').classList.remove('al-open');
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
        el('al-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var ftimer = null;
    var curPg = 1;

    function lock(b) {
        if (locked) {
            return false;
        }
        locked = true;
        if (b) {
            b.disabled = true;
            b.setAttribute('data-allk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 30000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-allk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-allk');
        }
    }

    function site(k) {
        var s = el('al-site');
        return s ? (s.getAttribute('data-' + k) || '') : '';
    }

    function reveal() {
        call('Load', [{ key: 'openstaff', vlu: site('qs') }]);
    }

    function ready() {
        times(el('al-list'));
    }

    function filters(pg) {
        return [
            { key: 'who', vlu: val('al-fwho') },
            { key: 'dfrom', vlu: val('al-dfrom') },
            { key: 'dto', vlu: val('al-dto') },
            { key: 'term', vlu: val('al-term') },
            { key: 'failed', vlu: el('al-ffail') && el('al-ffail').checked ? '1' : '0' },
            { key: 'pg', vlu: String(pg || 1) }
        ];
    }

    function find(pg) {
        var p = pg || 1;
        var moved = p !== curPg;
        curPg = p;
        var w = val('al-fwho');
        history.replaceState(null, '', 'AdminActivity' + (w ? '?s=' + encodeURIComponent(w) : ''));
        call('Find', filters(curPg));
        if (moved) {
            var t = el('al-list');
            if (t && t.getBoundingClientRect().top < 0) {
                t.scrollIntoView();
            }
        }
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            find(1);
        }, 350);
    }

    function csv(b) {
        if (!lock(b)) {
            return;
        }
        toast('Getting the list ready…');
        call('Csv', filters(1));
    }

    function csvGo(capped) {
        var rows = document.querySelectorAll('#al-csvall i');
        if (!rows.length) {
            el('al-csvall').innerHTML = '';
            toast('There is nothing in the list to download.');
            return;
        }
        var lines = ['Local time,UTC time,Who,Action,Target,Result,IP address'];
        for (var i = 0; i < rows.length; i++) {
            lines.push(rows[i].getAttribute('data-csv') || '');
        }
        el('al-csvall').innerHTML = '';
        var blob = new Blob(['﻿' + lines.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        var d = new Date();
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        a.href = URL.createObjectURL(blob);
        a.download = 'activity-' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        toast(rows.length + (rows.length === 1 ? ' entry downloaded.' : ' entries downloaded.') + (capped ? ' That is the most at once; narrow the dates for older entries.' : ''));
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        sec: sec,
        secs: secs,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        find: find,
        typed: typed,
        csv: csv,
        csvGo: csvGo
    };
})();

AdminActivityJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminActivityJs.navPaint();
        AdminActivityJs.reveal();
    });
} else {
    AdminActivityJs.navPaint();
    AdminActivityJs.reveal();
}
