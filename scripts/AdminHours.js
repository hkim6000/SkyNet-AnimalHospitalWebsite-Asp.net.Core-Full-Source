var AdminHoursJs = (function () {

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
        $ApiRequest('AdminHours/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('hr-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('hr-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('hr-open');
        }, 3400);
    }

    function openNav() {
        el('hr-nav').classList.add('hr-open');
        el('hr-scrim').classList.add('hr-open');
    }

    function closeNav() {
        el('hr-nav').classList.remove('hr-open');
        el('hr-scrim').classList.remove('hr-open');
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
        var open = !box.classList.contains('hr-sopen');
        box.classList.toggle('hr-sopen', open);
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
        var all = document.querySelectorAll('.hr-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('hr-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.hr-sec.hr-shere');
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
        var m = el('hr-menu');
        var w = el('hr-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminHoursJs.').join('{js}.').split('hr-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('hr-').join('{p}-'));
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
        var n = el('hr-nav');
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
        var a = n.querySelector('.hr-mi.hr-on');
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
        if (el('hr-menu')) {
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
            if (el('hr-menu')) {
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
        var n = el('hr-nav');
        var m = el('hr-menu');
        var w = el('hr-who');
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
            w.innerHTML = wh.split('{p}-').join('hr-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminHoursJs.').split('{p}-').join('hr-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.hr-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('hr-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.hr-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.hr-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('hr-shere', here);
                all[j].classList.toggle('hr-sopen', open);
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
        el('hr-mbody').innerHTML = html;
        el('hr-modal').classList.add('hr-open');
        document.body.style.overflow = 'hidden';
        times(el('hr-mbody'));
    }

    function closeModal() {
        el('hr-modal').classList.remove('hr-open');
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
        el('hr-main').innerHTML = html;
    }

    var locked = false;
    var ltimer = null;
    var ctimer = null;
    var newKey = '';
    var curTab = 'week';

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
            b.setAttribute('data-hrlk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-hrlk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-hrlk');
        }
    }

    function act(method, list, b) {
        if (!lock(b)) {
            return false;
        }
        call(method, list);
        return true;
    }

    function site(k) {
        var s = el('hr-site');
        return s ? (s.getAttribute('data-' + k) || '') : '';
    }

    function reveal() {
        call('Load', [{ key: 'opentab', vlu: site('qt') }]);
    }

    function tab(t) {
        var secs = document.querySelectorAll('.hr-sect');
        var found = false;
        for (var i = 0; i < secs.length; i++) {
            if (secs[i].getAttribute('data-tab') === t) {
                found = true;
            }
        }
        if (!found) {
            t = 'week';
        }
        curTab = t;
        for (var j = 0; j < secs.length; j++) {
            secs[j].hidden = secs[j].getAttribute('data-tab') !== t;
        }
        var tabs = document.querySelectorAll('.hr-tab');
        for (var k = 0; k < tabs.length; k++) {
            var on = tabs[k].getAttribute('data-tab') === t;
            tabs[k].classList.toggle('hr-tab-on', on);
            tabs[k].setAttribute('aria-selected', on ? 'true' : 'false');
        }
        history.replaceState(null, '', 'AdminHours' + (t !== 'week' ? '?t=' + t : ''));
    }

    function ready(t) {
        times(el('hr-main'));
        tab(t || curTab);
        offFilter();
    }

    function shown(focus) {
        el('hr-modal').classList.add('hr-open');
        document.body.style.overflow = 'hidden';
        times(el('hr-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function week(id) {
        call('WeekForm', [{ key: 'staffid', vlu: id }]);
    }

    function dayOn(d) {
        var r = el('hr-wr-' + d);
        if (r) {
            r.classList.toggle('hr-wr-off', !el('hr-w-on-' + d).checked);
        }
    }

    function copyMon() {
        var keys = ['from', 'to', 'bf', 'bt'];
        for (var d = 2; d <= 5; d++) {
            el('hr-w-on-' + d).checked = el('hr-w-on-1').checked;
            for (var i = 0; i < keys.length; i++) {
                el('hr-w-' + keys[i] + '-' + d).value = el('hr-w-' + keys[i] + '-1').value;
            }
            dayOn(d);
        }
        toast('Monday copied to Tuesday – Friday. Save to keep it.');
    }

    function weekSave() {
        var parts = [];
        for (var d = 0; d < 7; d++) {
            parts.push([d, el('hr-w-on-' + d).checked ? '1' : '0', val('hr-w-from-' + d), val('hr-w-to-' + d), val('hr-w-bf-' + d), val('hr-w-bt-' + d)].join('|'));
        }
        act('WeekSave', [{ key: 'staffid', vlu: val('hr-w-id') }, { key: 'week', vlu: parts.join(';') }], el('hr-w-go'));
        return false;
    }

    function offForm(id, b) {
        if (!lock(b)) {
            return;
        }
        newKey = hex();
        call('OffForm', [{ key: 'offid', vlu: id || '' }]);
    }

    function offShown() {
        shown('hr-o-who');
    }

    function offCheck() {
        clearTimeout(ctimer);
        ctimer = setTimeout(function () {
            call('OffCheck', [{ key: 'offstaff', vlu: val('hr-o-who') }, { key: 'ofrom', vlu: val('hr-o-from') }, { key: 'oto', vlu: val('hr-o-to') }]);
        }, 250);
    }

    function offSave() {
        clearTimeout(ctimer);
        act('OffSave', [{ key: 'offid', vlu: val('hr-o-id') }, { key: 'newid', vlu: newKey }, { key: 'offstaff', vlu: val('hr-o-who') }, { key: 'ofrom', vlu: val('hr-o-from') }, { key: 'oto', vlu: val('hr-o-to') }, { key: 'onote', vlu: val('hr-o-note') }], el('hr-o-go'));
        return false;
    }

    function offAsk(id, label) {
        openModal('<h2>Delete this time off?</h2><p class="hr-mlead">' + esc(label) + '. They can be booked on these days again.</p><div class="hr-mfoot"><button type="button" class="hr-btn hr-btn-ghost" onclick="AdminHoursJs.closeModal()">Not now</button><button type="button" class="hr-btn hr-btn-danger" data-id="' + esc(id) + '" onclick="AdminHoursJs.offDel(this)">Delete</button></div>');
    }

    function offDel(b) {
        act('OffDelete', [{ key: 'offid', vlu: b.getAttribute('data-id') || '' }], b);
    }

    function clashes(id) {
        call('Clashes', [{ key: 'offid', vlu: id }]);
    }

    function offFilter() {
        var body = el('hr-offrows');
        var who = el('hr-offwho') ? el('hr-offwho').value : '';
        if (!body) {
            return;
        }
        var rows = body.querySelectorAll('tr[data-staff]');
        for (var i = 0; i < rows.length; i++) {
            rows[i].hidden = who !== '' && rows[i].getAttribute('data-staff') !== who;
        }
    }

    function holCheck() {
        call('HolCheck', [{ key: 'hday', vlu: val('hr-h-date') }]);
    }

    function holSave() {
        act('HolSave', [{ key: 'hday', vlu: val('hr-h-date') }, { key: 'hname', vlu: val('hr-h-name') }], el('hr-h-go'));
        return false;
    }

    function holAsk(day, name) {
        openModal('<h2>Delete this holiday?</h2><p class="hr-mlead">' + esc(name) + ' (' + esc(day) + '). The clinic opens as usual that day and people can book again.</p><div class="hr-mfoot"><button type="button" class="hr-btn hr-btn-ghost" onclick="AdminHoursJs.closeModal()">Not now</button><button type="button" class="hr-btn hr-btn-danger" data-day="' + esc(day) + '" onclick="AdminHoursJs.holDel(this)">Delete holiday</button></div>');
    }

    function holDel(b) {
        act('HolDelete', [{ key: 'hday', vlu: b.getAttribute('data-day') || '' }], b);
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        unlock: unlock,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        sec: sec,
        secs: secs,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        tab: tab,
        shown: shown,
        week: week,
        dayOn: dayOn,
        copyMon: copyMon,
        weekSave: weekSave,
        offForm: offForm,
        offShown: offShown,
        offCheck: offCheck,
        offSave: offSave,
        offAsk: offAsk,
        offDel: offDel,
        clashes: clashes,
        offFilter: offFilter,
        holCheck: holCheck,
        holSave: holSave,
        holAsk: holAsk,
        holDel: holDel
    };
})();

AdminHoursJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminHoursJs.navPaint();
        AdminHoursJs.reveal();
    });
} else {
    AdminHoursJs.navPaint();
    AdminHoursJs.reveal();
}
