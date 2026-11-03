var MyAppointmentJs = (function () {

    var ttimer = null;
    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('MyAppointment/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ma-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ma-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ma-open');
        }, 3200);
    }

    function openNav() {
        el('ma-nav').classList.add('ma-open');
        el('ma-scrim').classList.add('ma-open');
    }

    function closeNav() {
        el('ma-nav').classList.remove('ma-open');
        el('ma-scrim').classList.remove('ma-open');
    }

    function applyTheme(theme) {
        el('ma-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('ma-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ma-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ma-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ma-ask';
        askBox.innerHTML = '<div class="ma-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ma-err" role="alert"></em><div class="ma-askacts"><button type="button" class="ma-btn ma-btn-ghost ma-btn-sm"></button><button type="button" class="ma-btn ma-btn-sm"></button></div></div>';
        (el('ma-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ma-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ma-btn ma-btn-sm' + (o.danger ? ' ma-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ma-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ma-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function keys() {
        return [{ key: 'code', vlu: val('ma-code') }, { key: 'who', vlu: val('ma-who') }];
    }

    function find() {
        el('ma-e-find').textContent = '';
        call('Find', keys());
        return false;
    }

    function found() {
        var r = el('ma-res');
        var top = r.getBoundingClientRect().top;
        if (top < 60 || top > window.innerHeight * 0.7) {
            r.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        var c = val('ma-code');
        try {
            history.replaceState(null, '', 'MyAppointment' + (c ? '?c=' + encodeURIComponent(c) : ''));
        } catch (e) {
        }
    }

    function resched() {
        call('Times', keys());
    }

    function week(d) {
        var l = keys();
        l.push({ key: 'wk', vlu: d });
        call('Times', l);
    }

    function day(d) {
        var l = keys();
        var mv = el('ma-mv');
        l.push({ key: 'wk', vlu: mv ? mv.getAttribute('data-wk') : '' });
        l.push({ key: 'day', vlu: d });
        call('Times', l);
    }

    function pickTime(t) {
        move(t, 0);
    }

    function move(t, go) {
        var l = keys();
        l.push({ key: 't', vlu: t });
        l.push({ key: 'go', vlu: go ? '1' : '' });
        call('Move', l);
    }

    function moving() {
        var m = el('ma-move');
        if (m) {
            m.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    function closeMove() {
        el('ma-move').innerHTML = '';
    }

    function cancel(drop) {
        ask({ title: 'Cancel this booking?', text: drop ? 'The procedure slot will be offered to another pet. You can book again at any time.' : 'The time will be offered to someone else. You can book again at any time.', ok: 'Cancel booking', no: 'Keep it', danger: true }, function () {
            call('Cancel', keys());
        });
    }

    function stayDates() {
        call('StayForm', keys());
    }

    function stayMove(go) {
        var l = keys();
        var mv = el('ma-mv');
        var e = el('ma-e-stay');
        if (e) {
            e.textContent = '';
        }
        if (go && mv) {
            l.push({ key: 'sfrom', vlu: mv.getAttribute('data-from') || '' });
            l.push({ key: 'sin', vlu: mv.getAttribute('data-in') || '' });
            l.push({ key: 'sto', vlu: mv.getAttribute('data-to') || '' });
        } else {
            l.push({ key: 'sfrom', vlu: val('ma-sfrom') });
            l.push({ key: 'sin', vlu: val('ma-sin') });
            l.push({ key: 'sto', vlu: val('ma-sto') });
        }
        l.push({ key: 'go', vlu: go ? '1' : '' });
        call('StayMove', l);
    }

    function cancelStay() {
        ask({ title: 'Cancel this stay?', text: 'The room will be offered to another pet. You can book again at any time.', ok: 'Cancel stay', no: 'Keep it', danger: true }, function () {
            call('CancelStay', keys());
        });
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
        if (val('ma-code')) {
            el('ma-who').focus();
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
            var id = e.target && e.target.id ? e.target.id.replace('ma-', 'ma-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
            if (e.target && (e.target.id === 'ma-code' || e.target.id === 'ma-who')) {
                el('ma-e-find').textContent = '';
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
        find: find,
        found: found,
        resched: resched,
        week: week,
        day: day,
        pickTime: pickTime,
        move: move,
        moving: moving,
        closeMove: closeMove,
        cancel: cancel,
        stayDates: stayDates,
        stayMove: stayMove,
        cancelStay: cancelStay,
        ics: ics
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', MyAppointmentJs.reveal);
} else {
    MyAppointmentJs.reveal();
}
