var VetsJs = (function () {

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
        $ApiRequest('Vets/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('pv-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pv-open');
        }, 3200);
    }

    function navOpen() {
        var d = el('pv-nav');
        return d && d.classList.contains('pv-open');
    }

    function burger() {
        return document.querySelector('#pv-site .pv-burger');
    }

    function openNav() {
        lastFocus = document.activeElement;
        el('pv-nav').classList.add('pv-open');
        el('pv-scrim').classList.add('pv-open');
        if (burger()) {
            burger().setAttribute('aria-expanded', 'true');
        }
        var first = el('pv-nav').querySelector('a, button');
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
        el('pv-nav').classList.remove('pv-open');
        el('pv-scrim').classList.remove('pv-open');
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
        var items = el('pv-nav').querySelectorAll('a, button');
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
        el('pv-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function storedTheme() {
        try {
            return localStorage.getItem('cl-theme') || '';
        } catch (e) {
            return '';
        }
    }

    function toggleTheme() {
        var next = el('pv-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function broken(e) {
        var t = e.target;
        if (t && t.tagName === 'IMG' && t.closest && t.closest('#pv-site')) {
            t.classList.add('pv-gone');
        }
    }

    var fTeam = '';
    var fAnimal = '';

    function chipsOn(id, k) {
        var box = el(id);
        if (!box) {
            return;
        }
        var list = box.querySelectorAll('.pv-chip');
        for (var i = 0; i < list.length; i++) {
            var on = list[i].getAttribute('data-k') === k;
            list[i].classList.toggle('pv-act', on);
            list[i].setAttribute('aria-pressed', on ? 'true' : 'false');
        }
    }

    function apply(push) {
        var grid = el('pv-team');
        if (!grid) {
            return;
        }
        var cards = grid.querySelectorAll('.pv-pcard');
        var n = 0;
        for (var i = 0; i < cards.length; i++) {
            var c = cards[i];
            var teams = c.getAttribute('data-teams') || '';
            var animals = c.getAttribute('data-animals') || '';
            var ok = (!fTeam || teams.indexOf(' ' + fTeam + ' ') >= 0) && (!fAnimal || animals === '*' || animals.indexOf(' ' + fAnimal + ' ') >= 0);
            c.hidden = !ok;
            if (ok) {
                n++;
            }
        }
        chipsOn('pv-teams', fTeam);
        chipsOn('pv-animals', fAnimal);
        el('pv-empty').hidden = n > 0;
        el('pv-count').textContent = n === cards.length ? 'Showing all ' + n + ' people' : 'Showing ' + n + ' of ' + cards.length + ' people';
        if (push) {
            var q = [];
            if (fTeam) {
                q.push('s=' + encodeURIComponent(fTeam));
            }
            if (fAnimal) {
                q.push('a=' + encodeURIComponent(fAnimal));
            }
            try {
                history.replaceState(null, '', 'Vets' + (q.length ? '?' + q.join('&') : ''));
            } catch (e) {
            }
        }
    }

    function team(k) {
        fTeam = k || '';
        apply(true);
    }

    function animal(k) {
        fAnimal = k || '';
        apply(true);
    }

    function reset() {
        fTeam = '';
        fAnimal = '';
        apply(true);
    }

    function pickedFrom(id) {
        var on = el(id) ? el(id).querySelector('.pv-chip.pv-act') : null;
        return on ? on.getAttribute('data-k') || '' : '';
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
        var imgs = document.querySelectorAll('#pv-site img');
        for (var i = 0; i < imgs.length; i++) {
            if (imgs[i].complete && imgs[i].naturalWidth === 0 && imgs[i].getAttribute('src')) {
                imgs[i].classList.add('pv-gone');
            }
        }
        fTeam = pickedFrom('pv-teams');
        fAnimal = pickedFrom('pv-animals');
        apply(false);
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
        reset: reset
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', VetsJs.reveal);
} else {
    VetsJs.reveal();
}
