var MediaJs = (function () {

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
        $ApiRequest('Media/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('mc-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('mc-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('mc-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('mc-nav');
        return d && d.classList.contains('mc-open');
    }

    function burger() {
        return document.querySelector('#mc-site .mc-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('mc-nav').classList.add('mc-open');
        el('mc-scrim').classList.add('mc-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('mc-nav').querySelector('a, button');
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
        el('mc-nav').classList.remove('mc-open');
        el('mc-scrim').classList.remove('mc-open');
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
        var items = el('mc-nav').querySelectorAll('a, button');
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
        el('mc-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('mc-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#mc-site')) {
            t.classList.add('mc-gone');
        }
    }

    var lbItems = [];
    var lbAt = 0;
    var lbBack = null;
    var tx = 0;
    var ty = 0;

    function lbShow() {
        var it = lbItems[lbAt];
        var img = el('mc-lbimg');
        img.classList.remove('mc-gone');
        img.src = it.src;
        img.alt = it.cap || '';
        el('mc-lbcap').textContent = it.cap || '';
        el('mc-lbnum').textContent = (lbAt + 1) + ' of ' + lbItems.length;
        var nx = lbItems[(lbAt + 1) % lbItems.length];
        if (nx && nx.src !== it.src) {
            var pre = new Image();
            pre.src = nx.src;
        }
    }

    function lbOpen(btn) {
        try {
            lbItems = JSON.parse(btn.getAttribute('data-items')) || [];
        } catch (e) {
            lbItems = [];
        }
        if (!lbItems.length) {
            return;
        }
        lbBack = btn;
        lbAt = 0;
        el('mc-lbtitle').textContent = btn.getAttribute('data-title') || '';
        lbShow();
        el('mc-lb').hidden = false;
        document.body.style.overflow = 'hidden';
        el('mc-lb').querySelector('.mc-lbx').focus();
    }

    function lbGo(d) {
        if (!lbItems.length) {
            return;
        }
        lbAt = (lbAt + d + lbItems.length) % lbItems.length;
        lbShow();
    }

    function lbClose() {
        el('mc-lb').hidden = true;
        document.body.style.overflow = '';
        if (lbBack && document.body.contains(lbBack)) {
            lbBack.focus();
        }
    }

    function start() {
        var lb = el('mc-lb');
        document.addEventListener('keydown', function (e) {
            if (lb.hidden) {
                return;
            }
            if (e.key === 'Escape') {
                e.stopPropagation();
                lbClose();
            } else if (e.key === 'ArrowLeft') {
                lbGo(-1);
            } else if (e.key === 'ArrowRight') {
                lbGo(1);
            } else if (e.key === 'Tab') {
                var f = Array.prototype.slice.call(lb.querySelectorAll('button'));
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1) + f.length) % f.length].focus();
            }
        });
        lb.addEventListener('click', function (e) {
            if (e.target === lb || e.target === el('mc-lbfig')) {
                lbClose();
            }
        });
        lb.addEventListener('touchstart', function (e) {
            if (e.touches.length === 1) {
                tx = e.touches[0].clientX;
                ty = e.touches[0].clientY;
            }
        }, { passive: true });
        lb.addEventListener('touchend', function (e) {
            if (!e.changedTouches.length) {
                return;
            }
            var dx = e.changedTouches[0].clientX - tx;
            var dy = e.changedTouches[0].clientY - ty;
            if (Math.abs(dx) > 45 && Math.abs(dx) > Math.abs(dy)) {
                lbGo(dx < 0 ? 1 : -1);
            } else if (dy > 90 && Math.abs(dy) > Math.abs(dx)) {
                lbClose();
            }
        }, { passive: true });
        document.addEventListener('play', function (e) {
            var vids = document.querySelectorAll('#mc-main video');
            for (var i = 0; i < vids.length; i++) {
                if (vids[i] !== e.target && !vids[i].paused && !vids[i].muted) {
                    vids[i].pause();
                }
            }
        }, true);
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
        start();
    }

    document.addEventListener('error', broken, true);

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        lbOpen: lbOpen,
        lbGo: lbGo,
        lbClose: lbClose
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', MediaJs.reveal);
} else {
    MediaJs.reveal();
}
