var BoardingJs = (function () {

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
        $ApiRequest('Boarding/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('bo-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bo-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bo-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('bo-nav');
        return d && d.classList.contains('bo-open');
    }

    function burger() {
        return document.querySelector('#bo-site .bo-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('bo-nav').classList.add('bo-open');
        el('bo-scrim').classList.add('bo-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('bo-nav').querySelector('a, button');
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
        el('bo-nav').classList.remove('bo-open');
        el('bo-scrim').classList.remove('bo-open');
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
        var items = el('bo-nav').querySelectorAll('a, button');
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
        el('bo-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('bo-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#bo-site')) {
            t.classList.add('bo-gone');
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
    document.addEventListener('DOMContentLoaded', BoardingJs.reveal);
} else {
    BoardingJs.reveal();
}
