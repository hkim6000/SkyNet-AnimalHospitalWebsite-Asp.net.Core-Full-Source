var ServicesJs = (function () {

    var ttimer = null;
    var lastFocus = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Services/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('sv-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('sv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sv-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('sv-nav');
        return d && d.classList.contains('sv-open');
    }

    function burger() {
        return document.querySelector('#sv-site .sv-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('sv-nav').classList.add('sv-open');
        el('sv-scrim').classList.add('sv-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('sv-nav').querySelector('a, button');
        if (first) {
            setTimeout(function () {
                first.focus();
            }, 30);
        }
    }

    function closeNav() {
        if (!navOpen()) {
            return;
        }
        el('sv-nav').classList.remove('sv-open');
        el('sv-scrim').classList.remove('sv-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'false');
        }
        if (lastFocus && lastFocus.focus) {
            lastFocus.focus();
        }
    }

    function trap(e) {
        if (e.key !== 'Tab' || !navOpen()) {
            return;
        }
        var items = el('sv-nav').querySelectorAll('a, button');
        if (!items.length) {
            return;
        }
        var first = items[0];
        var last = items[items.length - 1];
        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    }

    function applyTheme(theme) {
        el('sv-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('sv-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#sv-site')) {
            t.classList.add('sv-gone');
        }
    }

    function state() {
        var f = el('sv-filters');
        return { team: f ? f.getAttribute('data-team') || '' : '', sp: f ? f.getAttribute('data-sp') || '' : '' };
    }

    function load(team, sp) {
        var l = el('sv-list');
        if (l) {
            l.classList.add('sv-busy');
        }
        call('Filter', [{ key: 'team', vlu: team }, { key: 'animal', vlu: sp }]);
    }

    function team(k) {
        load(k, state().sp);
    }

    function animal() {
        load(state().team, val('sv-animal'));
    }

    function reset() {
        if (el('sv-animal')) {
            el('sv-animal').value = '';
        }
        load('', '');
    }

    function filtered(team, sp) {
        var f = el('sv-filters');
        if (f) {
            f.setAttribute('data-team', team);
            f.setAttribute('data-sp', sp);
        }
        if (el('sv-animal')) {
            el('sv-animal').value = sp;
        }
        if (el('sv-list')) {
            el('sv-list').classList.remove('sv-busy');
        }
        var q = [];
        if (team) {
            q.push('s=' + encodeURIComponent(team));
        }
        if (sp) {
            q.push('sp=' + encodeURIComponent(sp));
        }
        try {
            history.replaceState(null, '', 'Services' + (q.length ? '?' + q.join('&') : ''));
        } catch (e) {
        }
    }

    function reveal() {
        var stored = storedTheme();
        if (stored) {
            applyTheme(stored);
        }
        if (burger()) {
            burger().setAttribute('aria-expanded', 'false');
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
            trap(e);
        });

    }

    document.addEventListener('error', broken, true);

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        team: team,
        animal: animal,
        reset: reset,
        filtered: filtered
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ServicesJs.reveal);
} else {
    ServicesJs.reveal();
}
