var AdminTodayJs = (function () {

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
        $ApiRequest('AdminToday/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('td-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('td-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('td-open');
        }, 3400);
    }

    function openNav() {
        el('td-nav').classList.add('td-open');
        el('td-scrim').classList.add('td-open');
    }

    function closeNav() {
        el('td-nav').classList.remove('td-open');
        el('td-scrim').classList.remove('td-open');
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
        var open = !box.classList.contains('td-sopen');
        box.classList.toggle('td-sopen', open);
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
        var all = document.querySelectorAll('.td-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('td-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.td-sec.td-shere');
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
        var m = el('td-menu');
        var w = el('td-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminTodayJs.').join('{js}.').split('td-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('td-').join('{p}-'));
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
        var n = el('td-nav');
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
        var a = n.querySelector('.td-mi.td-on');
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
        if (el('td-menu')) {
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
            if (el('td-menu')) {
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
        var n = el('td-nav');
        var m = el('td-menu');
        var w = el('td-who');
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
            w.innerHTML = wh.split('{p}-').join('td-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminTodayJs.').split('{p}-').join('td-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.td-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('td-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.td-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.td-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('td-shere', here);
                all[j].classList.toggle('td-sopen', open);
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
        el('td-mbody').innerHTML = html;
        el('td-modal').classList.add('td-open');
        document.body.style.overflow = 'hidden';
        times(el('td-mbody'));
    }

    function closeModal() {
        el('td-modal').classList.remove('td-open');
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
        el('td-main').innerHTML = html;
    }

    var FKEY = 'ah_td_filt';
    var DKEY = 'ah_td_done';
    var timer = null;
    var ticker = null;
    var ftimer = null;
    var drawnAt = Date.now();
    var petId = '';
    var manualRoom = false;
    var pending = null;

    function store(k, v) {
        try {
            localStorage.setItem(k, v);
        } catch (e) {
            return;
        }
    }

    function load(k) {
        try {
            return localStorage.getItem(k) || '';
        } catch (e) {
            return '';
        }
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function modalOpen() {
        var m = el('td-modal');
        return !!(m && m.classList.contains('td-open'));
    }

    function filt() {
        var s = el('td-fstaff');
        return s ? s.value : load(FKEY);
    }

    function act(method, list) {
        call(method, (list || []).concat([{ key: 'filt', vlu: filt() }]));
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
        var list = document.querySelectorAll('.td-mins');
        for (var i = 0; i < list.length; i++) {
            var b = parseInt(list[i].getAttribute('data-min'), 10) || 0;
            list[i].textContent = minText(b + add);
        }
    }

    function refresh() {
        if (modalOpen() || document.hidden) {
            return;
        }
        act('Board', []);
    }

    function filter() {
        store(FKEY, val('td-fstaff'));
        act('Board', []);
    }

    function drawn() {
        drawnAt = Date.now();
        var d = el('td-done');
        if (d && load(DKEY) === '1') {
            d.open = true;
        }
        times(el('td-main'));
    }

    function ready() {
        drawn();
        clearInterval(timer);
        timer = setInterval(refresh, 45000);
        clearInterval(ticker);
        ticker = setInterval(tick, 20000);
    }

    function doneToggle(d) {
        store(DKEY, d.open ? '1' : '0');
    }

    function shown() {
        manualRoom = false;
        el('td-modal').classList.add('td-open');
        document.body.style.overflow = 'hidden';
        times(el('td-mbody'));
    }

    function ask(title, text, btn, fn, danger) {
        pending = fn;
        openModal('<h2>' + esc(title) + '</h2><p class="td-mlead">' + esc(text) + '</p><div class="td-mfoot"><button type="button" class="td-btn td-btn-ghost" onclick="AdminTodayJs.closeModal()">Not now</button><button type="button" class="td-btn ' + (danger ? 'td-btn-danger' : 'td-btn-accent') + '" onclick="AdminTodayJs.yes()">' + esc(btn) + '</button></div>');
    }

    function yes() {
        var f = pending;
        pending = null;
        if (f) {
            f();
        }
    }

    function checkIn(id) {
        act('CheckIn', [{ key: 'apptid', vlu: id }]);
    }

    function confirmAppt(id) {
        act('Confirm', [{ key: 'apptid', vlu: id }]);
    }

    function noShow(id, pet) {
        ask('Mark ' + pet + ' as a no-show?', 'The appointment stays in the history as a no-show. You can book a new one later.', 'Mark as no-show', function () {
            act('NoShow', [{ key: 'apptid', vlu: id }]);
        }, true);
    }

    function undo(id, pet) {
        ask('Undo check-in for ' + pet + '?', 'The pet leaves the waiting list. A booked appointment goes back to Expected; a walk-in is removed.', 'Undo check-in', function () {
            act('Undo', [{ key: 'visitid', vlu: id }]);
        }, true);
    }

    function start(id) {
        act('StartPick', [{ key: 'visitid', vlu: id }]);
    }

    function move(id) {
        act('MovePick', [{ key: 'visitid', vlu: id }]);
    }

    function toCheckout(id, pet) {
        ask('Send ' + pet + ' to checkout?', 'The room becomes free and the visit moves to Ready for checkout at the front desk.', 'Ready for checkout', function () {
            act('Ready', [{ key: 'visitid', vlu: id }]);
        });
    }

    function rooms() {
        return document.getElementsByName('td-room');
    }

    function checkedRoom() {
        var list = rooms();
        for (var i = 0; i < list.length; i++) {
            if (list[i].checked) {
                return list[i].value;
            }
        }
        return '';
    }

    function roomPick() {
        manualRoom = true;
        var e = el('td-e-room');
        if (e) {
            e.textContent = '';
        }
    }

    function staffPick() {
        if (manualRoom) {
            return;
        }
        var s = el('td-mstaff');
        var o = s && s.options[s.selectedIndex];
        var home = o ? o.getAttribute('data-home') || '' : '';
        if (!home) {
            return;
        }
        var list = rooms();
        for (var i = 0; i < list.length; i++) {
            if (list[i].value === home && !list[i].disabled) {
                list[i].checked = true;
            }
        }
    }

    function go(method, id) {
        var r = checkedRoom();
        var e = el('td-e-room');
        if (!r) {
            e.textContent = 'Pick a room.';
            return;
        }
        if (!val('td-mstaff')) {
            e.textContent = 'Pick who looks after the patient.';
            return;
        }
        act(method, [{ key: 'visitid', vlu: id }, { key: 'roomid', vlu: r }, { key: 'staffid', vlu: val('td-mstaff') }]);
    }

    function startGo(id) {
        go('StartGo', id);
    }

    function moveGo(id) {
        go('MoveGo', id);
    }

    function walkIn() {
        act('WalkOpen', []);
    }

    function walkReady() {
        petId = '';
        var q = el('td-wq');
        if (q) {
            q.focus();
        }
    }

    function find() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            act('Find', [{ key: 'term', vlu: val('td-wq') }]);
        }, 300);
    }

    function pickPet(id, label) {
        petId = id;
        var p = el('td-wpick');
        p.innerHTML = '<span>Pet</span><b>' + esc(label) + '</b><button type="button" class="td-link" onclick="AdminTodayJs.unpick()">Change</button>';
        p.hidden = false;
        el('td-wres').innerHTML = '';
        el('td-wq').value = '';
        el('td-e-walk').textContent = '';
    }

    function unpick() {
        petId = '';
        el('td-wpick').hidden = true;
        el('td-wres').innerHTML = '<p class="td-hint">Type at least 2 letters or 3 digits.</p>';
        el('td-wq').focus();
    }

    function walkGo() {
        if (!petId) {
            el('td-e-walk').textContent = 'Pick a pet from the search results first.';
            return;
        }
        act('WalkGo', [{ key: 'petid', vlu: petId }, { key: 'kind', vlu: val('td-wkind') }, { key: 'staffid', vlu: val('td-wstaff') }, { key: 'reason', vlu: val('td-wwhy') }]);
    }

    function reveal() {
        document.addEventListener('keydown', function (ev) {
            if (ev.key === 'Escape' && modalOpen()) {
                closeModal();
            }
        });
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && el('td-board')) {
                refresh();
            }
        });
        call('Load', [{ key: 'filt', vlu: load(FKEY) }]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        drawn: drawn,
        shown: shown,
        refresh: refresh,
        filter: filter,
        doneToggle: doneToggle,
        checkIn: checkIn,
        confirmAppt: confirmAppt,
        noShow: noShow,
        undo: undo,
        yes: yes,
        start: start,
        move: move,
        toCheckout: toCheckout,
        roomPick: roomPick,
        staffPick: staffPick,
        startGo: startGo,
        moveGo: moveGo,
        walkIn: walkIn,
        walkReady: walkReady,
        find: find,
        pickPet: pickPet,
        unpick: unpick,
        walkGo: walkGo,
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

AdminTodayJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminTodayJs.navPaint();
        AdminTodayJs.reveal();
    });
} else {
    AdminTodayJs.navPaint();
    AdminTodayJs.reveal();
}
