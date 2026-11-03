var AdminBalancesJs = (function () {

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
        $ApiRequest('AdminBalances/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('bl-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bl-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bl-open');
        }, 3400);
    }

    function openNav() {
        el('bl-nav').classList.add('bl-open');
        el('bl-scrim').classList.add('bl-open');
    }

    function closeNav() {
        el('bl-nav').classList.remove('bl-open');
        el('bl-scrim').classList.remove('bl-open');
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
        var open = !box.classList.contains('bl-sopen');
        box.classList.toggle('bl-sopen', open);
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
        var all = document.querySelectorAll('.bl-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('bl-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.bl-sec.bl-shere');
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
        var m = el('bl-menu');
        var w = el('bl-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminBalancesJs.').join('{js}.').split('bl-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('bl-').join('{p}-'));
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
        var n = el('bl-nav');
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
        var a = n.querySelector('.bl-mi.bl-on');
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
        if (el('bl-menu')) {
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
            if (el('bl-menu')) {
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
        var n = el('bl-nav');
        var m = el('bl-menu');
        var w = el('bl-who');
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
            w.innerHTML = wh.split('{p}-').join('bl-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminBalancesJs.').split('{p}-').join('bl-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.bl-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('bl-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.bl-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.bl-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('bl-shere', here);
                all[j].classList.toggle('bl-sopen', open);
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
        el('bl-mbody').innerHTML = html;
        el('bl-modal').classList.add('bl-open');
        document.body.style.overflow = 'hidden';
        times(el('bl-mbody'));
    }

    function closeModal() {
        el('bl-modal').classList.remove('bl-open');
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
        el('bl-main').innerHTML = html;
    }

    var tmr = null;
    var stId = '';
    var picked = {};
    var busy = false;
    var inflight = false;
    var again = false;
    var curPage = 1;

    function st() {
        return [
            { key: 'term', vlu: val('bl-term') },
            { key: 'bucket', vlu: val('bl-bucket') },
            { key: 'sort', vlu: val('bl-sort') },
            { key: 'pastonly', vlu: el('bl-past') && el('bl-past').checked ? '1' : '' },
            { key: 'page', vlu: String(curPage) }
        ];
    }

    function list() {
        curPage = 1;
        send();
    }

    function page(n) {
        curPage = n;
        send();
        window.scrollTo(0, 0);
    }

    function send() {
        clearTimeout(tmr);
        if (inflight) {
            again = true;
            return;
        }
        inflight = true;
        call('List', st());
    }

    function typed() {
        clearTimeout(tmr);
        tmr = setTimeout(list, 320);
    }

    function pastTile() {
        var p = el('bl-past');
        p.checked = !p.checked;
        list();
    }

    function bucketTile(b) {
        var s = el('bl-bucket');
        s.value = s.value === b ? '' : b;
        list();
    }

    function ready(first) {
        inflight = false;
        if (again) {
            again = false;
            send();
        }
        times(el('bl-main'));
        var boxes = document.querySelectorAll('.bl-sel');
        var keep = {};
        for (var i = 0; i < boxes.length; i++) {
            if (picked[boxes[i].value]) {
                boxes[i].checked = true;
                keep[boxes[i].value] = true;
            }
        }
        picked = keep;
        sel();
        if (first) {
            var site = el('bl-site');
            var qc = site ? site.getAttribute('data-qc') || '' : '';
            if (qc) {
                open(qc);
            }
        }
    }

    function open(id) {
        call('Statement', [{ key: 'clientid', vlu: id }]);
    }

    function stOpen(id) {
        stId = id;
        if (!el('bl-modal').classList.contains('bl-open')) {
            openModal(el('bl-mbody').innerHTML);
        }
        times(el('bl-mbody'));
        try {
            history.replaceState(null, '', 'AdminBalances?c=' + encodeURIComponent(id));
        } catch (e) {
            return;
        }
    }

    function stGone() {
        stId = '';
        try {
            history.replaceState(null, '', 'AdminBalances');
        } catch (e) {
            return;
        }
    }

    function closeSt() {
        closeModal();
        if (stId) {
            stGone();
        }
    }

    function payOpen(iid) {
        var f = el('bl-pf-' + iid);
        var b = el('bl-pb-' + iid);
        if (!f) {
            return;
        }
        f.hidden = false;
        if (b) {
            b.hidden = true;
        }
        var a = el('bl-pa-' + iid);
        if (a) {
            a.focus();
            a.select();
        }
    }

    function payClose(iid) {
        var f = el('bl-pf-' + iid);
        var b = el('bl-pb-' + iid);
        if (f) {
            f.hidden = true;
        }
        if (b) {
            b.hidden = false;
        }
    }

    function methodHint(iid) {
        var m = val('bl-pm-' + iid);
        var h = el('bl-ph-' + iid);
        if (h) {
            h.textContent = m === 'card' ? 'Last 4 digits of the card' : m === 'check' ? 'Check number' : 'Note (optional)';
        }
    }

    function hold() {
        if (busy) {
            return false;
        }
        busy = true;
        setTimeout(function () {
            busy = false;
        }, 1500);
        return true;
    }

    function pay(cid, iid, key) {
        if (!hold()) {
            return;
        }
        el('bl-pe-' + iid).textContent = '';
        call('Pay', [
            { key: 'clientid', vlu: cid },
            { key: 'invid', vlu: iid },
            { key: 'method', vlu: val('bl-pm-' + iid) },
            { key: 'amount', vlu: val('bl-pa-' + iid) },
            { key: 'refno', vlu: val('bl-pr-' + iid) },
            { key: 'paykey', vlu: key }
        ]);
    }

    function changed(msg) {
        busy = false;
        times(el('bl-mbody'));
        if (msg) {
            toast(msg);
        }
        send();
    }

    function mail(cid) {
        if (!hold()) {
            return;
        }
        call('Mail', [{ key: 'clientid', vlu: cid }]);
    }

    function print(cid) {
        call('Print', [{ key: 'clientid', vlu: cid }]);
    }

    function doPrint() {
        setTimeout(function () {
            window.print();
        }, 150);
    }

    function boxes() {
        return Array.prototype.slice.call(document.querySelectorAll('.bl-sel'));
    }

    function sel() {
        var all = boxes();
        var on = all.filter(function (b) { return b.checked; });
        picked = {};
        on.forEach(function (b) {
            picked[b.value] = true;
        });
        all.forEach(function (b) {
            var row = b.closest('tr');
            if (row) {
                row.classList.toggle('bl-picked', b.checked);
            }
        });
        var a = el('bl-all');
        if (a) {
            a.checked = all.length > 0 && on.length === all.length;
            a.indeterminate = on.length > 0 && on.length < all.length;
        }
        var bar = el('bl-bulk');
        if (bar) {
            bar.hidden = on.length === 0;
            el('bl-bulkn').textContent = on.length + (on.length === 1 ? ' owner selected' : ' owners selected');
        }
    }

    function selAll(on) {
        boxes().forEach(function (b) {
            b.checked = on;
        });
        sel();
    }

    function clearSel() {
        selAll(false);
    }

    function bulkAsk() {
        var on = boxes().filter(function (b) { return b.checked; });
        if (!on.length) {
            return;
        }
        var withMail = on.filter(function (b) { return b.getAttribute('data-mail') === '1'; }).length;
        var none = on.length - withMail;
        stId = '';
        openModal('<h2>Email statements</h2><p class="bl-mlead">Each owner gets one email with all of their unpaid invoices, their age and the total due.</p><ul class="bl-rows"><li class="bl-rl"><span class="bl-rw">Will be emailed</span><span class="bl-rt"><b>' + withMail + '</b> ' + (withMail === 1 ? 'owner' : 'owners') + '</span></li><li class="bl-rl"><span class="bl-rw">No email on file</span><span class="bl-rt"><b>' + none + '</b> ' + (none === 1 ? 'owner is' : 'owners are') + ' skipped</span></li></ul><div class="bl-mfoot"><button type="button" class="bl-btn bl-btn-ghost" onclick="AdminBalancesJs.closeModal()">Cancel</button><button type="button" class="bl-btn bl-btn-accent" id="bl-bgo" onclick="AdminBalancesJs.bulk()"' + (withMail ? '' : ' disabled') + '>Email ' + withMail + (withMail === 1 ? ' statement' : ' statements') + '</button></div>');
    }

    function bulk() {
        if (!hold()) {
            return;
        }
        var ids = boxes().filter(function (b) { return b.checked; }).map(function (b) { return b.value; });
        var b = el('bl-bgo');
        if (b) {
            b.disabled = true;
            b.textContent = 'Sending…';
        }
        call('Bulk', [{ key: 'clientids', vlu: ids.join(',') }]);
    }

    function bulkDone() {
        busy = false;
        picked = {};
        send();
    }

    function failed() {
        busy = false;
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && el('bl-modal').classList.contains('bl-open')) {
                closeSt();
            }
        });
        call('Load', []);
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
        closeModal: closeSt,
        signOut: signOut,
        out: out,
        expired: expired,
        list: list,
        page: page,
        typed: typed,
        pastTile: pastTile,
        bucketTile: bucketTile,
        ready: ready,
        open: open,
        stOpen: stOpen,
        stGone: stGone,
        payOpen: payOpen,
        payClose: payClose,
        methodHint: methodHint,
        pay: pay,
        changed: changed,
        mail: mail,
        print: print,
        doPrint: doPrint,
        sel: sel,
        selAll: selAll,
        clearSel: clearSel,
        bulkAsk: bulkAsk,
        bulk: bulk,
        bulkDone: bulkDone,
        failed: failed
    };
})();

AdminBalancesJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminBalancesJs.navPaint();
        AdminBalancesJs.reveal();
    });
} else {
    AdminBalancesJs.navPaint();
    AdminBalancesJs.reveal();
}
