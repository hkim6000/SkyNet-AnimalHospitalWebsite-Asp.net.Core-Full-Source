var AdminTestsJs = (function () {

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
        $ApiRequest('AdminTests/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('ts-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ts-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ts-open');
        }, 3400);
    }

    function openNav() {
        el('ts-nav').classList.add('ts-open');
        el('ts-scrim').classList.add('ts-open');
    }

    function closeNav() {
        el('ts-nav').classList.remove('ts-open');
        el('ts-scrim').classList.remove('ts-open');
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
        var open = !box.classList.contains('ts-sopen');
        box.classList.toggle('ts-sopen', open);
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
        var all = document.querySelectorAll('.ts-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('ts-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.ts-sec.ts-shere');
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
        var m = el('ts-menu');
        var w = el('ts-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminTestsJs.').join('{js}.').split('ts-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('ts-').join('{p}-'));
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
        var n = el('ts-nav');
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
        var a = n.querySelector('.ts-mi.ts-on');
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
        if (el('ts-menu')) {
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
            if (el('ts-menu')) {
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
        var n = el('ts-nav');
        var m = el('ts-menu');
        var w = el('ts-who');
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
            w.innerHTML = wh.split('{p}-').join('ts-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminTestsJs.').split('{p}-').join('ts-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.ts-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('ts-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.ts-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.ts-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('ts-shere', here);
                all[j].classList.toggle('ts-sopen', open);
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
        el('ts-mbody').innerHTML = html;
        el('ts-modal').classList.add('ts-open');
        document.body.style.overflow = 'hidden';
        times(el('ts-mbody'));
    }

    function closeModal() {
        el('ts-modal').classList.remove('ts-open');
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
        el('ts-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var curTest = '';
    var nkey = '';

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
            b.setAttribute('data-tslk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-tslk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-tslk');
        }
    }

    function reveal() {
        call('Load', []);
    }

    function ready(id, jump) {
        curTest = id || '';
        if (jump && window.innerWidth <= 900) {
            var p = el('ts-pane');
            if (p) {
                p.scrollIntoView({ block: 'start', behavior: 'smooth' });
            }
        }
    }

    function pick(id) {
        if (!lock(null)) {
            return;
        }
        call('Pick', [{ key: 'testid', vlu: id }, { key: 'jump', vlu: '1' }]);
    }

    function open(id) {
        if (!lock(null)) {
            return;
        }
        call('Form', [{ key: 'fieldid', vlu: id }]);
    }

    function add(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Form', [{ key: 'fieldid', vlu: '' }, { key: 'testid', vlu: id }]);
    }

    function shown(isNew) {
        el('ts-modal').classList.add('ts-open');
        document.body.style.overflow = 'hidden';
        if (isNew) {
            nkey = hex();
            var f = el('ts-f-name');
            if (f) {
                f.focus();
            }
        }
    }

    function kind() {
        el('ts-numw').hidden = el('ts-f-text').checked;
    }

    function save() {
        if (!lock(el('ts-fsave'))) {
            return false;
        }
        var id = val('ts-f-id');
        call('Save', [
            { key: 'fieldid', vlu: id },
            { key: 'testid', vlu: val('ts-f-test') },
            { key: 'newid', vlu: id ? '' : nkey },
            { key: 'fname', vlu: val('ts-f-name') },
            { key: 'fsp', vlu: val('ts-f-sp') },
            { key: 'ftext', vlu: el('ts-f-text').checked ? '1' : '0' },
            { key: 'funit', vlu: val('ts-f-unit') },
            { key: 'fdec', vlu: val('ts-f-dec') },
            { key: 'flow', vlu: val('ts-f-low') },
            { key: 'fhigh', vlu: val('ts-f-high') }
        ]);
        return false;
    }

    function saved(msg) {
        unlock();
        closeModal();
        toast(msg);
    }

    function move(id, dir, b) {
        if (!lock(b)) {
            return;
        }
        call('Move', [{ key: 'fieldid', vlu: id }, { key: 'dir', vlu: dir }]);
    }

    function ask(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Ask', [{ key: 'fieldid', vlu: id }]);
    }

    function remove(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Remove', [{ key: 'fieldid', vlu: id }]);
    }

    function preview(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Preview', [{ key: 'testid', vlu: id }]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        pick: pick,
        open: open,
        add: add,
        shown: shown,
        kind: kind,
        save: save,
        saved: saved,
        move: move,
        ask: ask,
        remove: remove,
        preview: preview,
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

AdminTestsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminTestsJs.navPaint();
        AdminTestsJs.reveal();
    });
} else {
    AdminTestsJs.navPaint();
    AdminTestsJs.reveal();
}
