var AdminBoardingJs = (function () {

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
        $ApiRequest('AdminBoarding/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('bd-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bd-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bd-open');
        }, 3400);
    }

    function openNav() {
        el('bd-nav').classList.add('bd-open');
        el('bd-scrim').classList.add('bd-open');
    }

    function closeNav() {
        el('bd-nav').classList.remove('bd-open');
        el('bd-scrim').classList.remove('bd-open');
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
        var open = !box.classList.contains('bd-sopen');
        box.classList.toggle('bd-sopen', open);
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
        var all = document.querySelectorAll('.bd-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('bd-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.bd-sec.bd-shere');
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
        var m = el('bd-menu');
        var w = el('bd-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminBoardingJs.').join('{js}.').split('bd-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('bd-').join('{p}-'));
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
        var n = el('bd-nav');
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
        var a = n.querySelector('.bd-mi.bd-on');
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
        if (el('bd-menu')) {
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
            if (el('bd-menu')) {
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
        var n = el('bd-nav');
        var m = el('bd-menu');
        var w = el('bd-who');
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
            w.innerHTML = wh.split('{p}-').join('bd-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminBoardingJs.').split('{p}-').join('bd-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.bd-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('bd-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.bd-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.bd-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('bd-shere', here);
                all[j].classList.toggle('bd-sopen', open);
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
        el('bd-mbody').innerHTML = html;
        el('bd-modal').classList.add('bd-open');
        document.body.style.overflow = 'hidden';
        times(el('bd-mbody'));
    }

    function closeModal() {
        el('bd-modal').classList.remove('bd-open');
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
        el('bd-main').innerHTML = html;
    }

    var openA = '';
    var start = '';
    var todayStr = '';
    var canAdd = false;
    var fkey = '';
    var fpet = '';
    var fappt = '';
    var hintRun = '';
    var hintDay = '';
    var listed = false;
    var lpg = 1;
    var ftimer = null;
    var qtimer = null;
    var ptimer = null;
    var locked = false;
    var ltimer = null;
    var pending = null;

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
            b.setAttribute('data-bdlk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-bdlk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-bdlk');
        }
    }

    function site(k) {
        var s = el('bd-site');
        return s ? (s.getAttribute('data-' + k) || '') : '';
    }

    function act(method, list) {
        var data = (list || []).slice();
        data.push({ key: 'apptid', vlu: openA });
        data.push({ key: 'bdstart', vlu: start });
        call(method, data);
    }

    function reveal() {
        call('Load', [
            { key: 'openstay', vlu: site('qa') },
            { key: 'newpet', vlu: site('qp') },
            { key: 'bdstart', vlu: site('qd') }
        ]);
    }

    function url() {
        try {
            history.replaceState(null, '', 'AdminBoarding' + (start && start !== todayStr ? '?d=' + start : ''));
        } catch (e) {
            return;
        }
    }

    function ready(day, today, add) {
        start = day;
        todayStr = today;
        canAdd = add === 1;
        times(el('bd-main'));
        url();
    }

    function boardAt(day, today) {
        start = day;
        todayStr = today;
        var i = el('bd-bday');
        if (i) {
            i.value = day;
        }
        url();
    }

    function board(day) {
        call('Board', [{ key: 'bdstart', vlu: day }]);
    }

    function ymd(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }

    function shift(n) {
        var p = (start || todayStr).split('-');
        var d = new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1, parseInt(p[2], 10), 12);
        d.setDate(d.getDate() + n);
        board(ymd(d));
    }

    function prev() {
        shift(-7);
    }

    function next() {
        shift(7);
    }

    function goToday() {
        board(todayStr);
    }

    function pickDay() {
        var v = val('bd-bday');
        if (/^\d{4}-\d{2}-\d{2}$/.test(v)) {
            board(v);
        }
    }

    function tab(name) {
        var isList = name === 'list';
        el('bd-vboard').hidden = isList;
        el('bd-vlist').hidden = !isList;
        el('bd-tb-board').classList.toggle('bd-on', !isList);
        el('bd-tb-list').classList.toggle('bd-on', isList);
        el('bd-tb-board').setAttribute('aria-selected', isList ? 'false' : 'true');
        el('bd-tb-list').setAttribute('aria-selected', isList ? 'true' : 'false');
        if (isList && !listed) {
            find(1);
        }
    }

    function find(pg) {
        listed = true;
        lpg = pg || 1;
        call('Find', [
            { key: 'lterm', vlu: val('bd-lterm') },
            { key: 'lstat', vlu: val('bd-lstat') },
            { key: 'lfrom', vlu: val('bd-lfrom') },
            { key: 'lto', vlu: val('bd-lto') },
            { key: 'pg', vlu: String(lpg) }
        ]);
    }

    function listed2(pg) {
        lpg = pg;
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            find(1);
        }, 350);
    }

    function clearList() {
        el('bd-lterm').value = '';
        el('bd-lstat').value = '';
        el('bd-lfrom').value = '';
        el('bd-lto').value = '';
        find(1);
    }

    function after() {
        if (listed && el('bd-vlist') && !el('bd-vlist').hidden) {
            find(lpg);
        }
    }

    function hit(e) {
        var t = e.target;
        if (!t || !t.closest) {
            return;
        }
        var b = t.closest('[data-a]');
        if (b) {
            open(b.getAttribute('data-a'));
            return;
        }
        var c = t.closest('[data-d]');
        if (c && canAdd) {
            newStay('', c.getAttribute('data-r'), c.getAttribute('data-d'));
        }
    }

    function open(id) {
        openA = id;
        call('Stay', [{ key: 'apptid', vlu: id }]);
    }

    function back() {
        if (openA) {
            open(openA);
        } else {
            closeModal();
        }
    }

    function shown(id, focus) {
        if (id) {
            openA = id;
        }
        fkey = hex();
        el('bd-modal').classList.add('bd-open');
        el('bd-modal').scrollTop = 0;
        document.body.style.overflow = 'hidden';
        times(el('bd-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function ask(title, text, btn, fn, danger) {
        pending = fn;
        openModal('<h2>' + esc(title) + '</h2><p class="bd-lead">' + esc(text) + '</p><div class="bd-mfoot"><button type="button" class="bd-btn bd-btn-ghost" onclick="AdminBoardingJs.back()">Not now</button><button type="button" class="bd-btn ' + (danger ? 'bd-btn-danger' : 'bd-btn-accent') + '" id="bd-yes" onclick="AdminBoardingJs.yes()">' + esc(btn) + '</button></div>');
    }

    function yes() {
        var f = pending;
        pending = null;
        if (f && lock(el('bd-yes'))) {
            f();
        }
    }

    function newStay(pet, run, day) {
        openA = '';
        call('NewStay', [
            { key: 'petid', vlu: pet || '' },
            { key: 'runid', vlu: run || '' },
            { key: 'inday', vlu: day || '' },
            { key: 'bdstart', vlu: start }
        ]);
    }

    function formShown(pet, appt, run, day) {
        fpet = pet;
        fappt = appt;
        hintRun = run;
        hintDay = day;
        if (appt) {
            openA = appt;
        }
        shown('', pet ? '' : 'bd-fterm');
    }

    function petType() {
        clearTimeout(ptimer);
        ptimer = setTimeout(function () {
            call('PetFind', [{ key: 'petterm', vlu: val('bd-fterm') }]);
        }, 300);
    }

    function pickPet(id) {
        call('PetPick', [
            { key: 'petid', vlu: id },
            { key: 'runid', vlu: hintRun },
            { key: 'inday', vlu: hintDay },
            { key: 'bdstart', vlu: start }
        ]);
    }

    function changePet() {
        newStay('', hintRun, hintDay);
    }

    function picked() {
        var r = document.querySelector('input[name="bd-frun"]:checked');
        return r ? r.value : '';
    }

    function adds() {
        var all = document.querySelectorAll('.bd-fadd:checked');
        var list = [];
        for (var i = 0; i < all.length; i++) {
            list.push(all[i].value);
        }
        return list.join(',');
    }

    function formData() {
        return [
            { key: 'petid', vlu: fpet },
            { key: 'apptid', vlu: fappt },
            { key: 'fdin', vlu: val('bd-fdin') },
            { key: 'ftin', vlu: val('bd-ftin') },
            { key: 'fdout', vlu: val('bd-fdout') },
            { key: 'ftout', vlu: val('bd-ftout') },
            { key: 'frun', vlu: picked() },
            { key: 'fadds', vlu: adds() }
        ];
    }

    function dinChanged() {
        var a = val('bd-fdin');
        var z = el('bd-fdout');
        if (/^\d{4}-\d{2}-\d{2}$/.test(a) && z && (!z.value || z.value <= a)) {
            var p = a.split('-');
            var d = new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1, parseInt(p[2], 10), 12);
            d.setDate(d.getDate() + 1);
            z.value = ymd(d);
        }
        quote();
    }

    function quote() {
        clearTimeout(qtimer);
        qtimer = setTimeout(function () {
            if (el('bd-fdin')) {
                call('Quote', formData());
            }
        }, 250);
    }

    function save() {
        if (!lock(el('bd-fsave'))) {
            return;
        }
        var e = el('bd-e-save');
        if (e) {
            e.textContent = '';
        }
        call('SaveStay', formData().concat([
            { key: 'newid', vlu: fkey },
            { key: 'fnote', vlu: val('bd-fnote') },
            { key: 'fsrc', vlu: val('bd-fsrc') },
            { key: 'bdstart', vlu: start }
        ]));
    }

    function edit() {
        act('EditStay', []);
    }

    function confirmStay(b) {
        if (lock(b)) {
            act('Confirm', []);
        }
    }

    function noShow() {
        ask('Mark as a no-show?', 'The pet did not come in. The run becomes free again.', 'Mark as no-show', function () {
            act('NoShow', []);
        }, true);
    }

    function noShowFor(id) {
        openA = id;
        noShow();
    }

    function cancelForm() {
        act('CancelForm', []);
    }

    function cancelGo() {
        if (lock(el('bd-cgo'))) {
            act('CancelGo', [{ key: 'cwhy', vlu: val('bd-cwhy') }]);
        }
    }

    function checkIn(id) {
        openA = id;
        act('CheckInForm', []);
    }

    function checkInGo() {
        if (lock(el('bd-cingo'))) {
            act('CheckInGo', [{ key: 'ovr', vlu: val('bd-ovr') }, { key: 'newid', vlu: fkey }]);
        }
    }

    function move() {
        act('MoveForm', []);
    }

    function moveGo() {
        var r = document.querySelector('input[name="bd-mrun"]:checked');
        if (lock(el('bd-mgo'))) {
            act('MoveGo', [{ key: 'runid', vlu: r ? r.value : '' }]);
        }
    }

    function extend() {
        act('ExtendForm', []);
    }

    function extendGo() {
        if (lock(el('bd-xgo'))) {
            act('ExtendGo', [{ key: 'xdout', vlu: val('bd-xdout') }, { key: 'xtout', vlu: val('bd-xtout') }]);
        }
    }

    function tag(b) {
        var on = b.getAttribute('aria-pressed') !== 'true';
        b.setAttribute('aria-pressed', on ? 'true' : 'false');
        b.classList.toggle('bd-on', on);
    }

    function careSave() {
        var all = document.querySelectorAll('.bd-qt.bd-on');
        var list = [];
        for (var i = 0; i < all.length; i++) {
            list.push(all[i].getAttribute('data-t'));
        }
        if (lock(el('bd-csave'))) {
            act('CareSave', [{ key: 'newid', vlu: fkey }, { key: 'ctext', vlu: val('bd-ctext') }, { key: 'ctags', vlu: list.join(',') }]);
        }
    }

    function toCare() {
        var b = el('bd-carebox');
        if (b && b.scrollIntoView) {
            b.scrollIntoView({ block: 'start' });
        }
    }

    function deskSave() {
        if (lock(el('bd-dsave'))) {
            act('DeskSave', [{ key: 'dnote', vlu: val('bd-dnote') }]);
        }
    }

    function checkOut(id) {
        openA = id;
        act('CheckOutForm', []);
    }

    function checkOutGo() {
        if (lock(el('bd-cogo'))) {
            act('CheckOutGo', [{ key: 'newid', vlu: fkey }]);
        }
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        secs: secs,
        sec: sec,
        openNav: openNav,
        closeNav: closeNav,
        closeModal: closeModal,
        toast: toast,
        signOut: signOut,
        out: out,
        expired: expired,
        unlock: unlock,
        ready: ready,
        boardAt: boardAt,
        prev: prev,
        next: next,
        goToday: goToday,
        pickDay: pickDay,
        tab: tab,
        find: find,
        listed: listed2,
        typed: typed,
        clearList: clearList,
        after: after,
        hit: hit,
        open: open,
        back: back,
        shown: shown,
        yes: yes,
        newStay: newStay,
        formShown: formShown,
        petType: petType,
        pickPet: pickPet,
        changePet: changePet,
        dinChanged: dinChanged,
        quote: quote,
        save: save,
        edit: edit,
        confirmStay: confirmStay,
        noShow: noShow,
        noShowFor: noShowFor,
        cancelForm: cancelForm,
        cancelGo: cancelGo,
        checkIn: checkIn,
        checkInGo: checkInGo,
        move: move,
        moveGo: moveGo,
        extend: extend,
        extendGo: extendGo,
        tag: tag,
        careSave: careSave,
        toCare: toCare,
        deskSave: deskSave,
        checkOut: checkOut,
        checkOutGo: checkOutGo
    };
})();

AdminBoardingJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminBoardingJs.navPaint();
        AdminBoardingJs.reveal();
    });
} else {
    AdminBoardingJs.navPaint();
    AdminBoardingJs.reveal();
}
