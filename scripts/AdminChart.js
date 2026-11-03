var AdminChartJs = (function () {

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
        $ApiRequest('AdminChart/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('ch-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ch-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ch-open');
        }, 3400);
    }

    function openNav() {
        el('ch-nav').classList.add('ch-open');
        el('ch-scrim').classList.add('ch-open');
    }

    function closeNav() {
        el('ch-nav').classList.remove('ch-open');
        el('ch-scrim').classList.remove('ch-open');
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
        var open = !box.classList.contains('ch-sopen');
        box.classList.toggle('ch-sopen', open);
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
        var all = document.querySelectorAll('.ch-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('ch-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.ch-sec.ch-shere');
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
        var m = el('ch-menu');
        var w = el('ch-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminChartJs.').join('{js}.').split('ch-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('ch-').join('{p}-'));
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
        var n = el('ch-nav');
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
        var a = n.querySelector('.ch-mi.ch-on');
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
        if (el('ch-menu')) {
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
            if (el('ch-menu')) {
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
        var n = el('ch-nav');
        var m = el('ch-menu');
        var w = el('ch-who');
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
            w.innerHTML = wh.split('{p}-').join('ch-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminChartJs.').split('{p}-').join('ch-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.ch-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('ch-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.ch-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.ch-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('ch-shere', here);
                all[j].classList.toggle('ch-sopen', open);
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
        el('ch-mbody').innerHTML = html;
        el('ch-modal').classList.add('ch-open');
        document.body.style.overflow = 'hidden';
        times(el('ch-mbody'));
    }

    function closeModal() {
        el('ch-modal').classList.remove('ch-open');
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
        el('ch-main').innerHTML = html;
    }

    var ftmr = null;
    var qtmr = null;
    var pre = null;

    function pid() {
        var s = el('ch-site');
        return s ? s.getAttribute('data-open') || '' : '';
    }

    function picker() {
        times(el('ch-main'));
        var t = el('ch-term');
        if (t && window.matchMedia('(min-width: 900px)').matches) {
            t.focus();
        }
    }

    function find() {
        clearTimeout(ftmr);
        call('Find', [{ key: 'term', vlu: val('ch-term') }]);
    }

    function typed() {
        clearTimeout(ftmr);
        ftmr = setTimeout(find, 300);
    }

    function ready() {
        times(el('ch-main'));
    }

    function setOpen(a, open) {
        a.classList.toggle('ch-open', open);
        var b = a.querySelector('.ch-vh');
        if (b && b.tagName === 'BUTTON') {
            b.setAttribute('aria-expanded', open ? 'true' : 'false');
        }
    }

    function tog(btn) {
        var a = btn.parentNode;
        setOpen(a, !a.classList.contains('ch-open'));
    }

    function arts() {
        return Array.prototype.slice.call(document.querySelectorAll('#ch-tl .ch-v:not(.ch-vlite)'));
    }

    function all(open) {
        arts().forEach(function (a) {
            if (!a.hidden) {
                setOpen(a, open);
            }
        });
    }

    function unmark(root) {
        var ms = root.querySelectorAll('mark.ch-hl');
        for (var i = 0; i < ms.length; i++) {
            var m = ms[i];
            var p = m.parentNode;
            p.replaceChild(document.createTextNode(m.textContent), m);
            p.normalize();
        }
    }

    function mark(root, q) {
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
        var list = [];
        while (walker.nextNode()) {
            list.push(walker.currentNode);
        }
        list.forEach(function (node) {
            var text = node.nodeValue;
            var low = text.toLowerCase();
            var i = low.indexOf(q);
            if (i < 0 || low.length !== text.length) {
                return;
            }
            var frag = document.createDocumentFragment();
            var last = 0;
            while (i >= 0) {
                frag.appendChild(document.createTextNode(text.substring(last, i)));
                var m = document.createElement('mark');
                m.className = 'ch-hl';
                m.textContent = text.substr(i, q.length);
                frag.appendChild(m);
                last = i + q.length;
                i = low.indexOf(q, last);
            }
            frag.appendChild(document.createTextNode(text.substring(last)));
            node.parentNode.replaceChild(frag, node);
        });
    }

    function filt() {
        var tl = el('ch-tl');
        if (!tl) {
            return;
        }
        var ft = val('ch-ft');
        var fy = val('ch-fy');
        var fs = val('ch-fs');
        var raw = val('ch-q');
        var q = raw.length >= 2 ? raw.toLowerCase() : '';
        unmark(tl);
        if (q && pre === null) {
            pre = arts().map(function (a) {
                return a.classList.contains('ch-open');
            });
        }
        if (!q && pre !== null) {
            var keep = pre;
            pre = null;
            arts().forEach(function (a, i) {
                setOpen(a, !!keep[i]);
            });
        }
        var list = tl.querySelectorAll('.ch-v');
        var shown = 0;
        for (var i = 0; i < list.length; i++) {
            var a = list[i];
            var ok = (!ft || a.getAttribute('data-type') === ft) && (!fy || a.getAttribute('data-year') === fy) && (!fs || a.getAttribute('data-staff') === fs);
            if (ok && q) {
                ok = a.textContent.toLowerCase().indexOf(q) >= 0;
            }
            a.hidden = !ok;
            if (ok) {
                shown++;
                if (q) {
                    mark(a, q);
                    if (!a.classList.contains('ch-vlite')) {
                        setOpen(a, true);
                    }
                }
            }
        }
        el('ch-none').hidden = shown > 0;
        var total = list.length;
        var noun = total === 1 ? ' visit' : ' visits';
        var txt;
        if (q) {
            txt = shown + ' of ' + total + noun + ' mention “' + raw + '”';
        } else if (shown === total) {
            txt = total + noun;
        } else {
            txt = 'Showing ' + shown + ' of ' + total + noun;
        }
        el('ch-count').textContent = txt;
    }

    function typedq() {
        clearTimeout(qtmr);
        qtmr = setTimeout(filt, 220);
    }

    function tab(name) {
        var tabs = document.querySelectorAll('.ch-tab');
        for (var i = 0; i < tabs.length; i++) {
            var on = tabs[i].getAttribute('data-tab') === name;
            tabs[i].classList.toggle('ch-on', on);
            tabs[i].setAttribute('aria-selected', on ? 'true' : 'false');
        }
        el('ch-pane-vis').classList.toggle('ch-pane-off', name !== 'vis');
        el('ch-pane-lab').classList.toggle('ch-pane-off', name !== 'lab');
    }

    function trend() {
        call('Trend', [{ key: 'petid', vlu: pid() }, { key: 'fld', vlu: val('ch-fld') }]);
    }

    function print() {
        ['ch-q', 'ch-ft', 'ch-fy', 'ch-fs'].forEach(function (id) {
            var e = el(id);
            if (e) {
                e.value = '';
            }
        });
        pre = null;
        filt();
        all(true);
        window.print();
    }

    function reveal() {
        call('Load', [{ key: 'petid', vlu: pid() }]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        picker: picker,
        find: find,
        typed: typed,
        ready: ready,
        tog: tog,
        all: all,
        filt: filt,
        typedq: typedq,
        tab: tab,
        trend: trend,
        print: print,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminChartJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminChartJs.navPaint();
        AdminChartJs.reveal();
    });
} else {
    AdminChartJs.navPaint();
    AdminChartJs.reveal();
}
