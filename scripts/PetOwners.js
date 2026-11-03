var PetOwnersJs = (function () {

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
        $ApiRequest('PetOwners/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('pi-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pi-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pi-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('pi-nav');
        return d && d.classList.contains('pi-open');
    }

    function burger() {
        return document.querySelector('#pi-site .pi-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('pi-nav').classList.add('pi-open');
        el('pi-scrim').classList.add('pi-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('pi-nav').querySelector('a, button');
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
        el('pi-nav').classList.remove('pi-open');
        el('pi-scrim').classList.remove('pi-open');
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
        var items = el('pi-nav').querySelectorAll('a, button');
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
        el('pi-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('pi-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#pi-site')) {
            t.classList.add('pi-gone');
        }
    }

    function tab(k) {
        var tabs = document.querySelectorAll('.pi-tab');
        for (var i = 0; i < tabs.length; i++) {
            var on = tabs[i].getAttribute('data-k') === k;
            tabs[i].classList.toggle('pi-act', on);
            tabs[i].setAttribute('aria-selected', on ? 'true' : 'false');
            tabs[i].tabIndex = on ? 0 : -1;
            var p = el('pi-panel-' + tabs[i].getAttribute('data-k'));
            if (p) {
                p.hidden = !on;
            }
            if (on) {
                tabs[i].focus({ preventScroll: true });
            }
        }
    }

    function arrows(e) {
        var t = e.target;
        if (!t || !t.classList || !t.classList.contains('pi-tab') || (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft')) {
            return;
        }
        var tabs = Array.prototype.slice.call(document.querySelectorAll('.pi-tab'));
        var at = tabs.indexOf(t);
        var next = tabs[(at + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
        e.preventDefault();
        tab(next.getAttribute('data-k'));
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
        document.addEventListener('keydown', arrows);
    }

    document.addEventListener('error', broken, true);

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        tab: tab
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', PetOwnersJs.reveal);
} else {
    PetOwnersJs.reveal();
}
