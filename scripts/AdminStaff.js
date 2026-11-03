var AdminStaffJs = (function () {

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
        $ApiRequest('AdminStaff/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('sf-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('sf-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sf-open');
        }, 3400);
    }

    function openNav() {
        el('sf-nav').classList.add('sf-open');
        el('sf-scrim').classList.add('sf-open');
    }

    function closeNav() {
        el('sf-nav').classList.remove('sf-open');
        el('sf-scrim').classList.remove('sf-open');
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
        var open = !box.classList.contains('sf-sopen');
        box.classList.toggle('sf-sopen', open);
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
        var all = document.querySelectorAll('.sf-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('sf-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.sf-sec.sf-shere');
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
        var m = el('sf-menu');
        var w = el('sf-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminStaffJs.').join('{js}.').split('sf-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('sf-').join('{p}-'));
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
        var n = el('sf-nav');
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
        var a = n.querySelector('.sf-mi.sf-on');
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
        if (el('sf-menu')) {
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
            if (el('sf-menu')) {
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
        var n = el('sf-nav');
        var m = el('sf-menu');
        var w = el('sf-who');
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
            w.innerHTML = wh.split('{p}-').join('sf-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminStaffJs.').split('{p}-').join('sf-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.sf-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('sf-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.sf-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.sf-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('sf-shere', here);
                all[j].classList.toggle('sf-sopen', open);
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
        el('sf-mbody').innerHTML = html;
        el('sf-modal').classList.add('sf-open');
        document.body.style.overflow = 'hidden';
        times(el('sf-mbody'));
    }

    function closeModal() {
        el('sf-modal').classList.remove('sf-open');
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
        el('sf-main').innerHTML = html;
    }

    var cur = '';
    var curRole = '';
    var mode = '';
    var locked = false;
    var ltimer = null;
    var newKey = '';

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
            b.setAttribute('data-sflk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-sflk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-sflk');
        }
    }

    function act(method, list, b) {
        if (!lock(b)) {
            return false;
        }
        call(method, list);
        return true;
    }

    function site(k) {
        var s = el('sf-site');
        return s ? (s.getAttribute('data-' + k) || '') : '';
    }

    var permOpen = {};

    document.addEventListener('toggle', function (e) {
        if (e.target && e.target.classList && e.target.classList.contains('sf-pgrp')) {
            remember();
        }
    }, true);

    function reveal() {
        call('Load', [{ key: 'openstaff', vlu: site('qs') }, { key: 'opentab', vlu: site('qt') }]);
    }

    function ready(m, id) {
        var same = m === mode && (m !== 'person' || id === cur) && (m !== 'roles' || id === curRole);
        mode = m;
        times(el('sf-main'));
        if (m === 'person') {
            cur = id || '';
            history.replaceState(null, '', 'AdminStaff?s=' + encodeURIComponent(cur));
        } else if (m === 'roles') {
            cur = '';
            curRole = id || '';
            history.replaceState(null, '', 'AdminStaff?t=roles');
        } else {
            cur = '';
            history.replaceState(null, '', 'AdminStaff');
            filter();
        }
        if (!same) {
            permOpen = {};
            window.scrollTo(0, 0);
        } else {
            reopen();
        }
    }

    function list() {
        call('List', []);
    }

    function person(id) {
        call('Open', [{ key: 'staffid', vlu: id }]);
    }

    function roles(rid) {
        call('RoleTab', [{ key: 'roleid', vlu: rid || '' }]);
    }

    function filter() {
        var body = el('sf-rows');
        if (!body) {
            return;
        }
        var term = val('sf-term').toLowerCase();
        var role = val('sf-frole');
        var st = el('sf-fact') ? el('sf-fact').value : '1';
        var rows = body.querySelectorAll('tr');
        var n = 0;
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            var ok = (!term || (r.getAttribute('data-search') || '').indexOf(term) >= 0) && (!role || r.getAttribute('data-role') === role) && (st === '' || r.getAttribute('data-act') === st);
            r.hidden = !ok;
            if (ok) {
                n++;
            }
        }
        el('sf-none').hidden = n > 0;
        var c = el('sf-count');
        if (c) {
            c.textContent = n + (n === 1 ? ' person' : ' people') + ' shown';
        }
    }

    function shown(focus) {
        el('sf-modal').classList.add('sf-open');
        document.body.style.overflow = 'hidden';
        times(el('sf-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function form(id, b) {
        if (!lock(b)) {
            return;
        }
        newKey = hex();
        call('Form', [{ key: 'staffid', vlu: id || '' }]);
    }

    function formShown() {
        shown('sf-f-name');
        ptype();
    }

    function ptype() {
        var p = el('sf-f-ptype');
        if (!p) {
            return;
        }
        var on = p.value !== '';
        var all = document.querySelectorAll('#sf-mbody .sf-prov');
        for (var i = 0; i < all.length; i++) {
            all[i].hidden = !on;
        }
    }

    function checked(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function save() {
        var sp = [];
        var boxes = document.querySelectorAll('#sf-f-species input[data-sp]');
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].checked) {
                sp.push(boxes[i].getAttribute('data-sp'));
            }
        }
        var ph = document.querySelector('input[name="sf-photo"]:checked');
        var id = val('sf-f-id');
        var list = [
            { key: 'staffid', vlu: id },
            { key: 'newid', vlu: newKey },
            { key: 'login', vlu: val('sf-f-login') },
            { key: 'sname', vlu: val('sf-f-name') },
            { key: 'title', vlu: val('sf-f-title') },
            { key: 'role', vlu: val('sf-f-role') },
            { key: 'email', vlu: val('sf-f-email') },
            { key: 'phone', vlu: val('sf-f-phone') },
            { key: 'ptype', vlu: val('sf-f-ptype') },
            { key: 'cred', vlu: val('sf-f-cred') },
            { key: 'room', vlu: val('sf-f-room') },
            { key: 'color', vlu: val('sf-f-color') },
            { key: 'species', vlu: sp.join(',') },
            { key: 'ownonly', vlu: checked('sf-f-own') },
            { key: 'active', vlu: el('sf-f-active') ? checked('sf-f-active') : '1' },
            { key: 'showweb', vlu: checked('sf-f-web') },
            { key: 'weborder', vlu: val('sf-f-order') },
            { key: 'accepting', vlu: checked('sf-f-accept') },
            { key: 'photo', vlu: ph ? ph.value : '' },
            { key: 'bio', vlu: el('sf-f-bio') ? el('sf-f-bio').value : '' },
            { key: 'edu', vlu: el('sf-f-edu') ? el('sf-f-edu').value : '' },
            { key: 'langs', vlu: val('sf-f-langs') }
        ];
        if (!id) {
            list.push({ key: 'pass', vlu: el('sf-f-pass') ? el('sf-f-pass').value : '' });
            list.push({ key: 'again', vlu: el('sf-f-again') ? el('sf-f-again').value : '' });
        }
        act('Save', list, el('sf-f-go'));
        return false;
    }

    function passForm() {
        call('PassForm', [{ key: 'staffid', vlu: cur }]);
    }

    function passShown() {
        shown('sf-p-new');
    }

    function setPass() {
        act('SetPass', [{ key: 'staffid', vlu: cur }, { key: 'next', vlu: el('sf-p-new') ? el('sf-p-new').value : '' }, { key: 'again', vlu: el('sf-p-again') ? el('sf-p-again').value : '' }], el('sf-p-go'));
        return false;
    }

    function kick(b) {
        act('Kick', [{ key: 'staffid', vlu: cur }], b);
    }

    function retire(on) {
        if (on) {
            act('Retire', [{ key: 'staffid', vlu: cur }, { key: 'makeactive', vlu: '1' }], null);
            return;
        }
        openModal('<h2>Make inactive?</h2><p class="sf-mlead">They are signed out at once, can\'t sign in, and are left out of new bookings and schedules. Their records keep their name, and you can make them active again later.</p><div class="sf-mfoot"><button type="button" class="sf-btn sf-btn-ghost" onclick="AdminStaffJs.closeModal()">Not now</button><button type="button" class="sf-btn sf-btn-danger" onclick="AdminStaffJs.retireGo(0, this)">Make inactive</button></div>');
    }

    function retireGo(on, b) {
        act('Retire', [{ key: 'staffid', vlu: cur }, { key: 'makeactive', vlu: on ? '1' : '0' }], b);
    }

    function askRemove() {
        call('Ask', [{ key: 'staffid', vlu: cur }]);
    }

    function remove(b) {
        act('Remove', [{ key: 'staffid', vlu: cur }], b);
    }

    function dirty(row) {
        var box = row ? row.closest('.sf-perm') : null;
        var any = false;
        var all = row.querySelectorAll('input[data-l]');
        for (var i = 0; i < all.length; i++) {
            if (all[i].checked) {
                any = true;
            }
        }
        row.classList.toggle('sf-pyes', any);
        count(row.closest('.sf-pgrp'));
        if (box) {
            box.classList.add('sf-pdirty');
        }
    }

    function count(grp) {
        if (!grp) {
            return;
        }
        var rows = grp.querySelectorAll('.sf-prow');
        var on = 0;
        for (var i = 0; i < rows.length; i++) {
            if (rows[i].classList.contains('sf-pyes')) {
                on++;
            }
        }
        var c = grp.querySelector('.sf-pcnt');
        if (c) {
            c.textContent = on + ' of ' + rows.length;
        }
    }

    function remember() {
        var all = document.querySelectorAll('#sf-main details.sf-pgrp');
        permOpen = {};
        for (var i = 0; i < all.length; i++) {
            if (all[i].open) {
                permOpen[all[i].closest('.sf-perm').id + '|' + all[i].getAttribute('data-sec')] = true;
            }
        }
    }

    function reopen() {
        var all = document.querySelectorAll('#sf-main details.sf-pgrp');
        for (var i = 0; i < all.length; i++) {
            if (permOpen[all[i].closest('.sf-perm').id + '|' + all[i].getAttribute('data-sec')]) {
                all[i].open = true;
            }
        }
    }

    function tick(cb) {
        var row = cb.closest('.sf-prow');
        if (!row) {
            return;
        }
        var all = row.querySelectorAll('input[data-l]');
        var l = cb.getAttribute('data-l');
        for (var i = 0; i < all.length; i++) {
            var x = all[i];
            if (l === 'V' && !cb.checked && x !== cb && !x.disabled) {
                x.checked = false;
            }
            if (l !== 'V' && cb.checked && x.getAttribute('data-l') === 'V' && !x.disabled) {
                x.checked = true;
            }
        }
        dirty(row);
    }

    function perms(id) {
        var box = el(id);
        if (!box) {
            return '';
        }
        var out = [];
        var rows = box.querySelectorAll('.sf-prow');
        for (var i = 0; i < rows.length; i++) {
            var r = '';
            var all = rows[i].querySelectorAll('input[data-l]');
            for (var j = 0; j < all.length; j++) {
                if (all[j].checked) {
                    r += all[j].getAttribute('data-l');
                }
            }
            if (r) {
                out.push(rows[i].getAttribute('data-k') + ':' + r);
            }
        }
        return out.join(';');
    }

    function fill(id, text) {
        var box = el(id);
        if (!box) {
            return;
        }
        var map = {};
        String(text || '').split(';').forEach(function (p) {
            var kv = p.split(':');
            if (kv.length === 2) {
                map[kv[0]] = kv[1];
            }
        });
        var rows = box.querySelectorAll('.sf-prow');
        for (var i = 0; i < rows.length; i++) {
            var have = map[rows[i].getAttribute('data-k')] || '';
            var all = rows[i].querySelectorAll('input[data-l]');
            for (var j = 0; j < all.length; j++) {
                if (!all[j].disabled) {
                    all[j].checked = have.indexOf(all[j].getAttribute('data-l')) >= 0;
                }
            }
            dirty(rows[i]);
        }
    }

    function defaults(b) {
        act('Defaults', [{ key: 'staffid', vlu: cur }], b);
    }

    function savePerms(b) {
        act('Perms', [{ key: 'staffid', vlu: cur }, { key: 'perms', vlu: perms('sf-pm-person') }], b);
    }

    function roleForm(rid, b) {
        if (!lock(b)) {
            return;
        }
        newKey = hex();
        call('RoleForm', [{ key: 'roleid', vlu: rid || '' }]);
    }

    function roleShown() {
        shown('sf-r-name');
    }

    function roleSave() {
        act('RoleSave', [{ key: 'roleid', vlu: val('sf-r-id') }, { key: 'newid', vlu: newKey }, { key: 'rname', vlu: val('sf-r-name') }, { key: 'rorder', vlu: val('sf-r-order') }], el('sf-r-go'));
        return false;
    }

    function saveRole(rid, b) {
        act('RolePerms', [{ key: 'roleid', vlu: rid }, { key: 'perms', vlu: perms('sf-pm-role') }], b);
    }

    function roleAsk(rid) {
        call('RoleAsk', [{ key: 'roleid', vlu: rid }]);
    }

    function roleRemove(rid, b) {
        act('RoleRemove', [{ key: 'roleid', vlu: rid }], b);
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
        list: list,
        person: person,
        roles: roles,
        filter: filter,
        shown: shown,
        form: form,
        formShown: formShown,
        ptype: ptype,
        save: save,
        passForm: passForm,
        passShown: passShown,
        setPass: setPass,
        kick: kick,
        retire: retire,
        retireGo: retireGo,
        askRemove: askRemove,
        remove: remove,
        tick: tick,
        fill: fill,
        defaults: defaults,
        savePerms: savePerms,
        roleForm: roleForm,
        roleShown: roleShown,
        roleSave: roleSave,
        saveRole: saveRole,
        roleAsk: roleAsk,
        roleRemove: roleRemove
    };
})();

AdminStaffJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminStaffJs.navPaint();
        AdminStaffJs.reveal();
    });
} else {
    AdminStaffJs.navPaint();
    AdminStaffJs.reveal();
}
