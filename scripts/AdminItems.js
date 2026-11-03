var AdminItemsJs = (function () {

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
        $ApiRequest('AdminItems/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('it-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('it-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('it-open');
        }, 3400);
    }

    function openNav() {
        el('it-nav').classList.add('it-open');
        el('it-scrim').classList.add('it-open');
    }

    function closeNav() {
        el('it-nav').classList.remove('it-open');
        el('it-scrim').classList.remove('it-open');
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
        var open = !box.classList.contains('it-sopen');
        box.classList.toggle('it-sopen', open);
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
        var all = document.querySelectorAll('.it-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('it-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.it-sec.it-shere');
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
        var m = el('it-menu');
        var w = el('it-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminItemsJs.').join('{js}.').split('it-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('it-').join('{p}-'));
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
        var n = el('it-nav');
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
        var a = n.querySelector('.it-mi.it-on');
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
        if (el('it-menu')) {
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
            if (el('it-menu')) {
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
        var n = el('it-nav');
        var m = el('it-menu');
        var w = el('it-who');
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
            w.innerHTML = wh.split('{p}-').join('it-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminItemsJs.').split('{p}-').join('it-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.it-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('it-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.it-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.it-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('it-shere', here);
                all[j].classList.toggle('it-sopen', open);
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
        el('it-mbody').innerHTML = html;
        el('it-modal').classList.add('it-open');
        document.body.style.overflow = 'hidden';
        times(el('it-mbody'));
    }

    function closeModal() {
        el('it-modal').classList.remove('it-open');
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
        el('it-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var ftimer = null;
    var curTyp = '';
    var curPg = 1;
    var nkey = '';
    var ptok = '';

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
            b.setAttribute('data-itlk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-itlk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-itlk');
        }
    }

    function reveal() {
        call('Load', []);
    }

    function ready() {
        times(el('it-main'));
    }

    function filters(pg) {
        return [
            { key: 'ityp', vlu: curTyp },
            { key: 'icat', vlu: val('it-cat') },
            { key: 'term', vlu: val('it-term') },
            { key: 'show', vlu: val('it-show') || 'active' },
            { key: 'pg', vlu: String(pg || 1) }
        ];
    }

    function find(pg, retab) {
        curPg = pg || 1;
        var list = filters(curPg);
        list.push({ key: 'retab', vlu: retab || el('it-show').getAttribute('data-was') !== val('it-show') ? '1' : '' });
        el('it-show').setAttribute('data-was', val('it-show'));
        call('List', list);
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            find(1);
        }, 350);
    }

    function tab(t) {
        curTyp = t;
        var c = el('it-cat');
        if (c) {
            c.value = '';
        }
        find(1, true);
    }

    function open(id) {
        if (!lock(null)) {
            return;
        }
        call('Form', [{ key: 'itemid', vlu: id }]);
    }

    function add(b) {
        if (!lock(b)) {
            return;
        }
        call('Form', [{ key: 'itemid', vlu: '' }, { key: 'ityp', vlu: curTyp }]);
    }

    function shown(isNew) {
        el('it-modal').classList.add('it-open');
        document.body.style.overflow = 'hidden';
        if (isNew) {
            nkey = hex();
            var f = el('it-f-name');
            if (f) {
                f.focus();
            }
        }
    }

    function typeChg() {
        var t = val('it-f-type');
        el('it-medw').hidden = t !== 'medicine';
        el('it-vaxw').hidden = t !== 'vaccine';
    }

    function checked(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function species() {
        var all = document.querySelectorAll('input[name="it-f-sp"]:checked');
        var out = [];
        for (var i = 0; i < all.length; i++) {
            out.push(all[i].value);
        }
        return out.join(',');
    }

    function save(force) {
        if (!lock(el('it-fsave'))) {
            return false;
        }
        var id = val('it-f-id');
        call('Save', [
            { key: 'force', vlu: force === 1 ? '1' : '' },
            { key: 'itemid', vlu: id },
            { key: 'newid', vlu: id ? '' : nkey },
            { key: 'name', vlu: val('it-f-name') },
            { key: 'code', vlu: val('it-f-code') },
            { key: 'ftype', vlu: val('it-f-type') },
            { key: 'cat', vlu: val('it-f-cat') },
            { key: 'unit', vlu: val('it-f-unit') },
            { key: 'price', vlu: val('it-f-price') },
            { key: 'pm', vlu: val('it-f-pm') },
            { key: 'pl', vlu: val('it-f-pl') },
            { key: 'tax', vlu: checked('it-f-tax') },
            { key: 'species', vlu: species() },
            { key: 'dose', vlu: val('it-f-dose') },
            { key: 'inst', vlu: el('it-f-inst') ? el('it-f-inst').value : '' },
            { key: 'vax', vlu: val('it-f-vax') },
            { key: 'stock', vlu: checked('it-f-stock') },
            { key: 'order', vlu: val('it-f-order') },
            { key: 'note', vlu: val('it-f-note') },
            { key: 'active', vlu: checked('it-f-active') }
        ]);
        return false;
    }

    function bad() {
        var all = document.querySelectorAll('#it-mbody .it-err');
        for (var i = 0; i < all.length; i++) {
            if (all[i].textContent) {
                all[i].scrollIntoView({ block: 'center', behavior: 'smooth' });
                return;
            }
        }
    }

    function saved(msg) {
        unlock();
        closeModal();
        toast(msg);
        find(curPg, true);
    }

    function ask(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Ask', [{ key: 'itemid', vlu: id }]);
    }

    function remove(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Remove', [{ key: 'itemid', vlu: id }]);
    }

    function retire(id, b, force) {
        if (!lock(b)) {
            return;
        }
        call('Retire', [{ key: 'itemid', vlu: id }, { key: 'force', vlu: force === 1 ? '1' : '' }]);
    }

    function prices(b) {
        if (!lock(b)) {
            return;
        }
        call('PriceForm', filters(1));
    }

    function bulk() {
        var list = filters(1);
        list.push({ key: 'pdir', vlu: val('it-pdir') });
        list.push({ key: 'pmode', vlu: val('it-pmode') });
        list.push({ key: 'pval', vlu: val('it-pval') });
        list.push({ key: 'pround', vlu: val('it-pround') });
        return list;
    }

    function pdirty() {
        ptok = '';
        var p = el('it-pprev');
        if (p) {
            p.innerHTML = '';
        }
        var v = el('it-pval');
        if (v) {
            v.placeholder = val('it-pmode') === 'amt' ? '2.00' : '5';
        }
    }

    function pview(b) {
        if (!lock(b)) {
            return;
        }
        call('PricePreview', bulk());
    }

    function previewed(t) {
        ptok = t;
        var p = el('it-pprev');
        if (p && p.firstChild) {
            p.scrollIntoView({ block: 'start', behavior: 'smooth' });
        }
    }

    function papply(b) {
        if (!ptok || !lock(b)) {
            return;
        }
        var list = bulk();
        list.push({ key: 'ptok', vlu: ptok });
        call('PriceApply', list);
    }

    function csv(b) {
        if (!lock(b)) {
            return;
        }
        toast('Getting the list ready…');
        call('Csv', filters(1));
    }

    function csvGo() {
        var rows = document.querySelectorAll('#it-csvall i');
        if (!rows.length) {
            toast('There is nothing in the list to download.');
            return;
        }
        var lines = ['Code,Name,Type,Category,Unit,Price,Medium pet price,Large pet price,Taxable,Species,Dose,Instructions,Vaccine months,Track stock,Order,Status,Note'];
        for (var i = 0; i < rows.length; i++) {
            lines.push(rows[i].getAttribute('data-csv') || '');
        }
        el('it-csvall').innerHTML = '';
        var blob = new Blob(['﻿' + lines.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        var d = new Date();
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        a.href = URL.createObjectURL(blob);
        a.download = 'items-' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        toast(rows.length + (rows.length === 1 ? ' item downloaded.' : ' items downloaded.'));
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        find: find,
        typed: typed,
        tab: tab,
        open: open,
        add: add,
        shown: shown,
        typeChg: typeChg,
        save: save,
        bad: bad,
        saved: saved,
        ask: ask,
        remove: remove,
        retire: retire,
        prices: prices,
        pdirty: pdirty,
        pview: pview,
        previewed: previewed,
        papply: papply,
        csv: csv,
        csvGo: csvGo,
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

AdminItemsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminItemsJs.navPaint();
        AdminItemsJs.reveal();
    });
} else {
    AdminItemsJs.navPaint();
    AdminItemsJs.reveal();
}
