var AdminLoginJs = (function () {

    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function call(method, list) {
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list || []));
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function signIn() {
        call('SignIn', [
            { key: 'user', vlu: (el('lg-user').value || '').trim() },
            { key: 'pass', vlu: el('lg-pass').value }
        ]);
        return false;
    }

    function as(login) {
        el('lg-user').value = login;
        el('lg-pass').value = 'demo';
        signIn();
    }

    function mustChange() {
        el('lg-form').hidden = true;
        el('lg-first').hidden = false;
        el('lg-new').focus();
    }

    function first() {
        call('SetFirst', [
            { key: 'user', vlu: (el('lg-user').value || '').trim() },
            { key: 'pass', vlu: el('lg-pass').value },
            { key: 'next', vlu: el('lg-new').value },
            { key: 'again', vlu: el('lg-again').value }
        ]);
        return false;
    }

    function navClear() {
        try {
            sessionStorage.removeItem('ah_menu');
            sessionStorage.removeItem('ah_who');
            sessionStorage.removeItem('ah_navy');
        } catch (e) {
            return;
        }
    }

    function signedIn(tok, secs) {
        navClear();
        setCookie('ah_tok', tok, secs);
        location.href = 'Admin';
    }

    function reveal() {
        document.addEventListener('input', function () {
            el('lg-e-pass').textContent = '';
        });
    }

    return {
        reveal: reveal,
        signIn: signIn,
        as: as,
        signedIn: signedIn,
        mustChange: mustChange,
        first: first
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
