var AdminBookTypesJs = (function () {

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
        $ApiRequest('AdminBookTypes/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('bt-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bt-open');
        }, 3400);
    }

    function openNav() {
        el('bt-nav').classList.add('bt-open');
        el('bt-scrim').classList.add('bt-open');
    }

    function closeNav() {
        el('bt-nav').classList.remove('bt-open');
        el('bt-scrim').classList.remove('bt-open');
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
        var open = !box.classList.contains('bt-sopen');
        box.classList.toggle('bt-sopen', open);
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
        var all = document.querySelectorAll('.bt-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('bt-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.bt-sec.bt-shere');
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
        var m = el('bt-menu');
        var w = el('bt-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminBookTypesJs.').join('{js}.').split('bt-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('bt-').join('{p}-'));
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
        var n = el('bt-nav');
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
        var a = n.querySelector('.bt-mi.bt-on');
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
        if (el('bt-menu')) {
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
            if (el('bt-menu')) {
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
        var n = el('bt-nav');
        var m = el('bt-menu');
        var w = el('bt-who');
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
            w.innerHTML = wh.split('{p}-').join('bt-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminBookTypesJs.').split('{p}-').join('bt-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.bt-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('bt-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.bt-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.bt-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('bt-shere', here);
                all[j].classList.toggle('bt-sopen', open);
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
        el('bt-mbody').innerHTML = html;
        el('bt-modal').classList.add('bt-open');
        document.body.style.overflow = 'hidden';
        times(el('bt-mbody'));
    }

    function closeModal() {
        el('bt-modal').classList.remove('bt-open');
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
        el('bt-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var ftimer = null;
    var nkey = '';
    var items = [];

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
            b.setAttribute('data-btlk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-btlk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-btlk');
        }
    }

    function reveal() {
        call('Load', []);
    }

    function find() {
        call('List', [{ key: 'term', vlu: val('bt-term') }, { key: 'fvt', vlu: val('bt-vt') }, { key: 'show', vlu: val('bt-show') || 'active' }]);
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(find, 350);
    }

    function open(id) {
        if (!lock(null)) {
            return;
        }
        call('Form', [{ key: 'bookid', vlu: id }]);
    }

    function add(b) {
        if (!lock(b)) {
            return;
        }
        call('Form', [{ key: 'bookid', vlu: '' }]);
    }

    function shown(isNew) {
        el('bt-modal').classList.add('bt-open');
        document.body.style.overflow = 'hidden';
        items = [];
        var sel = el('bt-f-item');
        if (sel) {
            for (var i = 0; i < sel.options.length; i++) {
                var o = sel.options[i];
                items.push({ v: o.value, t: o.textContent, vt: o.getAttribute('data-vt') || '' });
            }
            filterItems();
        }
        if (isNew) {
            nkey = hex();
            var f = el('bt-f-name');
            if (f) {
                f.focus();
            }
        }
    }

    function filterItems() {
        var sel = el('bt-f-item');
        if (!sel) {
            return;
        }
        var vt = val('bt-f-vt');
        var cur = sel.value;
        sel.innerHTML = '';
        var keep = false;
        for (var i = 0; i < items.length; i++) {
            if ((' ' + items[i].vt + ' ').indexOf(' ' + vt + ' ') < 0) {
                continue;
            }
            var o = document.createElement('option');
            o.value = items[i].v;
            o.textContent = items[i].t;
            o.setAttribute('data-vt', items[i].vt);
            sel.appendChild(o);
            if (items[i].v === cur) {
                keep = true;
            }
        }
        sel.value = keep ? cur : '';
    }

    function vtChg() {
        var vt = val('bt-f-vt');
        el('bt-surgw').hidden = vt !== 'surgery';
        el('bt-sizew').hidden = vt !== 'grooming';
        el('bt-sizew2').hidden = vt !== 'grooming';
        el('bt-minl').textContent = vt === 'surgery' ? 'Surgery length (minutes)' : vt === 'grooming' ? 'Minutes, small pet' : 'Minutes';
        filterItems();
    }

    function img() {
        var p = el('bt-imgp');
        var v = val('bt-f-img');
        if (!p) {
            return;
        }
        p.innerHTML = '';
        if (v) {
            var i = document.createElement('img');
            i.src = v;
            i.alt = '';
            p.appendChild(i);
        } else {
            var s = document.createElement('span');
            s.textContent = 'No picture';
            p.appendChild(s);
        }
    }

    function picked(name) {
        var all = document.querySelectorAll('input[name="' + name + '"]:checked');
        var out = [];
        for (var i = 0; i < all.length; i++) {
            out.push(all[i].value);
        }
        return out.join(',');
    }

    function checked(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function text(id) {
        var e = el(id);
        return e ? e.value : '';
    }

    function save() {
        if (!lock(el('bt-fsave'))) {
            return false;
        }
        var id = val('bt-f-id');
        call('Save', [
            { key: 'bookid', vlu: id },
            { key: 'newid', vlu: id ? '' : nkey },
            { key: 'bname', vlu: val('bt-f-name') },
            { key: 'team', vlu: val('bt-f-team') },
            { key: 'vt', vlu: val('bt-f-vt') },
            { key: 'short', vlu: val('bt-f-short') },
            { key: 'long', vlu: text('bt-f-long') },
            { key: 'expect', vlu: text('bt-f-expect') },
            { key: 'min', vlu: val('bt-f-min') },
            { key: 'minm', vlu: val('bt-f-minm') },
            { key: 'minl', vlu: val('bt-f-minl') },
            { key: 'item', vlu: val('bt-f-item') },
            { key: 'room', vlu: val('bt-f-room') },
            { key: 'dfrom', vlu: val('bt-f-dfrom') },
            { key: 'dto', vlu: val('bt-f-dto') },
            { key: 'pick', vlu: val('bt-f-pick') },
            { key: 'preop', vlu: text('bt-f-preop') },
            { key: 'species', vlu: picked('bt-f-sp') },
            { key: 'staff', vlu: picked('bt-f-staff') },
            { key: 'img', vlu: val('bt-f-img') },
            { key: 'online', vlu: checked('bt-f-online') },
            { key: 'newok', vlu: checked('bt-f-newok') },
            { key: 'order', vlu: val('bt-f-order') },
            { key: 'active', vlu: checked('bt-f-active') }
        ]);
        return false;
    }

    function bad() {
        var all = document.querySelectorAll('#bt-mbody .bt-err');
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
        find();
    }

    function ask(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Ask', [{ key: 'bookid', vlu: id }]);
    }

    function remove(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Remove', [{ key: 'bookid', vlu: id }]);
    }

    function retire(id, b) {
        if (!lock(b)) {
            return;
        }
        call('Retire', [{ key: 'bookid', vlu: id }]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        unlock: unlock,
        find: find,
        typed: typed,
        open: open,
        add: add,
        shown: shown,
        vtChg: vtChg,
        img: img,
        save: save,
        bad: bad,
        saved: saved,
        ask: ask,
        remove: remove,
        retire: retire,
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

AdminBookTypesJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminBookTypesJs.navPaint();
        AdminBookTypesJs.reveal();
    });
} else {
    AdminBookTypesJs.navPaint();
    AdminBookTypesJs.reveal();
}
