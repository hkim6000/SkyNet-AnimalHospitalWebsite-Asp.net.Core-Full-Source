var VetJs = (function () {

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
        $ApiRequest('Vet/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('pr-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pr-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pr-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('pr-nav');
        return d && d.classList.contains('pr-open');
    }

    function burger() {
        return document.querySelector('#pr-site .pr-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('pr-nav').classList.add('pr-open');
        el('pr-scrim').classList.add('pr-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('pr-nav').querySelector('a, button');
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
        el('pr-nav').classList.remove('pr-open');
        el('pr-scrim').classList.remove('pr-open');
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
        var items = el('pr-nav').querySelectorAll('a, button');
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
        el('pr-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('pr-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#pr-site')) {
            t.classList.add('pr-gone');
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
        var imgs = document.querySelectorAll('#pr-site img');
        for (var i = 0; i < imgs.length; i++) {
            if (imgs[i].complete && imgs[i].naturalWidth === 0 && imgs[i].getAttribute('src')) {
                imgs[i].classList.add('pr-gone');
            }
        }
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
    document.addEventListener('DOMContentLoaded', VetJs.reveal);
} else {
    VetJs.reveal();
}
