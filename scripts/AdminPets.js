var AdminPetsJs = (function () {

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
        $ApiRequest('AdminPets/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('pt-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pt-open');
        }, 3400);
    }

    function openNav() {
        el('pt-nav').classList.add('pt-open');
        el('pt-scrim').classList.add('pt-open');
    }

    function closeNav() {
        el('pt-nav').classList.remove('pt-open');
        el('pt-scrim').classList.remove('pt-open');
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
        var open = !box.classList.contains('pt-sopen');
        box.classList.toggle('pt-sopen', open);
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
        var all = document.querySelectorAll('.pt-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('pt-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.pt-sec.pt-shere');
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
        var m = el('pt-menu');
        var w = el('pt-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminPetsJs.').join('{js}.').split('pt-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('pt-').join('{p}-'));
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
        var n = el('pt-nav');
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
        var a = n.querySelector('.pt-mi.pt-on');
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
        if (el('pt-menu')) {
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
            if (el('pt-menu')) {
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
        var n = el('pt-nav');
        var m = el('pt-menu');
        var w = el('pt-who');
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
            w.innerHTML = wh.split('{p}-').join('pt-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminPetsJs.').split('{p}-').join('pt-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.pt-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('pt-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.pt-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.pt-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('pt-shere', here);
                all[j].classList.toggle('pt-sopen', open);
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
        el('pt-mbody').innerHTML = html;
        el('pt-modal').classList.add('pt-open');
        document.body.style.overflow = 'hidden';
        times(el('pt-mbody'));
    }

    function closeModal() {
        el('pt-modal').classList.remove('pt-open');
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
        el('pt-main').innerHTML = html;
    }

    var pg = 1;
    var tmr = null;
    var otmr = null;

    function st() {
        return [
            { key: 'term', vlu: val('pt-term') },
            { key: 'sp', vlu: val('pt-sp') },
            { key: 'show', vlu: val('pt-show') || 'active' },
            { key: 'pg', vlu: String(pg) }
        ];
    }

    function ready() {
        times(el('pt-main'));
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
        el('pt-modal').classList.add('pt-open');
        el('pt-modal').scrollTop = 0;
        document.body.style.overflow = 'hidden';
        times(el('pt-mbody'));
        if (id) {
            history.replaceState(null, '', 'AdminPets?p=' + encodeURIComponent(id));
        }
        var f = el('pt-mbody').querySelector('form input:not([type=hidden]), form select');
        if (f && f.offsetParent && window.matchMedia('(min-width: 900px)').matches) {
            f.focus();
        }
    }

    function closeAll() {
        closeModal();
        history.replaceState(null, '', 'AdminPets');
    }

    function open(id) {
        call('Open', [{ key: 'petid', vlu: id }]);
    }

    function add(owner) {
        call('Form', [{ key: 'petid', vlu: '' }, { key: 'ownerid', vlu: owner || '' }]);
    }

    function edit(id) {
        call('Form', [{ key: 'petid', vlu: id }]);
    }

    function species() {
        var s = el('pt-f-species');
        var o = s.options[s.selectedIndex];
        var sized = o && o.getAttribute('data-size') === '1';
        el('pt-f-sizebox').hidden = !sized;
        el('pt-f-breed').setAttribute('list', 'pt-bl-' + s.value);
    }

    function change() {
        el('pt-f-ochip').hidden = true;
        el('pt-f-ofind').hidden = false;
        el('pt-f-osearch').focus();
    }

    function ofind() {
        clearTimeout(otmr);
        otmr = setTimeout(function () {
            call('OwnerFind', [{ key: 'oterm', vlu: val('pt-f-osearch') }]);
        }, 280);
    }

    function pick(id, btn) {
        el('pt-f-owner').value = id;
        el('pt-f-oname').innerHTML = btn.innerHTML;
        el('pt-f-ochip').hidden = false;
        el('pt-f-ofind').hidden = true;
        el('pt-f-ores').innerHTML = '';
        el('pt-f-osearch').value = '';
        el('pt-e-owner').textContent = '';
    }

    function chk(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function save() {
        var act = el('pt-f-active');
        call('Save', [
            { key: 'petid', vlu: val('pt-f-id') },
            { key: 'ownerid', vlu: val('pt-f-owner') },
            { key: 'name', vlu: val('pt-f-name') },
            { key: 'species', vlu: val('pt-f-species') },
            { key: 'breed', vlu: val('pt-f-breed') },
            { key: 'sex', vlu: val('pt-f-sex') },
            { key: 'fixed', vlu: chk('pt-f-fixed') },
            { key: 'birth', vlu: val('pt-f-birth') },
            { key: 'color', vlu: val('pt-f-color') },
            { key: 'size', vlu: el('pt-f-sizebox').hidden ? '' : val('pt-f-size') },
            { key: 'chip', vlu: val('pt-f-chip') },
            { key: 'alert', vlu: val('pt-f-alert') },
            { key: 'active', vlu: act ? (act.checked ? '1' : '0') : '1' },
            { key: 'died', vlu: val('pt-f-died') }
        ]);
        return false;
    }

    function changed(msg) {
        call('List', st());
        toast(msg);
    }

    function wadd(id) {
        call('WeightAdd', [{ key: 'petid', vlu: id }, { key: 'wat', vlu: val('pt-w-at') }, { key: 'wlb', vlu: val('pt-w-lb') }]);
    }

    function wdel(id, wid) {
        if (!window.confirm('Delete this weight?')) {
            return;
        }
        call('WeightDel', [{ key: 'petid', vlu: id }, { key: 'weightid', vlu: wid }]);
    }

    function vform(id, vid) {
        call('VaxForm', [{ key: 'petid', vlu: id }, { key: 'vaxid', vlu: vid }]);
    }

    function pad(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function vitem() {
        var s = el('pt-v-item');
        if (!s) {
            return;
        }
        var o = s.options[s.selectedIndex];
        var nm = o ? o.getAttribute('data-n') : '';
        if (nm && document.activeElement === s) {
            el('pt-v-name').value = nm;
        }
        var due = el('pt-v-due');
        var m = o ? parseInt(o.getAttribute('data-m') || '0', 10) : 0;
        var g = val('pt-v-given');
        if (due.getAttribute('data-touched') === '1' || !m || !/^\d{4}-\d{2}-\d{2}$/.test(g)) {
            return;
        }
        var p = g.split('-');
        var d = new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1 + m, 1);
        var last = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
        d.setDate(Math.min(parseInt(p[2], 10), last));
        due.value = d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function vshown(isNew) {
        if (isNew) {
            vitem();
        }
        var f = el('pt-vf');
        if (f && f.scrollIntoView) {
            f.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }

    function vmore(btn) {
        var box = el('pt-vsec');
        var on = !box.classList.contains('pt-vall');
        box.classList.toggle('pt-vall', on);
        var n = btn.getAttribute('data-n');
        btn.textContent = on ? 'Hide older records' : 'Show ' + n + (n === '1' ? ' older record' : ' older records');
    }

    function vclose() {
        el('pt-vf').innerHTML = '';
    }

    function vsave(id) {
        call('VaxSave', [
            { key: 'petid', vlu: id },
            { key: 'vaxid', vlu: val('pt-v-id') },
            { key: 'vitem', vlu: val('pt-v-item') },
            { key: 'vname', vlu: val('pt-v-name') },
            { key: 'vgiven', vlu: val('pt-v-given') },
            { key: 'vdue', vlu: val('pt-v-due') },
            { key: 'vnote', vlu: val('pt-v-note') }
        ]);
    }

    function vdel(id, vid, name) {
        if (!window.confirm('Delete the ' + name + ' record?')) {
            return;
        }
        call('VaxDel', [{ key: 'petid', vlu: id }, { key: 'vaxid', vlu: vid }]);
    }

    function ask(id) {
        call('Ask', [{ key: 'petid', vlu: id }]);
    }

    function remove(id) {
        call('Remove', [{ key: 'petid', vlu: id }]);
    }

    function removed(msg) {
        closeAll();
        call('List', st());
        toast(msg);
    }

    function retire(id) {
        call('Retire', [{ key: 'petid', vlu: id }]);
    }

    function reveal() {
        var site = el('pt-site');
        function a(k) {
            return site ? site.getAttribute(k) || '' : '';
        }
        call('Load', [
            { key: 'openp', vlu: a('data-open') },
            { key: 'openn', vlu: a('data-new') },
            { key: 'openo', vlu: a('data-owner') }
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
        species: species,
        change: change,
        ofind: ofind,
        pick: pick,
        save: save,
        changed: changed,
        wadd: wadd,
        wdel: wdel,
        vform: vform,
        vitem: vitem,
        vshown: vshown,
        vmore: vmore,
        vclose: vclose,
        vsave: vsave,
        vdel: vdel,
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

AdminPetsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminPetsJs.navPaint();
        AdminPetsJs.reveal();
    });
} else {
    AdminPetsJs.navPaint();
    AdminPetsJs.reveal();
}
