var AdminRoomSetupJs = (function () {

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
        $ApiRequest('AdminRoomSetup/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('ro-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ro-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ro-open');
        }, 3400);
    }

    function openNav() {
        el('ro-nav').classList.add('ro-open');
        el('ro-scrim').classList.add('ro-open');
    }

    function closeNav() {
        el('ro-nav').classList.remove('ro-open');
        el('ro-scrim').classList.remove('ro-open');
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
        var open = !box.classList.contains('ro-sopen');
        box.classList.toggle('ro-sopen', open);
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
        var all = document.querySelectorAll('.ro-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('ro-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.ro-sec.ro-shere');
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
        var m = el('ro-menu');
        var w = el('ro-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminRoomSetupJs.').join('{js}.').split('ro-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('ro-').join('{p}-'));
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
        var n = el('ro-nav');
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
        var a = n.querySelector('.ro-mi.ro-on');
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
        if (el('ro-menu')) {
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
            if (el('ro-menu')) {
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
        var n = el('ro-nav');
        var m = el('ro-menu');
        var w = el('ro-who');
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
            w.innerHTML = wh.split('{p}-').join('ro-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminRoomSetupJs.').split('{p}-').join('ro-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.ro-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('ro-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.ro-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.ro-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('ro-shere', here);
                all[j].classList.toggle('ro-sopen', open);
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
        el('ro-mbody').innerHTML = html;
        el('ro-modal').classList.add('ro-open');
        document.body.style.overflow = 'hidden';
        times(el('ro-mbody'));
    }

    function closeModal() {
        el('ro-modal').classList.remove('ro-open');
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
        el('ro-main').innerHTML = html;
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
        el('ro-modal').classList.add('ro-open');
        document.body.style.overflow = 'hidden';
        times(el('ro-mbody'));
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

    function act(method, list, b) {
        if (!lock(b)) {
            return false;
        }
        var data = (list || []).slice();
        data.push({ key: 'rshow', vlu: val('ro-show') || 'active' });
        call(method, data);
        return true;
    }

    function reveal() {
        call('Load', []);
    }

    function ready() {
        unlock();
        pkey = hex();
    }

    function refresh() {
        call('Refresh', [{ key: 'rshow', vlu: val('ro-show') || 'active' }]);
    }

    function typeForm(id) {
        pkey = hex();
        act('TypeForm', [{ key: 'typeid', vlu: id || '' }]);
    }

    function formId(fid, attr) {
        var f = el(fid);
        return f ? (f.getAttribute(attr) || '') : '';
    }

    function purpose() {
        var on = val('ro-tpur') === 'boarding';
        var list = document.querySelectorAll('.ro-bonly');
        for (var i = 0; i < list.length; i++) {
            list[i].hidden = !on;
        }
    }

    function colour() {
        var v = el('ro-tcolv');
        if (v) {
            v.textContent = val('ro-tcol').toUpperCase();
        }
    }

    function picture() {
        var v = val('ro-timg');
        var p = el('ro-timgpv');
        if (p) {
            p.innerHTML = v ? '<img src="' + esc(v) + '" alt="" loading="lazy">' : '<span>No picture</span>';
        }
    }

    function saveType() {
        act('SaveType', [
            { key: 'typeid', vlu: formId('ro-tform', 'data-typeid') },
            { key: 'newid', vlu: pkey },
            { key: 'tname', vlu: val('ro-tname') },
            { key: 'tpur', vlu: val('ro-tpur') },
            { key: 'tcol', vlu: val('ro-tcol') },
            { key: 'tspecies', vlu: checked('ro-tsp') },
            { key: 'tsizes', vlu: checked('ro-tsz') },
            { key: 'titem', vlu: val('ro-titem') },
            { key: 'tshort', vlu: val('ro-tshort') },
            { key: 'tlong', vlu: el('ro-tlong') ? el('ro-tlong').value : '' },
            { key: 'timg', vlu: val('ro-timg') },
            { key: 'tonline', vlu: bit('ro-tonline') },
            { key: 'tord', vlu: val('ro-tord') },
            { key: 'tact', vlu: bit('ro-tact') }
        ], el('ro-tgo'));
    }

    function typeActive(id, on, b) {
        act('TypeActive', [{ key: 'typeid', vlu: id }, { key: 'tact', vlu: on ? '1' : '0' }], b);
    }

    function delType(id, b) {
        act('DelType', [{ key: 'typeid', vlu: id }], b);
    }

    function delTypeGo(id, b) {
        act('DelType', [{ key: 'typeid', vlu: id }, { key: 'sure', vlu: '1' }], b);
    }

    function moveType(id, dir, b) {
        act('MoveType', [{ key: 'typeid', vlu: id }, { key: 'mdir', vlu: dir }], b);
    }

    function roomForm(id, typeId) {
        pkey = hex();
        act('RoomForm', [{ key: 'roomid', vlu: id || '' }, { key: 'rtypeid', vlu: typeId || '' }]);
    }

    function saveRoom() {
        act('SaveRoom', [
            { key: 'roomid', vlu: formId('ro-rform', 'data-roomid') },
            { key: 'newid', vlu: pkey },
            { key: 'rname', vlu: val('ro-rname') },
            { key: 'rtype', vlu: val('ro-rtype') },
            { key: 'rnote', vlu: val('ro-rnote') },
            { key: 'rord', vlu: val('ro-rord') },
            { key: 'ract', vlu: bit('ro-ract') }
        ], el('ro-rgo'));
    }

    function roomActive(id, on, b) {
        act('RoomActive', [{ key: 'roomid', vlu: id }, { key: 'ract', vlu: on ? '1' : '0' }], b);
    }

    function delRoom(id, b) {
        act('DelRoom', [{ key: 'roomid', vlu: id }], b);
    }

    function delRoomGo(id, b) {
        act('DelRoom', [{ key: 'roomid', vlu: id }, { key: 'sure', vlu: '1' }], b);
    }

    function moveRoom(id, dir, b) {
        act('MoveRoom', [{ key: 'roomid', vlu: id }, { key: 'mdir', vlu: dir }], b);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        refresh: refresh,
        shown: shown,
        typeForm: typeForm,
        purpose: purpose,
        colour: colour,
        picture: picture,
        saveType: saveType,
        typeActive: typeActive,
        delType: delType,
        delTypeGo: delTypeGo,
        moveType: moveType,
        roomForm: roomForm,
        saveRoom: saveRoom,
        roomActive: roomActive,
        delRoom: delRoom,
        delRoomGo: delRoomGo,
        moveRoom: moveRoom,
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

AdminRoomSetupJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminRoomSetupJs.navPaint();
        AdminRoomSetupJs.reveal();
    });
} else {
    AdminRoomSetupJs.navPaint();
    AdminRoomSetupJs.reveal();
}
