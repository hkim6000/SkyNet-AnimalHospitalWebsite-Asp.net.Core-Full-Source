var AdminApptsJs = (function () {

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
        $ApiRequest('AdminAppts/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('ap-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3400);
    }

    function openNav() {
        el('ap-nav').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeNav() {
        el('ap-nav').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
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
        var open = !box.classList.contains('ap-sopen');
        box.classList.toggle('ap-sopen', open);
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
        var all = document.querySelectorAll('.ap-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('ap-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.ap-sec.ap-shere');
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
        var m = el('ap-menu');
        var w = el('ap-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminApptsJs.').join('{js}.').split('ap-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('ap-').join('{p}-'));
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
        var n = el('ap-nav');
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
        var a = n.querySelector('.ap-mi.ap-on');
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
        if (el('ap-menu')) {
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
            if (el('ap-menu')) {
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
        var n = el('ap-nav');
        var m = el('ap-menu');
        var w = el('ap-who');
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
            w.innerHTML = wh.split('{p}-').join('ap-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminApptsJs.').split('{p}-').join('ap-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.ap-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('ap-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.ap-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.ap-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('ap-shere', here);
                all[j].classList.toggle('ap-sopen', open);
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
        el('ap-mbody').innerHTML = html;
        el('ap-modal').classList.add('ap-open');
        document.body.style.overflow = 'hidden';
        times(el('ap-mbody'));
    }

    function closeModal() {
        el('ap-modal').classList.remove('ap-open');
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
        el('ap-main').innerHTML = html;
    }

    var pg = 1;
    var W = { mode: 'new', pet: '', book: '', staff: '', day: '', time: '', appt: '', pick: null };
    var saving = null;

    function fl() {
        return [
            { key: 'dfrom', vlu: val('ap-from') },
            { key: 'dto', vlu: val('ap-to') },
            { key: 'fstatus', vlu: val('ap-status') },
            { key: 'fstaff', vlu: val('ap-staff') },
            { key: 'fbook', vlu: val('ap-book') },
            { key: 'term', vlu: val('ap-term') },
            { key: 'pg', vlu: String(pg) }
        ];
    }

    function act(method, list) {
        call(method, (list || []).concat(fl()));
    }

    function go(p) {
        pg = p || 1;
        call('Filter', fl());
        return false;
    }

    function week() {
        var f = el('ap-from');
        var t = el('ap-to');
        if (f) {
            f.value = '';
        }
        if (t) {
            t.value = '';
        }
        go(1);
    }

    function ready() {
        times(el('ap-main'));
    }

    function listed(from, to) {
        var f = el('ap-from');
        var t = el('ap-to');
        if (f) {
            f.value = from;
        }
        if (t) {
            t.value = to;
        }
        times(el('ap-list'));
    }

    function shown() {
        el('ap-modal').classList.add('ap-open');
        document.body.style.overflow = 'hidden';
        times(el('ap-mbody'));
    }

    function open(id) {
        call('OpenAppt', [{ key: 'apptid', vlu: id }]);
        return false;
    }

    function setStatus(id, st) {
        act('SetStatus', [{ key: 'apptid', vlu: id }, { key: 'tostatus', vlu: st }]);
    }

    function noShow(id) {
        if (!window.confirm('Mark this appointment as a no-show?')) {
            return;
        }
        act('NoShow', [{ key: 'apptid', vlu: id }]);
    }

    function cancelForm(show) {
        var f = el('ap-cform');
        if (!f) {
            return;
        }
        f.hidden = !show;
        if (show) {
            el('ap-creason').focus();
        }
    }

    function cancelAppt(id) {
        el('ap-e-creason').textContent = '';
        act('CancelAppt', [{ key: 'apptid', vlu: id }, { key: 'reason', vlu: val('ap-creason') }]);
    }

    function checkIn(id) {
        act('CheckIn', [{ key: 'apptid', vlu: id }]);
    }

    function saveNote(id) {
        act('SaveNote', [{ key: 'apptid', vlu: id }, { key: 'desknote', vlu: val('ap-dnote') }]);
    }

    function addDay(ymd, n) {
        var p = (ymd || '').split('-');
        if (p.length !== 3) {
            return '';
        }
        var d = new Date(Date.UTC(+p[0], +p[1] - 1, +p[2] + n));
        return isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
    }

    function wz(list) {
        return [
            { key: 'wpet', vlu: W.pet },
            { key: 'wbook', vlu: W.book },
            { key: 'wstaff', vlu: W.staff },
            { key: 'wday', vlu: W.day },
            { key: 'wtime', vlu: W.time },
            { key: 'wappt', vlu: W.appt }
        ].concat(list || []);
    }

    function steps(n) {
        for (var i = 2; i <= 5; i++) {
            var s = el('ap-w' + i);
            if (s) {
                s.hidden = i > n;
            }
        }
    }

    function newBooking() {
        call('NewBooking', []);
    }

    function wzInit(step) {
        var v = $GetVarData('apwz');
        if (typeof v === 'string') {
            try {
                v = JSON.parse(v);
            } catch (e) {
                v = null;
            }
        }
        if (v) {
            W = { mode: v.mode || 'new', pet: v.pet || '', book: v.book || '', staff: v.staff || '', day: v.day || '', time: v.time || '', appt: v.appt || '', pick: null };
        }
        if (step === 1) {
            W.pet = '';
            W.book = '';
        }
        W.pick = null;
        steps(step);
        shown();
        if (step === 1 && el('ap-w-term')) {
            el('ap-w-term').focus();
        }
        if (step === 4) {
            prePick();
        }
    }

    function findPet() {
        call('PetSearch', wz([{ key: 'petterm', vlu: val('ap-w-term') }]));
        return false;
    }

    function pickPet(id) {
        W.pet = id;
        call('PickPet', wz());
    }

    function petPicked(id) {
        W.pet = id;
        W.book = '';
        W.pick = null;
        el('ap-w-staff').innerHTML = '';
        el('ap-w-when').innerHTML = '';
        steps(2);
    }

    function changePet() {
        W.pet = '';
        W.book = '';
        W.pick = null;
        steps(1);
        call('ChangePet', wz());
    }

    function anyStaff() {
        W.staff = '';
        call('PickPet', wz());
    }

    function pickBook(btn) {
        var all = document.querySelectorAll('.ap-type');
        for (var i = 0; i < all.length; i++) {
            all[i].classList.toggle('ap-sel', all[i] === btn);
        }
        W.book = btn.getAttribute('data-book');
        W.pick = null;
        steps(2);
        call('PickBook', wz());
    }

    function bookPicked(book, staff, day) {
        W.book = book;
        W.staff = staff;
        W.day = day;
        W.pick = null;
        steps(4);
        prePick();
    }

    function pickStaff(v) {
        W.staff = v;
        W.pick = null;
        steps(4);
        call('Times', wz());
    }

    function wday(n) {
        var d = addDay(W.day || val('ap-w-day'), n);
        if (d) {
            W.day = d;
            W.pick = null;
            steps(4);
            call('Times', wz());
        }
    }

    function wdayset(v) {
        if (/^\d{4}-\d{2}-\d{2}$/.test(v || '')) {
            W.day = v;
            W.pick = null;
            steps(4);
            call('Times', wz());
        }
    }

    function seek() {
        W.pick = null;
        steps(4);
        call('Times', wz([{ key: 'wseek', vlu: '1' }]));
    }

    function timesShown(day) {
        W.day = day;
        W.pick = null;
        steps(4);
        prePick();
    }

    function prePick() {
        var b = el('ap-w-pre');
        if (W.book) {
            W.time = '';
        }
        if (b) {
            pickSlot(b);
        }
    }

    function pickSlot(btn) {
        var all = document.querySelectorAll('.ap-tm');
        for (var i = 0; i < all.length; i++) {
            all[i].classList.toggle('ap-sel', all[i] === btn);
            all[i].setAttribute('aria-pressed', all[i] === btn ? 'true' : 'false');
        }
        W.pick = { staff: btn.getAttribute('data-staff'), start: btn.getAttribute('data-start'), proc: btn.getAttribute('data-proc') || '' };
        el('ap-w-sum').textContent = btn.getAttribute('data-label');
        el('ap-e-wsave').textContent = '';
        steps(5);
        var go5 = el('ap-w5');
        if (go5 && go5.scrollIntoView) {
            go5.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    function wsave() {
        if (!W.pick) {
            toast('Pick a time first.');
            return;
        }
        var b = el('ap-w-go');
        if (b) {
            b.disabled = true;
        }
        clearTimeout(saving);
        saving = setTimeout(function () {
            var x = el('ap-w-go');
            if (x) {
                x.disabled = false;
            }
        }, 2500);
        act(W.mode === 'move' ? 'SaveMove' : 'SaveBooking', wz([
            { key: 'slotstaff', vlu: W.pick.staff },
            { key: 'slotstart', vlu: W.pick.start },
            { key: 'slotproc', vlu: W.pick.proc },
            { key: 'vetnote', vlu: val('ap-w-note') },
            { key: 'srcval', vlu: val('ap-w-src') }
        ]));
    }

    function slotGone() {
        var b = el('ap-w-go');
        if (b) {
            b.disabled = false;
        }
        W.pick = null;
        call('Times', wz());
        var s5 = el('ap-w5');
        if (s5) {
            s5.hidden = false;
        }
    }

    function resched(id) {
        call('Reschedule', [{ key: 'apptid', vlu: id }]);
    }

    function keys(e) {
        if (e.key === 'Escape' && el('ap-modal').classList.contains('ap-open')) {
            closeModal();
        }
    }

    function reveal() {
        var q = el('ap-q');
        var g = function (k) {
            return q ? q.getAttribute('data-' + k) || '' : '';
        };
        document.addEventListener('keydown', keys);
        call('Load', [
            { key: 'openappt', vlu: g('a') },
            { key: 'bookpet', vlu: g('p') },
            { key: 'bookstaff', vlu: g('s') },
            { key: 'listday', vlu: g('d') },
            { key: 'booktime', vlu: g('t') }
        ]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        listed: listed,
        shown: shown,
        go: go,
        week: week,
        open: open,
        setStatus: setStatus,
        noShow: noShow,
        cancelForm: cancelForm,
        cancelAppt: cancelAppt,
        checkIn: checkIn,
        saveNote: saveNote,
        newBooking: newBooking,
        wzInit: wzInit,
        findPet: findPet,
        pickPet: pickPet,
        petPicked: petPicked,
        changePet: changePet,
        anyStaff: anyStaff,
        pickBook: pickBook,
        bookPicked: bookPicked,
        pickStaff: pickStaff,
        wday: wday,
        wdayset: wdayset,
        seek: seek,
        timesShown: timesShown,
        pickSlot: pickSlot,
        wsave: wsave,
        slotGone: slotGone,
        resched: resched,
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

AdminApptsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminApptsJs.navPaint();
        AdminApptsJs.reveal();
    });
} else {
    AdminApptsJs.navPaint();
    AdminApptsJs.reveal();
}
