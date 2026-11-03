var AdminCheckoutJs = (function () {

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
        $ApiRequest('AdminCheckout/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('ck-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ck-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ck-open');
        }, 3400);
    }

    function openNav() {
        el('ck-nav').classList.add('ck-open');
        el('ck-scrim').classList.add('ck-open');
    }

    function closeNav() {
        el('ck-nav').classList.remove('ck-open');
        el('ck-scrim').classList.remove('ck-open');
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
        var open = !box.classList.contains('ck-sopen');
        box.classList.toggle('ck-sopen', open);
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
        var all = document.querySelectorAll('.ck-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('ck-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.ck-sec.ck-shere');
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
        var m = el('ck-menu');
        var w = el('ck-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminCheckoutJs.').join('{js}.').split('ck-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('ck-').join('{p}-'));
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
        var n = el('ck-nav');
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
        var a = n.querySelector('.ck-mi.ck-on');
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
        if (el('ck-menu')) {
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
            if (el('ck-menu')) {
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
        var n = el('ck-nav');
        var m = el('ck-menu');
        var w = el('ck-who');
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
            w.innerHTML = wh.split('{p}-').join('ck-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminCheckoutJs.').split('{p}-').join('ck-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.ck-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('ck-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.ck-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.ck-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('ck-shere', here);
                all[j].classList.toggle('ck-sopen', open);
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
        el('ck-mbody').innerHTML = html;
        el('ck-modal').classList.add('ck-open');
        document.body.style.overflow = 'hidden';
        times(el('ck-mbody'));
    }

    function closeModal() {
        el('ck-modal').classList.remove('ck-open');
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
        el('ck-main').innerHTML = html;
    }

    var openV = '';
    var mode = '';
    var locked = false;
    var ltimer = null;
    var dtimer = null;
    var pending = null;
    var fkey = '';
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
            b.setAttribute('data-cklk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-cklk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-cklk');
        }
    }

    function act(method, list, b) {
        if (!lock(b)) {
            return false;
        }
        var data = (list || []).slice();
        data.push({ key: 'visitid', vlu: openV });
        call(method, data);
        return true;
    }

    function reveal() {
        var s = el('ck-site');
        openV = s ? (s.getAttribute('data-open') || '') : '';
        call('Load', [{ key: 'openv', vlu: openV }]);
    }

    function ready(m) {
        mode = m;
        fkey = hex();
        pkey = hex();
        times(el('ck-main'));
        clearInterval(dtimer);
        if (m === 'desk') {
            dtimer = setInterval(function () {
                if (!el('ck-modal').classList.contains('ck-open') && !locked && document.visibilityState === 'visible') {
                    call('Desk', []);
                }
            }, 60000);
        }
        if (m === 'work') {
            method();
        }
    }

    function go(id) {
        location.href = 'AdminCheckout?v=' + encodeURIComponent(id);
    }

    function desk() {
        call('Desk', []);
        toast('Refreshed.');
    }

    function shake(id) {
        var e = el(id);
        var d = e && e.closest ? e.closest('details') : null;
        if (d) {
            d.open = true;
        }
        if (e && e.scrollIntoView) {
            e.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    }

    function ask(title, text, btn, fn, danger, extra) {
        pending = fn;
        openModal('<h2>' + esc(title) + '</h2><p class="ck-mlead">' + esc(text) + '</p>' + (extra || '') + '<div class="ck-mfoot"><button type="button" class="ck-btn ck-btn-ghost" onclick="AdminCheckoutJs.closeModal()">Not now</button><button type="button" class="ck-btn ' + (danger ? 'ck-btn-danger' : 'ck-btn-accent') + '" id="ck-yes" onclick="AdminCheckoutJs.yes(this)">' + esc(btn) + '</button></div>');
    }

    function yes(b) {
        var f = pending;
        if (f) {
            f(b);
        }
    }

    function rowId(n) {
        var r = el('ck-lr-' + n);
        return r ? (r.getAttribute('data-id') || '') : '';
    }

    function saveLine(n) {
        var id = rowId(n);
        if (!id) {
            return;
        }
        if (!act('LineSave', [{ key: 'ilineid', vlu: id }, { key: 'lqty', vlu: val('ck-lq-' + n) }, { key: 'lprice', vlu: val('ck-lp-' + n) }], null)) {
            setTimeout(function () {
                saveLine(n);
            }, 600);
        }
    }

    function removeLine(id, b) {
        act('LineRemove', [{ key: 'ilineid', vlu: id }], b);
    }

    function restoreLine(id, b) {
        act('LineRestore', [{ key: 'ilineid', vlu: id }], b);
    }

    function addLine() {
        var e = el('ck-e-add');
        if (!val('ck-aitem')) {
            e.textContent = 'Pick an item from the list.';
            return;
        }
        e.textContent = '';
        act('LineAdd', [{ key: 'newid', vlu: fkey }, { key: 'itemid', vlu: val('ck-aitem') }, { key: 'aqty', vlu: val('ck-aqty') }], el('ck-ago'));
    }

    function discount() {
        act('Discount', [{ key: 'dmode', vlu: val('ck-dmode') }, { key: 'dval', vlu: val('ck-dval') }, { key: 'dnote', vlu: val('ck-dnote') }], el('ck-dgo'));
    }

    function noDiscount(b) {
        act('Discount', [{ key: 'dclear', vlu: '1' }], b);
    }

    function method() {
        var m = val('ck-pm');
        var l = el('ck-prl');
        var h = el('ck-ph');
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

    function num(v) {
        var t = String(v || '').replace(/[$\s]/g, '');
        if (!/^(\d{1,3}(,\d{3})*|\d+)(\.\d{1,2})?$/.test(t)) {
            return 0;
        }
        return parseFloat(t.replace(/,/g, ''));
    }

    function change() {
        var a = el('ck-pa');
        var h = el('ck-ph');
        if (!a || !h || val('ck-pm') !== 'cash') {
            return;
        }
        var bal = num(a.getAttribute('data-bal'));
        var got = num(a.value);
        if (got > bal && bal > 0) {
            h.textContent = 'Change to give back: $' + (Math.round((got - bal) * 100) / 100).toFixed(2) + '. Only the balance is saved.';
        } else {
            h.textContent = 'Write the cash the owner hands over. If it is more than the balance, the change is shown.';
        }
    }

    function pay() {
        el('ck-e-pay').textContent = '';
        act('Pay', [{ key: 'newid', vlu: pkey }, { key: 'pmethod', vlu: val('ck-pm') }, { key: 'pamount', vlu: val('ck-pa') }, { key: 'pref', vlu: val('ck-pr') }], el('ck-pgo'));
    }

    function changeDue(msg) {
        openModal('<h2>Give change</h2><p class="ck-change">' + esc(msg) + '</p><div class="ck-mfoot"><button type="button" class="ck-btn ck-btn-accent" onclick="AdminCheckoutJs.closeModal()">Done</button></div>');
    }

    function voidPay(id, label) {
        ask('Void this payment?', label + ' no longer counts toward the invoice. Use this for a mistake or a refused card.', 'Void payment', function (b) {
            act('PayVoid', [{ key: 'payid', vlu: id }, { key: 'vreason', vlu: val('ck-vreason') }], b);
        }, true, '<label class="ck-field"><span>Why</span><input id="ck-vreason" type="text" maxlength="200" placeholder="For example: card was declined"></label><em class="ck-err ck-merr" id="ck-e-void"></em>');
        var f = el('ck-vreason');
        if (f) {
            f.focus();
        }
    }

    function finish(b) {
        act('Finish', [{ key: 'later', vlu: '0' }], b);
    }

    function bill(bal, due) {
        ask('Bill the owner?', 'The visit closes and ' + bal + ' stays on the owner\'s account, due ' + due + '. The invoice can\'t be changed after this.', 'Bill the owner', function (b) {
            act('Finish', [{ key: 'later', vlu: '1' }], b);
        }, false);
    }

    function mail(b) {
        act('EmailOwner', [], b);
    }

    function print(b) {
        act('PrintView', [], b);
    }

    function printShown() {
        el('ck-modal').classList.add('ck-open');
        document.body.style.overflow = 'hidden';
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
        go: go,
        desk: desk,
        shake: shake,
        yes: yes,
        saveLine: saveLine,
        removeLine: removeLine,
        restoreLine: restoreLine,
        addLine: addLine,
        discount: discount,
        noDiscount: noDiscount,
        method: method,
        change: change,
        pay: pay,
        changeDue: changeDue,
        voidPay: voidPay,
        finish: finish,
        bill: bill,
        mail: mail,
        print: print,
        printShown: printShown,
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

AdminCheckoutJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminCheckoutJs.navPaint();
        AdminCheckoutJs.reveal();
    });
} else {
    AdminCheckoutJs.navPaint();
    AdminCheckoutJs.reveal();
}
