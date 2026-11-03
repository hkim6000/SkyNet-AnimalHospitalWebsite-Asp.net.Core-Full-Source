var AdminInvoicesJs = (function () {

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
        $ApiRequest('AdminInvoices/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('iv-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('iv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('iv-open');
        }, 3400);
    }

    function openNav() {
        el('iv-nav').classList.add('iv-open');
        el('iv-scrim').classList.add('iv-open');
    }

    function closeNav() {
        el('iv-nav').classList.remove('iv-open');
        el('iv-scrim').classList.remove('iv-open');
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
        var open = !box.classList.contains('iv-sopen');
        box.classList.toggle('iv-sopen', open);
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
        var all = document.querySelectorAll('.iv-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('iv-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.iv-sec.iv-shere');
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
        var m = el('iv-menu');
        var w = el('iv-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminInvoicesJs.').join('{js}.').split('iv-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('iv-').join('{p}-'));
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
        var n = el('iv-nav');
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
        var a = n.querySelector('.iv-mi.iv-on');
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
        if (el('iv-menu')) {
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
            if (el('iv-menu')) {
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
        var n = el('iv-nav');
        var m = el('iv-menu');
        var w = el('iv-who');
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
            w.innerHTML = wh.split('{p}-').join('iv-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminInvoicesJs.').split('{p}-').join('iv-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.iv-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('iv-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.iv-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.iv-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('iv-shere', here);
                all[j].classList.toggle('iv-sopen', open);
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
        el('iv-mbody').innerHTML = html;
        el('iv-modal').classList.add('iv-open');
        document.body.style.overflow = 'hidden';
        times(el('iv-mbody'));
    }

    function closeModal() {
        el('iv-modal').classList.remove('iv-open');
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
        el('iv-main').innerHTML = html;
    }

    var openI = '';
    var locked = false;
    var ltimer = null;
    var ftimer = null;
    var pending = null;
    var pkey = '';
    var curPg = 1;

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
            b.setAttribute('data-ivlk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-ivlk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-ivlk');
        }
    }

    function act(method, list, b) {
        if (!lock(b)) {
            return false;
        }
        var data = (list || []).slice();
        data.push({ key: 'invoiceid', vlu: openI });
        call(method, data);
        return true;
    }

    function site(k) {
        var s = el('iv-site');
        return s ? (s.getAttribute('data-' + k) || '') : '';
    }

    function reveal() {
        call('Load', [{ key: 'openi', vlu: site('qi') }, { key: 'openc', vlu: site('qc') }]);
    }

    function ready(m, id) {
        times(el('iv-main'));
        pkey = hex();
        if (m === 'detail') {
            openI = id || '';
        }
    }

    function clientId() {
        var c = el('iv-chips');
        return c ? (c.getAttribute('data-cid') || '') : '';
    }

    function filters(pg) {
        return [
            { key: 'term', vlu: val('iv-term') },
            { key: 'ist', vlu: val('iv-ist') },
            { key: 'dfrom', vlu: val('iv-dfrom') },
            { key: 'dto', vlu: val('iv-dto') },
            { key: 'clientid', vlu: clientId() },
            { key: 'pg', vlu: String(pg || 1) }
        ];
    }

    function find(pg) {
        curPg = pg || 1;
        call('Find', filters(curPg));
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            find(1);
        }, 350);
    }

    function allOwners() {
        var c = el('iv-chips');
        if (c) {
            c.setAttribute('data-cid', '');
            c.innerHTML = '';
        }
        history.replaceState(null, '', 'AdminInvoices');
        find(1);
    }

    function open(id) {
        location.href = 'AdminInvoices?i=' + encodeURIComponent(id);
    }

    function csv(b) {
        if (!lock(b)) {
            return;
        }
        toast('Getting the list ready…');
        call('Csv', filters(1));
    }

    function csvGo() {
        var rows = document.querySelectorAll('#iv-csvall i');
        if (!rows.length) {
            toast('There is nothing in the list to download.');
            return;
        }
        var lines = ['Invoice,Date,Owner,Pet,Visit,Total,Paid,Balance,Status,Due date'];
        for (var i = 0; i < rows.length; i++) {
            lines.push(rows[i].getAttribute('data-csv') || '');
        }
        el('iv-csvall').innerHTML = '';
        var blob = new Blob(['﻿' + lines.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        var d = new Date();
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        a.href = URL.createObjectURL(blob);
        a.download = 'invoices-' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        toast(rows.length + (rows.length === 1 ? ' invoice downloaded.' : ' invoices downloaded.'));
    }

    function shown(focus) {
        el('iv-modal').classList.add('iv-open');
        document.body.style.overflow = 'hidden';
        times(el('iv-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function ask(title, text, btn, fn, danger, extra) {
        pending = fn;
        openModal('<h2>' + esc(title) + '</h2><p class="iv-mlead">' + esc(text) + '</p>' + (extra || '') + '<div class="iv-mfoot"><button type="button" class="iv-btn iv-btn-ghost" onclick="AdminInvoicesJs.closeModal()">Not now</button><button type="button" class="iv-btn ' + (danger ? 'iv-btn-danger' : 'iv-btn-accent') + '" id="iv-yes" onclick="AdminInvoicesJs.yes(this)">' + esc(btn) + '</button></div>');
    }

    function yes(b) {
        var f = pending;
        if (f) {
            f(b);
        }
    }

    function problem(msg) {
        openModal('<h2>The email was not sent</h2><p class="iv-mlead">' + esc(msg) + '</p><div class="iv-mfoot"><button type="button" class="iv-btn iv-btn-accent" onclick="AdminInvoicesJs.closeModal()">OK</button></div>');
    }

    function payForm(b) {
        act('PayForm', [], b);
    }

    function num(v) {
        var t = String(v || '').replace(/[$\s]/g, '');
        if (!/^(\d{1,3}(,\d{3})*|\d+)(\.\d{1,2})?$/.test(t)) {
            return 0;
        }
        return parseFloat(t.replace(/,/g, ''));
    }

    function method() {
        var m = val('iv-pm');
        var l = el('iv-prl');
        var h = el('iv-ph');
        if (!l || !h) {
            return;
        }
        if (m === 'card') {
            l.textContent = 'Last 4 digits of the card';
            h.textContent = 'Never write the full card number.';
        } else if (m === 'cash') {
            l.textContent = 'Note (optional)';
            h.textContent = 'Write the cash the owner hands over. If it is more than the balance, the change is shown.';
        } else if (m === 'check') {
            l.textContent = 'Check number';
            h.textContent = 'The owner writes the invoice number on the check.';
        } else {
            l.textContent = 'What it was';
            h.textContent = 'For example: insurance payment or gift card.';
        }
        change();
    }

    function change() {
        var a = el('iv-pa');
        var h = el('iv-ph');
        if (!a || !h || val('iv-pm') !== 'cash') {
            return;
        }
        var bal = num(a.getAttribute('data-bal'));
        var got = num(a.value);
        h.textContent = got > bal && bal > 0 ? 'Change to give back: $' + (Math.round((got - bal) * 100) / 100).toFixed(2) + '. Only the balance is saved.' : 'Write the cash the owner hands over. If it is more than the balance, the change is shown.';
    }

    function pay() {
        el('iv-e-pay').textContent = '';
        act('Pay', [{ key: 'newid', vlu: pkey }, { key: 'pmethod', vlu: val('iv-pm') }, { key: 'pamount', vlu: val('iv-pa') }, { key: 'pref', vlu: val('iv-pr') }], el('iv-pgo'));
    }

    function voidPay(id, label) {
        ask('Void this payment?', label + ' no longer counts toward the invoice, and the balance goes back up.', 'Void payment', function (b) {
            act('PayVoid', [{ key: 'payid', vlu: id }, { key: 'vreason', vlu: val('iv-vreason') }], b);
        }, true, '<label class="iv-field"><span>Why</span><input id="iv-vreason" type="text" maxlength="200" placeholder="For example: check bounced"></label><em class="iv-err iv-merr" id="iv-e-void"></em>');
        var f = el('iv-vreason');
        if (f) {
            f.focus();
        }
    }

    function voidInv() {
        ask('Void this invoice?', 'A void invoice stays in the list for the record, but nothing is owed on it. This can\'t be undone.', 'Void invoice', function (b) {
            act('VoidInv', [{ key: 'ireason', vlu: val('iv-ireason') }], b);
        }, true, '<label class="iv-field"><span>Why</span><input id="iv-ireason" type="text" maxlength="200" placeholder="For example: made by mistake"></label><em class="iv-err iv-merr" id="iv-e-void"></em>');
        var f = el('iv-ireason');
        if (f) {
            f.focus();
        }
    }

    function saveNote() {
        act('SaveNote', [{ key: 'inote', vlu: el('iv-inote') ? el('iv-inote').value : '' }], el('iv-ngo'));
    }

    function mail(b) {
        act('EmailInv', [], b);
    }

    function print(b) {
        act('PrintView', [], b);
    }

    function doPrint() {
        window.print();
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        find: find,
        typed: typed,
        allOwners: allOwners,
        open: open,
        csv: csv,
        csvGo: csvGo,
        shown: shown,
        yes: yes,
        problem: problem,
        payForm: payForm,
        method: method,
        change: change,
        pay: pay,
        voidPay: voidPay,
        voidInv: voidInv,
        saveNote: saveNote,
        mail: mail,
        print: print,
        doPrint: doPrint,
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

AdminInvoicesJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminInvoicesJs.navPaint();
        AdminInvoicesJs.reveal();
    });
} else {
    AdminInvoicesJs.navPaint();
    AdminInvoicesJs.reveal();
}
