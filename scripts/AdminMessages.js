var AdminMessagesJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminMessages/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('xg-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('xg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('xg-open');
        }, 3200);
    }

    function openNav() {
        el('xg-nav').classList.add('xg-open');
        el('xg-scrim').classList.add('xg-open');
    }

    function closeNav() {
        el('xg-nav').classList.remove('xg-open');
        el('xg-scrim').classList.remove('xg-open');
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

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]:not([data-x])');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            t.setAttribute('data-x', '1');
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var otimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('ah_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('ah_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        navClear();
        setCookie('ah_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('xg-modal').classList.add('xg-open');
        times(el('xg-mbody'));
        var f = el('xg-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('xg-modal');
        if (m) {
            m.classList.remove('xg-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function loaded() {
        times(el('xg-main'));
        if (window.MutationObserver && el('xg-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('xg-main'));
                }, 30);
            }).observe(el('xg-main'), { childList: true, subtree: true });
        }
        start();
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('xg-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('xg-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'xg-ask';
        askBox.innerHTML = '<div class="xg-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="xg-err" role="alert"></em><div class="xg-askacts"><button type="button" class="xg-btn xg-btn-ghost xg-btn-sm"></button><button type="button" class="xg-btn xg-btn-sm"></button></div></div>';
        (el('xg-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('xg-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'xg-btn xg-btn-sm' + (o.danger ? ' xg-askdel' : ' xg-btn-accent');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('xg-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('xg-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    var current = '';
    var page = 1;
    var stimer = null;

    function start() {
        list(1);
        if (qs('id')) {
            open1(qs('id'));
        }
    }

    function per(k) {
        if (el('xg-per')) {
            el('xg-per').value = k;
        }
        if (k !== 'range') {
            if (el('xg-from')) {
                el('xg-from').value = '';
            }
            if (el('xg-to')) {
                el('xg-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('xg-per') }, { key: 'pfrom', vlu: val('xg-from') }, { key: 'pto', vlu: val('xg-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function list(pg) {
        if (pg) {
            page = pg;
        }
        api('List', [{ key: 'mq', vlu: val('xg-q') }, { key: 'mst', vlu: val('xg-mst') }, { key: 'mtopic', vlu: val('xg-mtopic') }, { key: 'mpg', vlu: String(page) }].concat(period()));
    }

    function open1(id) {
        current = id;
        api('Open', [{ key: 'mid', vlu: id }]);
    }

    function state(s) {
        api('SetState', [{ key: 'mid', vlu: current }, { key: 'mstate', vlu: s }]);
    }

    function note() {
        if (el('xg-notebtn')) {
            el('xg-notebtn').disabled = true;
        }
        api('SaveNote', [{ key: 'mid', vlu: current }, { key: 'ereply', vlu: el('xg-ereply') ? el('xg-ereply').value.trim() : '' }]);
    }

    function changed(msg, again) {
        if (again) {
            open1(current);
        } else {
            closeModal();
        }
        list(0);
        if (msg) {
            toast(msg);
        }
    }

    function remove(name) {
        ask({ title: 'Delete the message from ' + name + '?', text: 'The message and its team note are removed for good.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('Delete', [{ key: 'mid', vlu: current }]);
        });
    }

    function listed() {
        times(el('xg-list'));
    }

    function unlock() {
        if (el('xg-notebtn')) {
            el('xg-notebtn').disabled = false;
        }
    }

    function deleted() {
        closeModal();
        list(0);
        toast('Message deleted.');
    }

    function reveal() {
        if (!getCookie('ah_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', []);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('xg-', 'xg-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
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
        var open = !box.classList.contains('xg-sopen');
        box.classList.toggle('xg-sopen', open);
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
        var all = document.querySelectorAll('.xg-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('xg-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.xg-sec.xg-shere');
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
        var m = el('xg-menu');
        var w = el('xg-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminMessagesJs.').join('{js}.').split('xg-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('xg-').join('{p}-'));
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
        var n = el('xg-nav');
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
        var a = n.querySelector('.xg-mi.xg-on');
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
        if (el('xg-menu')) {
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
            if (el('xg-menu')) {
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
        var n = el('xg-nav');
        var m = el('xg-menu');
        var w = el('xg-who');
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
            w.innerHTML = wh.split('{p}-').join('xg-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminMessagesJs.').split('{p}-').join('xg-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.xg-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('xg-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.xg-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.xg-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('xg-shere', here);
                all[j].classList.toggle('xg-sopen', open);
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

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        sec: sec,
        secs: secs,
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        loaded: loaded,
        per: per,
        search: search,
        list: list,
        open: open1,
        state: state,
        note: note,
        changed: changed,
        remove: remove,
        deleted: deleted,
        listed: listed,
        unlock: unlock
    };
})();

AdminMessagesJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminMessagesJs.navPaint();
        AdminMessagesJs.reveal();
    });
} else {
    AdminMessagesJs.navPaint();
    AdminMessagesJs.reveal();
}
