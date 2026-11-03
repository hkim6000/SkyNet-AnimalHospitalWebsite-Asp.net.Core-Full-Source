var ServiceJs = (function () {

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
        $ApiRequest('Service/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('se-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('se-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('se-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('se-nav');
        return d && d.classList.contains('se-open');
    }

    function burger() {
        return document.querySelector('#se-site .se-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('se-nav').classList.add('se-open');
        el('se-scrim').classList.add('se-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('se-nav').querySelector('a, button');
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
        el('se-nav').classList.remove('se-open');
        el('se-scrim').classList.remove('se-open');
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
        var items = el('se-nav').querySelectorAll('a, button');
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
        el('se-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('se-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#se-site')) {
            t.classList.add('se-gone');
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
        toggleTheme: toggleTheme
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ServiceJs.reveal);
} else {
    ServiceJs.reveal();
}
