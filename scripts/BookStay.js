var BookStayJs = (function () {

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
        $ApiRequest('BookStay/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('bs-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bs-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bs-open');
        }, 3200);
    }

    function openNav() {
        el('bs-nav').classList.add('bs-open');
        el('bs-scrim').classList.add('bs-open');
    }

    function closeNav() {
        el('bs-nav').classList.remove('bs-open');
        el('bs-scrim').classList.remove('bs-open');
    }

    function applyTheme(theme) {
        el('bs-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('bs-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    var KEYS = { from: 'kfrom', 'in': 'kin', to: 'kto', dd: 'kdd', sp: 'ksp', size: 'ksz', run: 'krun', adds: 'kadds', xd: 'kxd' };
    var kept = {};

    function st() {
        var s = el('bs-st');
        var o = {};
        Object.keys(KEYS).forEach(function (k) {
            o[k] = s ? (s.getAttribute('data-' + k) || '') : '';
        });
        return o;
    }

    function keep() {
        var f = el('bs-dform');
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
        var f = el('bs-dform');
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

    function dates() {
        var o = st();
        o.from = val('bs-from');
        o['in'] = val('bs-in');
        o.to = val('bs-to');
        o.dd = '1';
        send(o);
    }

    function pickSpecies(k) {
        var o = st();
        if (o.sp !== k) {
            o.size = '';
            o.adds = '';
            o.xd = '';
        }
        o.sp = k;
        send(o);
    }

    function pickSize(k) {
        var o = st();
        o.size = k;
        o.xd = '';
        send(o);
    }

    function pickRun(k) {
        var o = st();
        o.run = k;
        o.xd = '';
        send(o);
    }

    function checked() {
        var list = document.querySelectorAll('.bs-addck:checked');
        var out = [];
        for (var i = 0; i < list.length; i++) {
            out.push(list[i].value);
        }
        return out.join(',');
    }

    function addon() {
        var o = st();
        o.adds = checked();
        var s = el('bs-st');
        if (s) {
            s.setAttribute('data-adds', o.adds);
        }
        call('Total', base(o));
    }

    function addsDone() {
        var o = st();
        o.adds = checked();
        o.xd = '1';
        send(o);
    }

    function go(step) {
        var o = st();
        if (step === 'dates') {
            o.dd = '';
            o.xd = '';
        } else if (step === 'pet') {
            o.sp = '';
            o.size = '';
            o.run = '';
            o.adds = '';
            o.xd = '';
        } else if (step === 'run') {
            o.run = '';
            o.xd = '';
        } else if (step === 'adds') {
            o.xd = '';
        }
        send(o);
    }

    function url() {
        var o = st();
        try {
            history.replaceState(null, '', 'BookStay' + (o.run ? '?run=' + encodeURIComponent(o.run) : ''));
        } catch (e) {
        }
    }

    function after() {
        url();
        restore();
        var cur = el('bs-cur');
        if (cur) {
            var r = cur.getBoundingClientRect();
            if (r.top < 70 || r.top > window.innerHeight * 0.6) {
                cur.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            var f = el('bs-first');
            if (f && !f.value) {
                setTimeout(function () {
                    f.focus({ preventScroll: true });
                }, 350);
            }
        }
    }

    function notice(msg) {
        var cur = el('bs-cur');
        if (!cur) {
            toast(msg);
            return;
        }
        var p = document.createElement('p');
        p.className = 'bs-warn-box';
        p.setAttribute('role', 'alert');
        p.textContent = msg;
        var h = cur.querySelector('h2');
        h.parentNode.insertBefore(p, h.nextSibling);
    }

    function radio(name) {
        var r = document.querySelector('input[name="' + name + '"]:checked');
        return r ? r.value : '';
    }

    function unlock() {
        busy = false;
        var b = el('bs-cbtn');
        if (b) {
            b.disabled = false;
        }
    }

    function confirm() {
        if (busy) {
            return false;
        }
        busy = true;
        var b = el('bs-cbtn');
        if (b) {
            b.disabled = true;
        }
        var o = st();
        var f = el('bs-dform');
        var list = base(o);
        list.push({ key: 'newp', vlu: radio('bs-new') });
        ['first', 'last', 'phone', 'email', 'contact', 'pname', 'breed', 'agey', 'agem', 'weight', 'feeding', 'note'].forEach(function (k) {
            list.push({ key: k, vlu: val('bs-' + k) });
        });
        list.push({ key: 'sex', vlu: radio('bs-sex') });
        list.push({ key: 'fixed', vlu: el('bs-fixed').checked ? '1' : '' });
        list.push({ key: 'vaxok', vlu: el('bs-vaxok') && el('bs-vaxok').checked ? '1' : '' });
        list.push({ key: 'agree', vlu: el('bs-agree').checked ? '1' : '' });
        list.push({ key: 'ftok', vlu: f.getAttribute('data-ft') || '' });
        list.push({ key: 'hp', vlu: val('bs-hp') });
        var errs = f.querySelectorAll('.bs-err');
        for (var j = 0; j < errs.length; j++) {
            errs[j].innerHTML = '';
        }
        keep();
        call('Confirm', list);
        return false;
    }

    function form(tok) {
        var f = el('bs-dform');
        if (f) {
            f.setAttribute('data-ft', tok);
        }
    }

    function invalid() {
        var e = document.querySelector('#bs-dform .bs-err:not(:empty)');
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
            history.replaceState(null, '', 'BookStay');
        } catch (e) {
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
        var b = el('bs-booked');
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
            if (t && t.name === 'bs-sex') {
                el('bs-e-sex').textContent = '';
            }
            if (t && (t.id === 'bs-agree' || t.id === 'bs-vaxok')) {
                el(t.id.replace('bs-', 'bs-e-')).textContent = '';
            }
            if (t && (t.id === 'bs-from' || t.id === 'bs-to' || t.id === 'bs-in') && el('bs-e-dates')) {
                el('bs-e-dates').textContent = '';
            }
        });
        url();
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
            var id = e.target && e.target.id ? e.target.id.replace('bs-', 'bs-e-') : '';
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
        dates: dates,
        pickSpecies: pickSpecies,
        pickSize: pickSize,
        pickRun: pickRun,
        addon: addon,
        addsDone: addsDone,
        go: go,
        after: after,
        notice: notice,
        confirm: confirm,
        unlock: unlock,
        invalid: invalid,
        form: form,
        booked: booked,
        ics: ics
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', BookStayJs.reveal);
} else {
    BookStayJs.reveal();
}
