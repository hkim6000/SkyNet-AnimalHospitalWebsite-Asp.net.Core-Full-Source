var AdminPaymentsJs = (function () {

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
        $ApiRequest('AdminPayments/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('py-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('py-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('py-open');
        }, 3400);
    }

    function openNav() {
        el('py-nav').classList.add('py-open');
        el('py-scrim').classList.add('py-open');
    }

    function closeNav() {
        el('py-nav').classList.remove('py-open');
        el('py-scrim').classList.remove('py-open');
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
        var open = !box.classList.contains('py-sopen');
        box.classList.toggle('py-sopen', open);
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
        var all = document.querySelectorAll('.py-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('py-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.py-sec.py-shere');
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
        var m = el('py-menu');
        var w = el('py-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminPaymentsJs.').join('{js}.').split('py-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('py-').join('{p}-'));
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
        var n = el('py-nav');
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
        var a = n.querySelector('.py-mi.py-on');
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
        if (el('py-menu')) {
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
            if (el('py-menu')) {
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
        var n = el('py-nav');
        var m = el('py-menu');
        var w = el('py-who');
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
            w.innerHTML = wh.split('{p}-').join('py-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminPaymentsJs.').split('{p}-').join('py-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.py-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('py-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.py-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.py-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('py-shere', here);
                all[j].classList.toggle('py-sopen', open);
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
        el('py-mbody').innerHTML = html;
        el('py-modal').classList.add('py-open');
        document.body.style.overflow = 'hidden';
        times(el('py-mbody'));
    }

    function closeModal() {
        el('py-modal').classList.remove('py-open');
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
        el('py-main').innerHTML = html;
    }

    var tmr = null;
    var ftmr = null;
    var curPage = 1;
    var today = '';
    var busy = false;
    var inflight = false;
    var again = false;

    function pad(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function ymd(d) {
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function parse(v) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || '');
        return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
    }

    function addDays(d, n) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
    }

    function st() {
        return [
            { key: 'from', vlu: val('py-from') },
            { key: 'to', vlu: val('py-to') },
            { key: 'meth', vlu: val('py-meth') },
            { key: 'staffid', vlu: val('py-staff') },
            { key: 'term', vlu: val('py-term') },
            { key: 'page', vlu: String(curPage) }
        ];
    }

    function send() {
        if (inflight) {
            again = true;
            return;
        }
        inflight = true;
        call('List', st());
    }

    function list() {
        clearTimeout(tmr);
        curPage = 1;
        send();
    }

    function page(n) {
        curPage = n;
        send();
        window.scrollTo(0, 0);
    }

    function typed() {
        clearTimeout(tmr);
        tmr = setTimeout(list, 320);
    }

    function dates(which) {
        var f = val('py-from');
        var t = val('py-to');
        if (!f && !t) {
            return;
        }
        if (which === 'from') {
            if (!f) {
                el('py-from').value = t;
            } else if (!t || t < f) {
                el('py-to').value = f;
            }
        } else {
            if (!t) {
                el('py-to').value = f;
            } else if (t < f) {
                el('py-from').value = t;
            }
        }
        list();
    }

    function step(dir) {
        var f = parse(val('py-from'));
        var t = parse(val('py-to')) || f;
        if (!f) {
            return;
        }
        var span = Math.round((t - f) / 86400000) + 1;
        el('py-from').value = ymd(addDays(f, dir * span));
        el('py-to').value = ymd(addDays(t, dir * span));
        list();
    }

    function quick(kind) {
        var t = parse(today) || new Date();
        var f = t;
        if (kind === 'week') {
            f = addDays(t, -6);
        } else if (kind === 'month') {
            f = new Date(t.getFullYear(), t.getMonth(), 1);
        }
        el('py-from').value = ymd(f);
        el('py-to').value = ymd(t);
        list();
    }

    function shown(f, t, day) {
        inflight = false;
        if (again) {
            again = false;
            send();
        }
        if (day) {
            today = day;
        }
        if (el('py-from')) {
            el('py-from').value = f;
            el('py-to').value = t;
        }
        var url = 'AdminPayments' + (f === t && f === today ? '' : '?d=' + f);
        try {
            history.replaceState(null, '', url);
        } catch (e) {
            return;
        }
        times(el('py-main'));
    }

    function csv() {
        call('Csv', st());
    }

    function gotCsv() {
        var d = $GetVarData('pycsv');
        if (!d || !d.text) {
            return;
        }
        var blob = new Blob(['﻿' + d.text], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = d.name || 'payments.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        toast(d.count + (d.count === 1 ? ' payment downloaded.' : ' payments downloaded.'));
    }

    function record() {
        openModal('<h2>Record a payment</h2><p class="py-mlead">Find the unpaid invoice by owner, pet or invoice number, then enter the payment.</p><label class="py-field"><span>Find an invoice</span><input type="search" id="py-fterm" maxlength="80" placeholder="Owner, pet, phone or invoice number" autocomplete="off" oninput="AdminPaymentsJs.ftyped()"></label><div id="py-found" class="py-found"><p class="py-dim py-empty-s">Loading&hellip;</p></div>');
        call('Find', [{ key: 'findterm', vlu: '' }]);
        setTimeout(function () {
            var i = el('py-fterm');
            if (i) {
                i.focus();
            }
        }, 60);
    }

    function ftyped() {
        clearTimeout(ftmr);
        ftmr = setTimeout(function () {
            call('Find', [{ key: 'findterm', vlu: val('py-fterm') }]);
        }, 300);
    }

    function pick(id) {
        call('PayForm', [{ key: 'invid', vlu: id }]);
    }

    function back() {
        record();
    }

    function modal(focusId) {
        if (!el('py-modal').classList.contains('py-open')) {
            openModal(el('py-mbody').innerHTML);
        }
        times(el('py-mbody'));
        setTimeout(function () {
            var i = el(focusId);
            if (i) {
                i.focus();
                if (i.select) {
                    i.select();
                }
            }
        }, 60);
    }

    function savePay(id, key) {
        if (busy) {
            return;
        }
        busy = true;
        setTimeout(function () {
            busy = false;
        }, 1500);
        el('py-merr').textContent = '';
        call('SavePay', [
            { key: 'invid', vlu: id },
            { key: 'method', vlu: val('py-pmethod') },
            { key: 'amount', vlu: val('py-pamount') },
            { key: 'refno', vlu: val('py-pref') },
            { key: 'paykey', vlu: key }
        ]);
    }

    function methodHint() {
        var m = val('py-pmethod');
        var h = el('py-prefh');
        if (h) {
            h.textContent = m === 'card' ? 'Last 4 digits of the card' : m === 'check' ? 'Check number' : 'Optional note';
        }
    }

    function voidAsk(id) {
        call('VoidAsk', [{ key: 'payid', vlu: id }]);
    }

    function doVoid(id) {
        if (busy) {
            return;
        }
        busy = true;
        setTimeout(function () {
            busy = false;
        }, 1500);
        el('py-merr').textContent = '';
        call('VoidPay', [{ key: 'payid', vlu: id }, { key: 'reason', vlu: val('py-vreason') }]);
    }

    function done(msg) {
        busy = false;
        closeModal();
        if (msg) {
            toast(msg);
        }
        send();
    }

    function failed() {
        busy = false;
    }

    function reveal() {
        var site = el('py-site');
        var qd = site ? site.getAttribute('data-qd') || '' : '';
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && el('py-modal').classList.contains('py-open')) {
                closeModal();
            }
        });
        call('Load', [{ key: 'from', vlu: qd }, { key: 'to', vlu: qd }]);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        openNav: openNav,
        closeNav: closeNav,
        sec: sec,
        secs: secs,
        toast: toast,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        list: list,
        page: page,
        typed: typed,
        dates: dates,
        step: step,
        quick: quick,
        shown: shown,
        csv: csv,
        gotCsv: gotCsv,
        record: record,
        ftyped: ftyped,
        pick: pick,
        back: back,
        modal: modal,
        savePay: savePay,
        methodHint: methodHint,
        voidAsk: voidAsk,
        doVoid: doVoid,
        done: done,
        failed: failed
    };
})();

AdminPaymentsJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminPaymentsJs.navPaint();
        AdminPaymentsJs.reveal();
    });
} else {
    AdminPaymentsJs.navPaint();
    AdminPaymentsJs.reveal();
}
