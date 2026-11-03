var AdminReportsJs = (function () {

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
        $ApiRequest('AdminReports/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('rp-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rp-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rp-open');
        }, 3400);
    }

    function openNav() {
        el('rp-nav').classList.add('rp-open');
        el('rp-scrim').classList.add('rp-open');
    }

    function closeNav() {
        el('rp-nav').classList.remove('rp-open');
        el('rp-scrim').classList.remove('rp-open');
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
        var open = !box.classList.contains('rp-sopen');
        box.classList.toggle('rp-sopen', open);
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
        var all = document.querySelectorAll('.rp-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('rp-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.rp-sec.rp-shere');
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
        var m = el('rp-menu');
        var w = el('rp-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminReportsJs.').join('{js}.').split('rp-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('rp-').join('{p}-'));
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
        var n = el('rp-nav');
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
        var a = n.querySelector('.rp-mi.rp-on');
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
        if (el('rp-menu')) {
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
            if (el('rp-menu')) {
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
        var n = el('rp-nav');
        var m = el('rp-menu');
        var w = el('rp-who');
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
            w.innerHTML = wh.split('{p}-').join('rp-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminReportsJs.').split('{p}-').join('rp-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.rp-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('rp-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.rp-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.rp-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('rp-shere', here);
                all[j].classList.toggle('rp-sopen', open);
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
        el('rp-mbody').innerHTML = html;
        el('rp-modal').classList.add('rp-open');
        document.body.style.overflow = 'hidden';
        times(el('rp-mbody'));
    }

    function closeModal() {
        el('rp-modal').classList.remove('rp-open');
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
        el('rp-main').innerHTML = html;
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
        el('rp-modal').classList.add('rp-open');
        document.body.style.overflow = 'hidden';
        times(el('rp-mbody'));
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

    var kind = 'sales';
    var again = false;

    function reveal() {
        call('Load', []);
    }

    function ready(k, from, to, group) {
        unlock();
        var o = el('rp-out');
        if (o) {
            o.classList.remove('rp-busy');
        }
        if (again) {
            again = false;
            run();
            return;
        }
        kind = k || kind;
        var list = document.querySelectorAll('.rp-kind');
        for (var i = 0; i < list.length; i++) {
            var on = list[i].id === 'rp-k-' + kind;
            list[i].classList.toggle('rp-kon', on);
            list[i].setAttribute('aria-pressed', on ? 'true' : 'false');
        }
        if (from && el('rp-from')) {
            el('rp-from').value = from;
        }
        if (to && el('rp-to')) {
            el('rp-to').value = to;
        }
        if (group && el('rp-group')) {
            el('rp-group').value = group;
        }
        var bar = el('rp-bar');
        if (bar) {
            bar.classList.toggle('rp-owed', kind === 'owed');
            bar.classList.toggle('rp-nogroup', kind === 'boarding');
        }
    }

    function params() {
        return [
            { key: 'rkind', vlu: kind },
            { key: 'rpreset', vlu: val('rp-preset') },
            { key: 'rfrom', vlu: val('rp-from') },
            { key: 'rto', vlu: val('rp-to') },
            { key: 'rgroup', vlu: val('rp-group') }
        ];
    }

    function run() {
        if (!lock(null)) {
            again = true;
            return;
        }
        var o = el('rp-out');
        if (o) {
            o.classList.add('rp-busy');
        }
        call('Run', params());
    }

    function pick(k) {
        kind = k;
        var list = document.querySelectorAll('.rp-kind');
        for (var i = 0; i < list.length; i++) {
            var on = list[i].id === 'rp-k-' + kind;
            list[i].classList.toggle('rp-kon', on);
            list[i].setAttribute('aria-pressed', on ? 'true' : 'false');
        }
        run();
    }

    function failed() {
        unlock();
        var o = el('rp-out');
        if (o) {
            o.classList.remove('rp-busy');
        }
        if (again) {
            again = false;
            run();
        }
    }

    function preset() {
        if (val('rp-preset') === 'custom') {
            var f = el('rp-from');
            if (f) {
                f.focus();
            }
            return;
        }
        if (el('rp-group')) {
            el('rp-group').value = '';
        }
        run();
    }

    function custom() {
        if (el('rp-preset')) {
            el('rp-preset').value = 'custom';
        }
        if (val('rp-from') && val('rp-to')) {
            if (el('rp-group')) {
                el('rp-group').value = '';
            }
            run();
        }
    }

    function csv() {
        var box = el('rp-csv');
        var rows = box ? box.querySelectorAll('i') : [];
        if (!rows.length) {
            toast('There is nothing to download.');
            return;
        }
        var lines = [];
        for (var i = 0; i < rows.length; i++) {
            lines.push(rows[i].getAttribute('data-csv') || '');
        }
        var name = box.getAttribute('data-name') || 'report.csv';
        var blob = new Blob(['﻿' + lines.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        call('CsvLog', [{ key: 'rfile', vlu: name }]);
        toast('Downloaded ' + name);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        failed: failed,
        unlock: unlock,
        run: run,
        pick: pick,
        preset: preset,
        custom: custom,
        csv: csv,
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

AdminReportsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminReportsJs.navPaint();
        AdminReportsJs.reveal();
    });
} else {
    AdminReportsJs.navPaint();
    AdminReportsJs.reveal();
}
