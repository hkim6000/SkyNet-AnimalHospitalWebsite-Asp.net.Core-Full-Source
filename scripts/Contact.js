var ContactJs = (function () {

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
        $ApiRequest('Contact/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ct-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ct-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ct-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('ct-nav');
        return d && d.classList.contains('ct-open');
    }

    function burger() {
        return document.querySelector('#ct-site .ct-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('ct-nav').classList.add('ct-open');
        el('ct-scrim').classList.add('ct-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('ct-nav').querySelector('a, button');
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
        el('ct-nav').classList.remove('ct-open');
        el('ct-scrim').classList.remove('ct-open');
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
        var items = el('ct-nav').querySelectorAll('a, button');
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
        el('ct-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('ct-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#ct-site')) {
            t.classList.add('ct-gone');
        }
    }

    var popBack = null;
    var msgId = '';

    function newId() {
        var s = '';
        var a = new Uint8Array(16);
        try {
            crypto.getRandomValues(a);
        } catch (e) {
            for (var j = 0; j < 16; j++) {
                a[j] = Math.floor(Math.random() * 256);
            }
        }
        for (var i = 0; i < a.length; i++) {
            s += ('0' + a[i].toString(16)).slice(-2);
        }
        return s;
    }

    function send() {
        if (!msgId) {
            msgId = newId();
        }
        var list = [{ key: 'msgid', vlu: msgId }, { key: 'web', vlu: val('ct-web') }];
        ['name', 'email', 'phone', 'topic', 'text'].forEach(function (k) {
            list.push({ key: k, vlu: val('ct-' + k) });
        });
        var b = el('ct-send');
        if (b) {
            b.disabled = true;
            setTimeout(function () {
                b.disabled = false;
            }, 1500);
        }
        call('Send', list);
        return false;
    }

    function invalid() {
        var b = el('ct-send');
        if (b) {
            b.disabled = false;
        }
        var list = ['name', 'email', 'phone', 'topic', 'text'];
        for (var i = 0; i < list.length; i++) {
            var e = el('ct-e-' + list[i]);
            if (e && e.textContent) {
                el('ct-' + list[i]).focus();
                return;
            }
        }
    }

    function sent(first, topic) {
        ['name', 'email', 'phone', 'topic', 'text'].forEach(function (k) {
            if (el('ct-' + k)) {
                el('ct-' + k).value = '';
            }
        });
        msgId = '';
        if (el('ct-send')) {
            el('ct-send').disabled = false;
        }
        el('ct-pop-t').textContent = 'Thanks, ' + first + '!';
        el('ct-pop-p').textContent = 'Your message about ' + topic + ' is with the front desk. We reply within one working day. If your pet is unwell, please call us instead.';
        popBack = document.activeElement;
        el('ct-pop').hidden = false;
        el('ct-pop-ok').focus();
    }

    function closePop() {
        el('ct-pop').hidden = true;
        if (popBack && popBack.focus && document.body.contains(popBack)) {
            popBack.focus();
        }
        popBack = null;
    }

    function clearErr(e) {
        var id = e.target && e.target.id ? e.target.id.replace('ct-', 'ct-e-') : '';
        if (id && id !== 'ct-e-web' && el(id)) {
            el(id).textContent = '';
        }
        if (e.target && e.target.id && e.target.id !== 'ct-web') {
            msgId = '';
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
        document.addEventListener('input', clearErr);
        document.addEventListener('change', clearErr);
        if (el('ct-pop')) {
            el('ct-pop').addEventListener('mousedown', function (e) {
                if (e.target === el('ct-pop')) {
                    closePop();
                }
            });
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && el('ct-pop') && !el('ct-pop').hidden) {
                closePop();
            }
            if (e.key === 'Tab' && el('ct-pop') && !el('ct-pop').hidden) {
                e.preventDefault();
                el('ct-pop-ok').focus();
            }
        });
    }

    document.addEventListener('error', broken, true);

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        send: send,
        invalid: invalid,
        sent: sent,
        closePop: closePop
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ContactJs.reveal);
} else {
    ContactJs.reveal();
}
