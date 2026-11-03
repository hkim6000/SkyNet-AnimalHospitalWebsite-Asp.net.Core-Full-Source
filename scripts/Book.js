var BookJs = (function () {

    var ttimer = null;
    var busy = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Book/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('bk-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bk-open');
        }, 3200);
    }

    function openNav() {
        el('bk-nav').classList.add('bk-open');
        el('bk-scrim').classList.add('bk-open');
    }

    function closeNav() {
        el('bk-nav').classList.remove('bk-open');
        el('bk-scrim').classList.remove('bk-open');
    }

    function applyTheme(theme) {
        el('bk-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('bk-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    var KEYS = { sp: 'ksp', size: 'ksz', sv: 'ksv', p: 'kp', pa: 'kpa', t: 'kt', wk: 'kwk', day: 'kday', pref: 'kpref', team: 'kteam', at: 'kat' };
    var kept = {};

    function st() {
        var s = el('bk-st');
        var o = {};
        Object.keys(KEYS).forEach(function (k) {
            o[k] = s ? (s.getAttribute('data-' + k) || '') : '';
        });
        return o;
    }

    function keep() {
        var f = el('bk-dform');
        if (!f) {
            return;
        }
        var list = f.querySelectorAll('input, select, textarea');
        for (var i = 0; i < list.length; i++) {
            var n = list[i];
            if (n.type === 'radio') {
                if (n.checked) {
                    kept['r:' + n.name] = n.value;
                }
            } else if (n.type === 'checkbox') {
                kept['c:' + n.id] = n.checked;
            } else if (n.id) {
                kept['v:' + n.id] = n.value;
            }
        }
    }

    function restore() {
        var f = el('bk-dform');
        if (!f) {
            return;
        }
        var list = f.querySelectorAll('input, select, textarea');
        for (var i = 0; i < list.length; i++) {
            var n = list[i];
            if (n.type === 'radio') {
                if (kept.hasOwnProperty('r:' + n.name) && !n.disabled) {
                    n.checked = kept['r:' + n.name] === n.value;
                }
            } else if (n.type === 'checkbox') {
                if (kept.hasOwnProperty('c:' + n.id)) {
                    n.checked = kept['c:' + n.id];
                }
            } else if (n.id && kept.hasOwnProperty('v:' + n.id) && !n.value) {
                n.value = kept['v:' + n.id];
            }
        }
    }

    function base(o) {
        var list = [];
        Object.keys(KEYS).forEach(function (k) {
            list.push({ key: KEYS[k], vlu: o[k] || '' });
        });
        return list;
    }

    function send(o) {
        keep();
        call('Step', base(o));
    }

    function pickSpecies(k) {
        var o = st();
        if (o.sp !== k) {
            o.size = '';
            o.t = '';
            o.pa = '';
            o.day = '';
        }
        o.sp = k;
        if (o.sv && !o.p && o.pref) {
            o.p = o.pref;
        }
        send(o);
    }

    function pickSize(k) {
        var o = st();
        o.size = k;
        o.t = '';
        o.pa = '';
        o.day = '';
        if (o.sv && !o.p && o.pref) {
            o.p = o.pref;
        }
        send(o);
    }

    function pickService(k) {
        var o = st();
        o.sv = k;
        o.t = '';
        o.pa = '';
        o.day = '';
        if (o.pref && !o.p) {
            o.p = o.pref;
        }
        send(o);
    }

    function pickProvider(id) {
        var o = st();
        o.p = id;
        o.t = '';
        o.pa = '';
        o.day = '';
        send(o);
    }

    function pickTime(t, pid) {
        var o = st();
        o.t = t;
        o.pa = pid;
        send(o);
    }

    function week(d) {
        var o = st();
        o.wk = d;
        o.day = '';
        send(o);
    }

    function day(d) {
        var o = st();
        o.day = d;
        send(o);
    }

    function go(step) {
        var o = st();
        if (step === 'pet') {
            o = { pref: o.pref, team: o.team, sv: o.sv, sp: o.sp, p: o.p, at: o.at };
            o.sp = '';
            o.size = '';
        } else if (step === 'sv') {
            o = { pref: o.pref, team: o.team, sp: o.sp, size: o.size };
        } else if (step === 'p') {
            o.p = '';
            o.t = '';
            o.pa = '';
            o.day = '';
        } else if (step === 't') {
            o.t = '';
            o.pa = '';
        }
        send(o);
    }

    function filter(btn, team) {
        var chips = btn.parentNode.querySelectorAll('.bk-chip');
        for (var i = 0; i < chips.length; i++) {
            chips[i].classList.toggle('bk-act', chips[i] === btn);
        }
        var opts = document.querySelectorAll('.bk-opt');
        for (var j = 0; j < opts.length; j++) {
            opts[j].hidden = team !== '' && opts[j].getAttribute('data-sp') !== team;
        }
        var s = el('bk-st');
        if (s) {
            s.setAttribute('data-team', team);
        }
        url();
    }

    function url() {
        var o = st();
        var q = [];
        if (o.sp) {
            q.push('sp=' + encodeURIComponent(o.sp));
        }
        if (o.sv) {
            q.push('sv=' + encodeURIComponent(o.sv));
        }
        if (o.p && o.p !== 'any') {
            q.push('p=' + encodeURIComponent(o.p));
        } else if (!o.sv && o.pref) {
            q.push('p=' + encodeURIComponent(o.pref));
        }
        if (o.team && !o.sv) {
            q.push('t=' + encodeURIComponent(o.team));
        }
        try {
            history.replaceState(null, '', 'Book' + (q.length ? '?' + q.join('&') : ''));
        } catch (e) {
        }
    }

    function after() {
        url();
        restore();
        var cur = el('bk-cur');
        if (cur) {
            var r = cur.getBoundingClientRect();
            if (r.top < 70 || r.top > window.innerHeight * 0.6) {
                cur.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            var f = el('bk-first');
            if (f && !f.value) {
                setTimeout(function () {
                    f.focus({ preventScroll: true });
                }, 350);
            }
        }
    }

    function radio(name) {
        var r = document.querySelector('input[name="' + name + '"]:checked');
        return r ? r.value : '';
    }

    function unlock() {
        busy = false;
        var b = el('bk-cbtn');
        if (b) {
            b.disabled = false;
        }
    }

    function confirm() {
        if (busy) {
            return false;
        }
        busy = true;
        var b = el('bk-cbtn');
        if (b) {
            b.disabled = true;
        }
        var o = st();
        var f = el('bk-dform');
        var list = base(o);
        list.push({ key: 'newp', vlu: radio('bk-new') });
        ['first', 'last', 'phone', 'email', 'contact', 'pname', 'breed', 'agey', 'agem', 'weight', 'note'].forEach(function (k) {
            list.push({ key: k, vlu: val('bk-' + k) });
        });
        list.push({ key: 'sex', vlu: radio('bk-sex') });
        list.push({ key: 'fixed', vlu: el('bk-fixed').checked ? '1' : '' });
        list.push({ key: 'agree', vlu: el('bk-agree').checked ? '1' : '' });
        list.push({ key: 'ftok', vlu: f.getAttribute('data-ft') || '' });
        list.push({ key: 'hp', vlu: val('bk-hp') });
        var errs = f.querySelectorAll('.bk-err');
        for (var j = 0; j < errs.length; j++) {
            errs[j].textContent = '';
        }
        keep();
        call('Confirm', list);
        return false;
    }

    function form(tok) {
        var f = el('bk-dform');
        if (f) {
            f.setAttribute('data-ft', tok);
        }
    }

    function invalid() {
        var e = document.querySelector('#bk-dform .bk-err:not(:empty)');
        if (e) {
            var box = e.parentNode;
            box.scrollIntoView({ behavior: 'smooth', block: 'center' });
            var i = box.querySelector('input, select, textarea');
            if (i) {
                i.focus({ preventScroll: true });
            }
        }
    }

    function booked() {
        kept = {};
        try {
            history.replaceState(null, '', 'Book');
        } catch (e) {
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
        var b = el('bk-booked');
        if (b) {
            b.focus({ preventScroll: true });
        }
    }

    function icsDate(s) {
        return String(s || '').replace(/[^0-9TZ]/g, '');
    }

    function ics(box) {
        var b = el(box);
        if (!b) {
            return;
        }
        var esc = function (s) {
            return String(s || '').replace(/([,;\\])/g, '\\$1').replace(/\r?\n/g, '\\n');
        };
        var ev = [];
        try {
            ev = JSON.parse(b.getAttribute('data-ev') || '[]');
        } catch (e) {
            ev = [];
        }
        var stamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+/, '');
        var lines = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//AnimalHospital//Booking//EN', 'CALSCALE:GREGORIAN'];
        ev.forEach(function (x) {
            lines.push('BEGIN:VEVENT', 'UID:' + esc(x.u) + '@animalhospital', 'DTSTAMP:' + stamp, 'DTSTART:' + icsDate(x.s), 'DTEND:' + icsDate(x.e), 'SUMMARY:' + esc(x.t), 'LOCATION:' + esc(b.getAttribute('data-loc')), 'DESCRIPTION:' + esc(x.d), 'END:VEVENT');
        });
        lines.push('END:VCALENDAR');
        var blob = new Blob([lines.join('\r\n')], { type: 'text/calendar' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = 'booking-' + b.getAttribute('data-code') + '.ics';
        document.body.appendChild(a);
        a.click();
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
            a.remove();
        }, 500);
    }

    function start() {
        document.addEventListener('change', function (e) {
            var t = e.target;
            if (t && t.name && (t.name.indexOf('bk-q') === 0 || t.name === 'bk-sex')) {
                var x = el('bk-e-' + t.name.replace('bk-', ''));
                if (x) {
                    x.textContent = '';
                }
            }
            if (t && t.id === 'bk-agree') {
                el('bk-e-agree').textContent = '';
            }
        });
        if (el('bk-st') && (el('bk-st').getAttribute('data-sp') || el('bk-st').getAttribute('data-sv'))) {
            url();
        }
    }

    function reveal() {
        if (!document.documentElement.getAttribute('lang')) {
            document.documentElement.setAttribute('lang', 'en');
        }
        var stored = '';
        try {
            stored = localStorage.getItem('cl-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('bk-', 'bk-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        pickSpecies: pickSpecies,
        pickSize: pickSize,
        pickService: pickService,
        pickProvider: pickProvider,
        pickTime: pickTime,
        week: week,
        day: day,
        go: go,
        filter: filter,
        after: after,
        confirm: confirm,
        unlock: unlock,
        invalid: invalid,
        form: form,
        booked: booked,
        ics: ics
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', BookJs.reveal);
} else {
    BookJs.reveal();
}
