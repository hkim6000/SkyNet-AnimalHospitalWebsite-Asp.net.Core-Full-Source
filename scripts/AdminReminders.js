var AdminRemindersJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    var curPage = 1;

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
        $ApiRequest('AdminReminders/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('rm-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rm-open');
        }, 3400);
    }

    function openNav() {
        el('rm-nav').classList.add('rm-open');
        el('rm-scrim').classList.add('rm-open');
    }

    function closeNav() {
        el('rm-nav').classList.remove('rm-open');
        el('rm-scrim').classList.remove('rm-open');
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
        var open = !box.classList.contains('rm-sopen');
        box.classList.toggle('rm-sopen', open);
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
        var all = document.querySelectorAll('.rm-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('rm-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.rm-sec.rm-shere');
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
        var m = el('rm-menu');
        var w = el('rm-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminRemindersJs.').join('{js}.').split('rm-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('rm-').join('{p}-'));
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
        var n = el('rm-nav');
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
        var a = n.querySelector('.rm-mi.rm-on');
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
        if (el('rm-menu')) {
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
            if (el('rm-menu')) {
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
        var n = el('rm-nav');
        var m = el('rm-menu');
        var w = el('rm-who');
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
            w.innerHTML = wh.split('{p}-').join('rm-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminRemindersJs.').split('{p}-').join('rm-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.rm-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('rm-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.rm-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.rm-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('rm-shere', here);
                all[j].classList.toggle('rm-sopen', open);
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
        el('rm-mbody').innerHTML = html;
        el('rm-modal').classList.add('rm-open');
        document.body.style.overflow = 'hidden';
        times(el('rm-mbody'));
    }

    function closeModal() {
        el('rm-modal').classList.remove('rm-open');
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
        el('rm-main').innerHTML = html;
    }

    var tmr = null;
    var pend = [];
    var logState = 0;

    function st() {
        return [
            { key: 'kind', vlu: val('rm-kind') },
            { key: 'show', vlu: val('rm-show') },
            { key: 'quiet', vlu: val('rm-quiet') },
            { key: 'term', vlu: val('rm-term') },
            { key: 'page', vlu: String(curPage) }
        ];
    }

    function ready() {
        times(el('rm-main'));
        sel();
    }

    function list() {
        clearTimeout(tmr);
        curPage = 1;
        call('List', st());
    }

    function page(n) {
        curPage = n;
        call('List', st());
        window.scrollTo(0, 0);
    }

    function typed() {
        clearTimeout(tmr);
        tmr = setTimeout(list, 320);
    }

    function boxes(root) {
        return Array.prototype.slice.call((root || document).querySelectorAll('.rm-sel'));
    }

    function sel() {
        var all = boxes();
        var on = all.filter(function (b) { return b.checked; });
        var groups = document.querySelectorAll('.rm-grp');
        for (var i = 0; i < groups.length; i++) {
            var g = groups[i].querySelector('.rm-gsel');
            if (!g) {
                continue;
            }
            var mine = boxes(groups[i]);
            var n = mine.filter(function (b) { return b.checked; }).length;
            g.checked = n > 0 && n === mine.length;
            g.indeterminate = n > 0 && n < mine.length;
        }
        all.forEach(function (b) {
            var row = b.closest('.rm-row');
            if (row) {
                row.classList.toggle('rm-picked', b.checked);
            }
        });
        var bar = el('rm-bulk');
        if (bar) {
            bar.hidden = on.length === 0;
            el('rm-bulkn').textContent = on.length + (on.length === 1 ? ' reminder selected' : ' reminders selected');
        }
    }

    function grp(cb) {
        boxes(cb.closest('.rm-grp')).forEach(function (b) {
            b.checked = cb.checked;
        });
        sel();
    }

    function clearSel() {
        boxes().forEach(function (b) {
            b.checked = false;
        });
        sel();
    }

    function keyOf(row) {
        return row ? row.getAttribute('data-k') || '' : '';
    }

    function log(keys) {
        pend = keys.filter(function (k) { return !!k; });
        if (!pend.length) {
            toast('Choose at least one reminder first.');
            return;
        }
        call('LogForm', st().concat([{ key: 'rkeys', vlu: pend.join(',') }]));
    }

    function logOne(btn) {
        log([keyOf(btn.closest('.rm-row'))]);
    }

    function logGroup(btn) {
        var rows = btn.closest('.rm-grp').querySelectorAll('.rm-row');
        log(Array.prototype.map.call(rows, keyOf));
    }

    function logSel() {
        log(boxes().filter(function (b) { return b.checked; }).map(function (b) { return keyOf(b.closest('.rm-row')); }));
    }

    function shown() {
        el('rm-modal').classList.add('rm-open');
        el('rm-modal').scrollTop = 0;
        document.body.style.overflow = 'hidden';
        var f = document.querySelector('#rm-mbody input[name=rm-how]:checked');
        if (f && window.matchMedia('(min-width: 900px)').matches) {
            f.focus();
        }
    }

    function save() {
        var how = document.querySelector('#rm-mbody input[name=rm-how]:checked');
        var b = el('rm-save');
        if (b) {
            b.disabled = true;
        }
        call('LogSave', st().concat([
            { key: 'rkeys', vlu: pend.join(',') },
            { key: 'how', vlu: how ? how.value : '' },
            { key: 'outcome', vlu: val('rm-out') },
            { key: 'note', vlu: val('rm-note') }
        ]));
    }

    function unlock() {
        var b = el('rm-save');
        if (b) {
            b.disabled = false;
        }
    }

    function saved() {
        pend = [];
        closeModal();
        if (logState === 2) {
            logState = 1;
        }
    }

    function tab(name) {
        var tabs = document.querySelectorAll('.rm-tab');
        for (var i = 0; i < tabs.length; i++) {
            var on = tabs[i].getAttribute('data-tab') === name;
            tabs[i].classList.toggle('rm-on', on);
            tabs[i].setAttribute('aria-selected', on ? 'true' : 'false');
        }
        el('rm-pane-due').classList.toggle('rm-pane-off', name !== 'due');
        el('rm-pane-log').classList.toggle('rm-pane-off', name !== 'log');
        var bar = el('rm-bulk');
        if (bar) {
            bar.classList.toggle('rm-pane-off', name !== 'due');
        }
        if (name === 'log' && logState !== 2) {
            call('Contacts', []);
        }
    }

    function logged() {
        logState = 2;
        times(el('rm-log'));
    }

    function csv() {
        var rows = document.querySelectorAll('#rm-csvall i');
        if (!rows.length) {
            toast('There is nothing in the list to download.');
            return;
        }
        var lines = ['Owner,Contact preference,Phone,Email,Pet,Species,What is due,Kind,Due date,Status,Last contact,Last contact note'];
        for (var i = 0; i < rows.length; i++) {
            lines.push(rows[i].getAttribute('data-csv') || '');
        }
        var blob = new Blob(['﻿' + lines.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        var d = new Date();
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        a.href = URL.createObjectURL(blob);
        a.download = 'reminders-' + d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
        }, 2000);
        toast(rows.length + (rows.length === 1 ? ' reminder downloaded.' : ' reminders downloaded.'));
    }

    function reveal() {
        call('Load', []);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        list: list,
        typed: typed,
        sel: sel,
        grp: grp,
        clearSel: clearSel,
        logOne: logOne,
        logGroup: logGroup,
        logSel: logSel,
        shown: shown,
        save: save,
        unlock: unlock,
        saved: saved,
        tab: tab,
        logged: logged,
        csv: csv,
        page: page,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminRemindersJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminRemindersJs.navPaint();
        AdminRemindersJs.reveal();
    });
} else {
    AdminRemindersJs.navPaint();
    AdminRemindersJs.reveal();
}
