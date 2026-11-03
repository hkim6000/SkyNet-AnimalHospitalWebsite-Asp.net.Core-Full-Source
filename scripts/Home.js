var HomeJs = (function () {

    var ttimer = null;
    var LIST = [];
    var lastFocus = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Home/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('hm-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('hm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('hm-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('hm-nav');
        return d && d.classList.contains('hm-open');
    }

    function burger() {
        return document.querySelector('#hm-site .hm-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('hm-nav').classList.add('hm-open');
        el('hm-scrim').classList.add('hm-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('hm-nav').querySelector('a, button');
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
        el('hm-nav').classList.remove('hm-open');
        el('hm-scrim').classList.remove('hm-open');
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
        var items = el('hm-nav').querySelectorAll('a, button');
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
        el('hm-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('hm-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function finder() {
        var s = el('hm-fsv');
        LIST = [];
        if (!s) {
            return;
        }
        try {
            LIST = JSON.parse(s.getAttribute('data-list') || '[]');
        } catch (e) {
            LIST = [];
        }
    }

    function species() {
        var s = el('hm-fsv');
        if (!s) {
            return;
        }
        var sp = val('hm-fsp');
        var cur = s.value;
        var keep = false;
        s.innerHTML = '';
        var group = null;
        var name = '';
        LIST.forEach(function (x) {
            if (sp && x.s && x.s.length && x.s.indexOf(sp) < 0) {
                return;
            }
            if (!group || name !== x.t) {
                group = document.createElement('optgroup');
                group.label = x.t;
                name = x.t;
                s.appendChild(group);
            }
            var o = document.createElement('option');
            o.value = x.k;
            o.textContent = x.n;
            if (x.k === cur) {
                o.selected = true;
                keep = true;
            }
            group.appendChild(o);
        });
        if (!keep && s.options.length) {
            s.selectedIndex = 0;
        }
        pick();
    }

    function pick() {
        var n = el('hm-fnext');
        if (!n) {
            return;
        }
        n.classList.add('hm-busy');
        call('Next', [{ key: 'svc', vlu: val('hm-fsv') }, { key: 'spc', vlu: val('hm-fsp') }]);
        setTimeout(function () {
            n.classList.remove('hm-busy');
        }, 400);
    }

    function find() {
        var sv = val('hm-fsv');
        var sp = val('hm-fsp');
        var q = [];
        if (sv) {
            q.push('s=' + encodeURIComponent(sv));
        }
        if (sp) {
            q.push('sp=' + encodeURIComponent(sp));
        }
        location.href = 'Book' + (q.length ? '?' + q.join('&') : '');
        return false;
    }

    function reveal() {
        var stored = storedTheme();
        if (stored) {
            applyTheme(stored);
        }
        if (burger()) {
            burger().setAttribute('aria-expanded', 'false');
        }
        finder();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
            trap(e);
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        species: species,
        pick: pick,
        find: find
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', HomeJs.reveal);
} else {
    HomeJs.reveal();
}
