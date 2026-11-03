var AdminClientsJs = (function () {

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
        $ApiRequest('AdminClients/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('cl-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('cl-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('cl-open');
        }, 3400);
    }

    function openNav() {
        el('cl-nav').classList.add('cl-open');
        el('cl-scrim').classList.add('cl-open');
    }

    function closeNav() {
        el('cl-nav').classList.remove('cl-open');
        el('cl-scrim').classList.remove('cl-open');
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
        var open = !box.classList.contains('cl-sopen');
        box.classList.toggle('cl-sopen', open);
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
        var all = document.querySelectorAll('.cl-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('cl-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.cl-sec.cl-shere');
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
        var m = el('cl-menu');
        var w = el('cl-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminClientsJs.').join('{js}.').split('cl-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('cl-').join('{p}-'));
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
        var n = el('cl-nav');
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
        var a = n.querySelector('.cl-mi.cl-on');
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
        if (el('cl-menu')) {
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
            if (el('cl-menu')) {
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
        var n = el('cl-nav');
        var m = el('cl-menu');
        var w = el('cl-who');
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
            w.innerHTML = wh.split('{p}-').join('cl-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminClientsJs.').split('{p}-').join('cl-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.cl-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('cl-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.cl-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.cl-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('cl-shere', here);
                all[j].classList.toggle('cl-sopen', open);
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
        el('cl-mbody').innerHTML = html;
        el('cl-modal').classList.add('cl-open');
        document.body.style.overflow = 'hidden';
        times(el('cl-mbody'));
    }

    function closeModal() {
        el('cl-modal').classList.remove('cl-open');
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
        el('cl-main').innerHTML = html;
    }

    var pg = 1;
    var tmr = null;

    function st() {
        return [
            { key: 'term', vlu: val('cl-term') },
            { key: 'show', vlu: val('cl-show') || 'active' },
            { key: 'pg', vlu: String(pg) }
        ];
    }

    function ready() {
        times(el('cl-main'));
    }

    function page(n) {
        pg = n > 0 ? n : 1;
        call('List', st());
    }

    function typed() {
        clearTimeout(tmr);
        tmr = setTimeout(function () {
            page(1);
        }, 320);
    }

    function shown(id) {
        el('cl-modal').classList.add('cl-open');
        el('cl-modal').scrollTop = 0;
        document.body.style.overflow = 'hidden';
        times(el('cl-mbody'));
        if (id) {
            history.replaceState(null, '', 'AdminClients?c=' + encodeURIComponent(id));
        }
        var f = el('cl-mbody').querySelector('input:not([type=hidden]), select, textarea');
        if (f && window.matchMedia('(min-width: 900px)').matches) {
            f.focus();
        }
    }

    function closeAll() {
        closeModal();
        history.replaceState(null, '', 'AdminClients');
    }

    function open(id) {
        call('Open', [{ key: 'clientid', vlu: id }]);
    }

    function add() {
        call('Form', [{ key: 'clientid', vlu: '' }]);
    }

    function edit(id) {
        call('Form', [{ key: 'clientid', vlu: id }]);
    }

    function save(force) {
        var act = el('cl-f-active');
        call('Save', [
            { key: 'clientid', vlu: val('cl-f-id') },
            { key: 'first', vlu: val('cl-f-first') },
            { key: 'last', vlu: val('cl-f-last') },
            { key: 'phone', vlu: val('cl-f-phone') },
            { key: 'phone2', vlu: val('cl-f-phone2') },
            { key: 'email', vlu: val('cl-f-email') },
            { key: 'pref', vlu: val('cl-f-pref') },
            { key: 'addr', vlu: val('cl-f-addr') },
            { key: 'city', vlu: val('cl-f-city') },
            { key: 'state', vlu: val('cl-f-state') },
            { key: 'zip', vlu: val('cl-f-zip') },
            { key: 'alert', vlu: val('cl-f-alert') },
            { key: 'note', vlu: val('cl-f-note') },
            { key: 'active', vlu: act ? (act.checked ? '1' : '0') : '1' },
            { key: 'force', vlu: force ? '1' : '0' }
        ]);
        return false;
    }

    function dupShown() {
        var d = el('cl-dup');
        if (d && d.scrollIntoView) {
            d.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }

    function saved(isNew) {
        call('List', st());
        toast(isNew ? 'The new client is saved.' : 'Changes saved.');
    }

    function ask(id) {
        call('Ask', [{ key: 'clientid', vlu: id }]);
    }

    function remove(id) {
        call('Remove', [{ key: 'clientid', vlu: id }]);
    }

    function removed(msg) {
        closeAll();
        call('List', st());
        toast(msg);
    }

    function retire(id) {
        call('Retire', [{ key: 'clientid', vlu: id }]);
    }

    function reveal() {
        var site = el('cl-site');
        call('Load', [
            { key: 'openc', vlu: site ? site.getAttribute('data-open') || '' : '' },
            { key: 'openn', vlu: site ? site.getAttribute('data-new') || '' : '' }
        ]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        page: page,
        typed: typed,
        shown: shown,
        open: open,
        add: add,
        edit: edit,
        save: save,
        dupShown: dupShown,
        saved: saved,
        ask: ask,
        remove: remove,
        removed: removed,
        retire: retire,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        closeModal: closeAll,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminClientsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminClientsJs.navPaint();
        AdminClientsJs.reveal();
    });
} else {
    AdminClientsJs.navPaint();
    AdminClientsJs.reveal();
}
