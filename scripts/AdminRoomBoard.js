var AdminRoomBoardJs = (function () {

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
        $ApiRequest('AdminRoomBoard/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('rb-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rb-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rb-open');
        }, 3400);
    }

    function openNav() {
        el('rb-nav').classList.add('rb-open');
        el('rb-scrim').classList.add('rb-open');
    }

    function closeNav() {
        el('rb-nav').classList.remove('rb-open');
        el('rb-scrim').classList.remove('rb-open');
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
        var open = !box.classList.contains('rb-sopen');
        box.classList.toggle('rb-sopen', open);
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
        var all = document.querySelectorAll('.rb-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('rb-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.rb-sec.rb-shere');
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
        var m = el('rb-menu');
        var w = el('rb-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminRoomBoardJs.').join('{js}.').split('rb-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('rb-').join('{p}-'));
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
        var n = el('rb-nav');
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
        var a = n.querySelector('.rb-mi.rb-on');
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
        if (el('rb-menu')) {
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
            if (el('rb-menu')) {
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
        var n = el('rb-nav');
        var m = el('rb-menu');
        var w = el('rb-who');
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
            w.innerHTML = wh.split('{p}-').join('rb-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminRoomBoardJs.').split('{p}-').join('rb-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.rb-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('rb-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.rb-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.rb-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('rb-shere', here);
                all[j].classList.toggle('rb-sopen', open);
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
        el('rb-mbody').innerHTML = html;
        el('rb-modal').classList.add('rb-open');
        document.body.style.overflow = 'hidden';
        times(el('rb-mbody'));
    }

    function closeModal() {
        el('rb-modal').classList.remove('rb-open');
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
        el('rb-main').innerHTML = html;
    }

    var timer = null;
    var ticker = null;
    var drawnAt = Date.now();
    var pending = null;

    function esc(s) {
        return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function modalOpen() {
        var m = el('rb-modal');
        return !!(m && m.classList.contains('rb-open'));
    }

    function minText(m) {
        if (m < 1) {
            return '<1 min';
        }
        if (m < 60) {
            return m === 1 ? '1 min' : m + ' mins';
        }
        return Math.floor(m / 60) + ' hr' + (m % 60 > 0 ? ' ' + (m % 60) + ' min' : '');
    }

    function tick() {
        var add = Math.floor((Date.now() - drawnAt) / 60000);
        var list = document.querySelectorAll('.rb-mins');
        for (var i = 0; i < list.length; i++) {
            var b = parseInt(list[i].getAttribute('data-min'), 10) || 0;
            list[i].textContent = minText(b + add);
        }
    }

    function refresh() {
        if (modalOpen() || document.hidden) {
            return;
        }
        call('Board', []);
    }

    function drawn() {
        drawnAt = Date.now();
        times(el('rb-main'));
    }

    function ready() {
        drawn();
        clearInterval(timer);
        timer = setInterval(refresh, 30000);
        clearInterval(ticker);
        ticker = setInterval(tick, 20000);
    }

    function shown() {
        el('rb-modal').classList.add('rb-open');
        document.body.style.overflow = 'hidden';
        times(el('rb-mbody'));
    }

    function yes() {
        var f = pending;
        pending = null;
        if (f) {
            f();
        }
    }

    function checked(name) {
        var list = document.getElementsByName(name);
        for (var i = 0; i < list.length; i++) {
            if (list[i].checked) {
                return list[i];
            }
        }
        return null;
    }

    function startHere(roomId) {
        call('StartPick', [{ key: 'roomid', vlu: roomId }]);
    }

    function wvPick(r) {
        var sid = r.getAttribute('data-staff') || '';
        var s = el('rb-mstaff');
        if (!sid || !s) {
            return;
        }
        for (var i = 0; i < s.options.length; i++) {
            if (s.options[i].value === sid) {
                s.selectedIndex = i;
            }
        }
        el('rb-e-modal').textContent = '';
    }

    function startGo(roomId) {
        var r = checked('rb-wv');
        var e = el('rb-e-modal');
        if (!r) {
            e.textContent = 'Pick the waiting patient.';
            return;
        }
        if (!val('rb-mstaff')) {
            e.textContent = 'Pick who sees the patient.';
            return;
        }
        call('StartGo', [{ key: 'roomid', vlu: roomId }, { key: 'visitid', vlu: r.value }, { key: 'staffid', vlu: val('rb-mstaff') }]);
    }

    function move(id) {
        call('MovePick', [{ key: 'visitid', vlu: id }]);
    }

    function moveGo(id) {
        var r = checked('rb-mroom');
        var e = el('rb-e-modal');
        if (!r) {
            e.textContent = 'Pick a free room.';
            return;
        }
        if (!val('rb-mstaff')) {
            e.textContent = 'Pick who looks after the patient.';
            return;
        }
        call('MoveGo', [{ key: 'visitid', vlu: id }, { key: 'roomid', vlu: r.value }, { key: 'staffid', vlu: val('rb-mstaff') }]);
    }

    function toCheckout(id, pet) {
        pending = function () {
            call('Ready', [{ key: 'visitid', vlu: id }]);
        };
        openModal('<h2>Send ' + esc(pet) + ' to checkout?</h2><p class="rb-mlead">The room becomes free and the visit moves to Ready for checkout at the front desk.</p><div class="rb-mfoot"><button type="button" class="rb-btn rb-btn-ghost" onclick="AdminRoomBoardJs.closeModal()">Not now</button><button type="button" class="rb-btn rb-btn-accent" onclick="AdminRoomBoardJs.yes()">Ready for checkout</button></div>');
    }

    function reveal() {
        document.addEventListener('keydown', function (ev) {
            if (ev.key === 'Escape' && modalOpen()) {
                closeModal();
            }
        });
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && el('rb-board')) {
                refresh();
            }
        });
        call('Load', []);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        drawn: drawn,
        shown: shown,
        refresh: refresh,
        yes: yes,
        startHere: startHere,
        wvPick: wvPick,
        startGo: startGo,
        move: move,
        moveGo: moveGo,
        toCheckout: toCheckout,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        openModal: openModal,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminRoomBoardJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminRoomBoardJs.navPaint();
        AdminRoomBoardJs.reveal();
    });
} else {
    AdminRoomBoardJs.navPaint();
    AdminRoomBoardJs.reveal();
}
