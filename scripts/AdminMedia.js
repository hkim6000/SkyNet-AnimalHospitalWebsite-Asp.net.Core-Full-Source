var AdminMediaJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminMedia/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('md-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('md-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('md-open');
        }, 3200);
    }

    function openNav() {
        el('md-nav').classList.add('md-open');
        el('md-scrim').classList.add('md-open');
    }

    function closeNav() {
        el('md-nav').classList.remove('md-open');
        el('md-scrim').classList.remove('md-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]:not([data-x])');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            t.setAttribute('data-x', '1');
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var otimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('ah_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('ah_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        navClear();
        setCookie('ah_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('md-modal').classList.add('md-open');
        times(el('md-mbody'));
        var f = el('md-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('md-modal');
        if (m) {
            m.classList.remove('md-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function loaded() {
        times(el('md-main'));
        if (window.MutationObserver && el('md-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('md-main'));
                }, 30);
            }).observe(el('md-main'), { childList: true, subtree: true });
        }
        start();
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('md-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('md-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'md-ask';
        askBox.innerHTML = '<div class="md-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="md-err" role="alert"></em><div class="md-askacts"><button type="button" class="md-btn md-btn-ghost md-btn-sm"></button><button type="button" class="md-btn md-btn-sm"></button></div></div>';
        (el('md-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('md-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'md-btn md-btn-sm' + (o.danger ? ' md-askdel' : ' md-btn-accent');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('md-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('md-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    var kind = '';
    var page = 1;
    var current = '';
    var CHUNK = 2 * 1024 * 1024;
    var stimer = null;
    var busy = false;
    var ctx = '';
    var photoData = null;
    var vfile = null;
    var dfile = null;
    var dcover = '';
    var up = null;

    function start() {
        list(1);
        if (qs('id')) {
            view(qs('id'));
        }
    }

    function filters() {
        return [{ key: 'mkind', vlu: kind }, { key: 'mq', vlu: val('md-q') }, { key: 'mtag', vlu: val('md-mtag') }, { key: 'muse', vlu: val('md-muse') }];
    }

    function list(pg) {
        page = pg || 1;
        api('List', filters().concat([{ key: 'mpg', vlu: String(page) }]));
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function setKind(k) {
        kind = k;
        list(1);
    }

    function mb(n) {
        return (n / 1048576).toFixed(n < 10485760 ? 1 : 0) + ' MB';
    }

    function nameOf(f) {
        return f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim().substring(0, 80);
    }

    function setBox(boxId, inputId, html) {
        var inp = el(inputId);
        var box = el(boxId);
        if (!box || !inp) {
            return;
        }
        box.innerHTML = html;
        box.appendChild(inp);
    }

    function canvas(w, h) {
        var c = document.createElement('canvas');
        c.width = Math.max(1, Math.round(w));
        c.height = Math.max(1, Math.round(h));
        return c;
    }

    function enc(c, q) {
        var d = c.toDataURL('image/webp', q);
        return d.indexOf('data:image/webp') === 0 ? d : c.toDataURL('image/jpeg', q);
    }

    function under(c, limit) {
        var qs2 = [0.86, 0.76, 0.64, 0.5, 0.38];
        for (var i = 0; i < qs2.length; i++) {
            var d = enc(c, qs2[i]);
            if (d.length <= limit) {
                return d;
            }
        }
        return '';
    }

    function shrink(src, w, h, max) {
        var r = Math.min(1, max / Math.max(w, h));
        var c = canvas(w * r, h * r);
        c.getContext('2d').drawImage(src, 0, 0, c.width, c.height);
        return c;
    }

    function bitmap(f, done, bad) {
        if (!window.createImageBitmap) {
            bad();
            return;
        }
        createImageBitmap(f).then(done, bad);
    }

    function photo(bm) {
        var full = under(shrink(bm, bm.width, bm.height, 1600), 3000000);
        var thumb = under(shrink(bm, bm.width, bm.height, 640), 700000);
        if (full && thumb && full.substring(0, 15) !== thumb.substring(0, 15)) {
            thumb = '';
        }
        return { full: full, thumb: thumb, w: bm.width, h: bm.height };
    }

    function wrap(g, text, maxW) {
        var words = String(text || '').split(/\s+/);
        var lines = [];
        var line = '';
        words.forEach(function (w) {
            var t = line ? line + ' ' + w : w;
            if (g.measureText(t).width > maxW && line) {
                lines.push(line);
                line = w;
            } else {
                line = t;
            }
        });
        if (line) {
            lines.push(line);
        }
        return lines.slice(0, 4);
    }

    function card(w, h, title, label) {
        var c = canvas(w, h);
        var g = c.getContext('2d');
        var s = getComputedStyle(el('md-site'));
        var acc = (s.getPropertyValue('--md-accent') || '#3F7D6B').trim();
        g.fillStyle = acc;
        g.fillRect(0, 0, w, h);
        g.fillStyle = 'rgba(255,255,255,0.10)';
        g.beginPath();
        g.arc(w * 0.88, h * 0.16, Math.min(w, h) * 0.34, 0, Math.PI * 2);
        g.fill();
        g.beginPath();
        g.arc(w * 0.08, h * 0.96, Math.min(w, h) * 0.26, 0, Math.PI * 2);
        g.fill();
        var pad = Math.round(w * 0.08);
        g.fillStyle = '#F7F3EC';
        g.font = '700 ' + Math.round(h * 0.045) + 'px "Segoe UI", system-ui, sans-serif';
        g.fillText(String(label).toUpperCase(), pad, pad + h * 0.04);
        var size = Math.round(Math.min(w * 0.09, h * 0.1));
        g.font = '700 ' + size + 'px "Iowan Old Style", "Palatino Linotype", Georgia, serif';
        var lines = wrap(g, title, w - pad * 2);
        var y = h * 0.5 - (lines.length - 1) * size * 0.6;
        lines.forEach(function (l) {
            g.fillText(l, pad, y);
            y += size * 1.2;
        });
        g.fillStyle = 'rgba(247,243,236,0.8)';
        g.font = '600 ' + Math.round(h * 0.035) + 'px "Segoe UI", system-ui, sans-serif';
        g.fillText((el('md-bname') ? el('md-bname').textContent : ''), pad, h - pad);
        return c;
    }

    function hex() {
        var a = new Uint8Array(16);
        window.crypto.getRandomValues(a);
        var s = '';
        for (var i = 0; i < a.length; i++) {
            s += (a[i] < 16 ? '0' : '') + a[i].toString(16);
        }
        return s;
    }

    function errEl() {
        return el('md-e-' + ctx + 'file');
    }

    function lock(on) {
        busy = on;
        var b = el('md-' + ctx + 'btn');
        if (b) {
            if (b.tagName === 'LABEL') {
                b.classList.toggle('md-busy', on);
            } else {
                b.disabled = on;
            }
        }
    }

    function fail(msg) {
        if (errEl()) {
            errEl().textContent = msg;
        } else if (msg) {
            toast(msg);
        }
        lock(false);
        var p = el('md-' + ctx + 'prog');
        if (p) {
            p.classList.remove('md-on');
        }
        var bar = el('md-' + ctx + 'bar');
        if (bar) {
            bar.style.width = '0';
        }
        up = null;
    }

    function ppick(input) {
        var f = input.files && input.files[0];
        input.value = '';
        ctx = 'p';
        el('md-e-pfile').textContent = '';
        photoData = null;
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('md-e-pfile').textContent = 'Use a JPG, PNG or WebP photo under 30 MB.';
            return;
        }
        if (!val('md-ptitle')) {
            el('md-ptitle').value = nameOf(f);
        }
        var label = esc(f.name) + ' &middot; ' + mb(f.size);
        setBox('md-pprev', 'md-pfile', '<span>Reading&hellip;</span><span class="md-finfo">' + label + '</span>');
        bitmap(f, function (bm) {
            photoData = photo(bm);
            if (!photoData.full || !photoData.thumb) {
                photoData = null;
                setBox('md-pprev', 'md-pfile', '<span>Choose a photo<br>JPG, PNG or WebP</span>');
                el('md-e-pfile').textContent = 'That photo could not be made small enough. Try another one.';
                return;
            }
            setBox('md-pprev', 'md-pfile', '<img src="' + photoData.thumb + '" alt=""><span class="md-finfo">' + label + ' &middot; ' + photoData.w + '&times;' + photoData.h + '</span>');
        }, function () {
            setBox('md-pprev', 'md-pfile', '<span>Choose a photo<br>JPG, PNG or WebP</span>');
            el('md-e-pfile').textContent = 'That image could not be read.';
        });
    }

    function pupload() {
        if (busy) {
            return;
        }
        ctx = 'p';
        if (!photoData) {
            el('md-e-pfile').textContent = 'Choose a photo first.';
            return;
        }
        if (val('md-ptitle').length < 2) {
            el('md-e-pfile').textContent = 'Give the photo a name.';
            return;
        }
        lock(true);
        api('Upload', [{ key: 'full', vlu: photoData.full }, { key: 'thumb', vlu: photoData.thumb }, { key: 'mtitle', vlu: val('md-ptitle') }, { key: 'malt', vlu: val('md-palt') }, { key: 'mtags', vlu: val('md-ptags') }, { key: 'mw', vlu: String(photoData.w) }, { key: 'mh', vlu: String(photoData.h) }]);
    }

    function isMp4(f) {
        return f.type === 'video/mp4' || /\.mp4$/i.test(f.name);
    }

    function vpick(input) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        input.value = '';
        ctx = 'v';
        el('md-e-vfile').textContent = '';
        vfile = null;
        if (!f) {
            return;
        }
        if (!isMp4(f)) {
            el('md-e-vfile').textContent = 'Use an MP4 video.';
            return;
        }
        if (f.size > max) {
            el('md-e-vfile').textContent = 'The video must be ' + Math.round(max / 1048576) + ' MB or smaller. This one is ' + mb(f.size) + '.';
            return;
        }
        vfile = f;
        if (!val('md-vtitle')) {
            el('md-vtitle').value = nameOf(f);
        }
        setBox('md-vprev', 'md-vfile', '<span class="md-play md-play-s"></span><span class="md-finfo">' + esc(f.name) + ' &middot; ' + mb(f.size) + '</span>');
    }

    function vupload() {
        if (busy) {
            return;
        }
        ctx = 'v';
        if (!vfile) {
            el('md-e-vfile').textContent = 'Choose a video first.';
            return;
        }
        if (val('md-vtitle').length < 2) {
            el('md-e-vfile').textContent = 'Give the video a name.';
            return;
        }
        var go = function () {
            ctx = 'v';
            chunks({ file: vfile, kind: 'video', rid: '', title: val('md-vtitle'), tags: val('md-vtags') });
        };
        if (vfile.size > 20 * 1024 * 1024) {
            ask({ title: 'This video is ' + mb(vfile.size) + '.', text: 'Large videos load slowly for visitors on phones. A short clip under 20 MB works best. Upload it anyway?', ok: 'Upload anyway', no: 'Choose another' }, go);
            return;
        }
        go();
    }

    function dpick(input) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        input.value = '';
        ctx = 'd';
        el('md-e-dfile').textContent = '';
        dfile = null;
        if (!f) {
            return;
        }
        if (!/\.pdf$/i.test(f.name) && f.type !== 'application/pdf') {
            el('md-e-dfile').textContent = 'Use a PDF file.';
            return;
        }
        if (f.size > max) {
            el('md-e-dfile').textContent = 'The PDF must be ' + Math.round(max / 1048576) + ' MB or smaller.';
            return;
        }
        dfile = f;
        if (!val('md-dtitle')) {
            el('md-dtitle').value = nameOf(f);
        }
        setBox('md-dprev', 'md-dfile', (dcover ? '<img src="' + dcover + '" alt="">' : '<span class="md-pdfi">PDF</span>') + '<span class="md-finfo">' + esc(f.name) + ' &middot; ' + mb(f.size) + '</span>');
    }

    function dcov(input) {
        var f = input.files && input.files[0];
        input.value = '';
        ctx = 'd';
        el('md-e-dfile').textContent = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('md-e-dfile').textContent = 'Use a JPG, PNG or WebP picture for the cover.';
            return;
        }
        bitmap(f, function (bm) {
            dcover = under(shrink(bm, bm.width, bm.height, 800), 700000);
            el('md-dcovt').textContent = dcover ? 'Cover: ' + f.name : 'That picture could not be used.';
            if (dfile && dcover) {
                setBox('md-dprev', 'md-dfile', '<img src="' + dcover + '" alt=""><span class="md-finfo">' + esc(dfile.name) + ' &middot; ' + mb(dfile.size) + '</span>');
            }
        }, function () {
            el('md-e-dfile').textContent = 'That picture could not be read.';
        });
    }

    function dupload() {
        if (busy) {
            return;
        }
        ctx = 'd';
        if (!dfile) {
            el('md-e-dfile').textContent = 'Choose a PDF first.';
            return;
        }
        if (val('md-dtitle').length < 2) {
            el('md-e-dfile').textContent = 'Give the PDF a name.';
            return;
        }
        chunks({ file: dfile, kind: 'doc', rid: '', title: val('md-dtitle'), tags: val('md-dtags'), cover: dcover });
    }

    function chunks(o) {
        up = o;
        up.uid = hex();
        up.total = Math.max(1, Math.ceil(o.file.size / CHUNK));
        lock(true);
        if (errEl()) {
            errEl().textContent = '';
        }
        var p = el('md-' + ctx + 'prog');
        if (p) {
            p.classList.add('md-on');
        }
        send(0);
    }

    function send(i) {
        if (!up) {
            return;
        }
        var r = new FileReader();
        r.onload = function () {
            var d = String(r.result);
            api('Chunk', [{ key: 'uid', vlu: up.uid }, { key: 'idx', vlu: String(i) }, { key: 'total', vlu: String(up.total) }, { key: 'ukind', vlu: up.kind }, { key: 'part', vlu: d.substring(d.indexOf(',') + 1) }]);
        };
        r.onerror = function () {
            fail('That file could not be read.');
        };
        r.readAsDataURL(up.file.slice(i * CHUNK, (i + 1) * CHUNK));
    }

    function chunkOk(i) {
        if (!up) {
            return;
        }
        var bar = el('md-' + ctx + 'bar');
        if (bar) {
            bar.style.width = Math.round((i + 1) / up.total * 100) + '%';
        }
        if (i + 1 < up.total) {
            send(i + 1);
            return;
        }
        if (up.kind === 'doc') {
            api('DocDone', [{ key: 'uid', vlu: up.uid }, { key: 'rid', vlu: up.rid }, { key: 'mtitle', vlu: up.title }, { key: 'mtags', vlu: up.tags || '' }, { key: 'cover', vlu: up.cover || under(card(600, 800, up.title, 'PDF'), 700000) }]);
            return;
        }
        api('VideoDone', [{ key: 'uid', vlu: up.uid }, { key: 'rid', vlu: up.rid }, { key: 'mtitle', vlu: up.title }, { key: 'mtags', vlu: up.tags || '' }, { key: 'poster', vlu: card(960, 540, up.title, 'Video').toDataURL('image/jpeg', 0.84) }]);
    }

    function resetPanel(c) {
        var box = el('md-' + c + 'prev');
        if (box && box.closest('details')) {
            box.closest('details').open = false;
        }
        if (c === 'p') {
            photoData = null;
            setBox('md-pprev', 'md-pfile', '<span>Choose a photo<br>JPG, PNG or WebP</span>');
            ['md-ptitle', 'md-palt', 'md-ptags'].forEach(function (k) {
                el(k).value = '';
            });
        } else if (c === 'v') {
            vfile = null;
            setBox('md-vprev', 'md-vfile', '<span>Choose a video<br>MP4</span>');
            el('md-vtitle').value = '';
            el('md-vtags').value = '';
        } else if (c === 'd') {
            dfile = null;
            dcover = '';
            setBox('md-dprev', 'md-dfile', '<span>Choose a PDF</span>');
            el('md-dtitle').value = '';
            el('md-dtags').value = '';
            el('md-dcovt').textContent = 'Without one, a simple cover with the name is made for you.';
        }
    }

    function done(what, id, src, rk) {
        var c = ctx;
        fail('');
        if (what === 'replace') {
            if (rk === 'video') {
                toast('Video replaced. Making its cover picture…');
                poster(id, src, true);
            } else {
                toast('File replaced everywhere it is used.');
                view(id);
            }
            list(page);
            return;
        }
        resetPanel(c);
        kind = '';
        list(1);
        if (what === 'video') {
            toast('Video added. Making its cover picture…');
            poster(id, src, false);
        } else {
            toast(what === 'doc' ? 'PDF added to the library.' : 'Photo added to the library.');
        }
    }

    function poster(id, src, reopen) {
        var v = document.createElement('video');
        var over = false;
        var timer = null;
        var stop = function () {
            if (over) {
                return;
            }
            over = true;
            clearTimeout(timer);
            v.removeAttribute('src');
            v.load();
            if (reopen && current === id) {
                view(id);
            }
        };
        timer = setTimeout(stop, 20000);
        v.muted = true;
        v.preload = 'auto';
        v.playsInline = true;
        v.addEventListener('loadedmetadata', function () {
            v.currentTime = Math.min(1, (isFinite(v.duration) && v.duration > 0 ? v.duration : 2) / 2);
        });
        v.addEventListener('seeked', function () {
            if (over) {
                return;
            }
            try {
                var c = shrink(v, v.videoWidth, v.videoHeight, 960);
                var d = c.toDataURL('image/jpeg', 0.82);
                over = true;
                clearTimeout(timer);
                api('SetPoster', [{ key: 'mid', vlu: id }, { key: 'poster', vlu: d }, { key: 'secs', vlu: String(isFinite(v.duration) ? Math.round(v.duration) : 0) }, { key: 'mw', vlu: String(v.videoWidth) }, { key: 'mh', vlu: String(v.videoHeight) }, { key: 'again', vlu: reopen && current === id ? '1' : '' }]);
            } catch (e) {
                stop();
            }
        });
        v.addEventListener('error', stop);
        v.src = src;
    }

    function postered(id, again) {
        list(page);
        if (again) {
            view(id);
        }
        toast('Cover picture made from the video.');
    }

    function view(id) {
        current = id;
        api('Open', [{ key: 'mid', vlu: id }]);
    }

    function stopPreview() {
        var v = document.querySelector('#md-mbody video');
        if (v) {
            v.pause();
        }
    }

    function rpick(input, k) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        input.value = '';
        if (busy || !f) {
            return;
        }
        ctx = 'r';
        el('md-e-rfile').textContent = '';
        var title = val('md-etitle') || 'Media';
        if (f.size > max) {
            fail('The file must be ' + Math.round(max / 1048576) + ' MB or smaller. This one is ' + mb(f.size) + '.');
            return;
        }
        if (k === 'photo') {
            if (!/^image\/(jpeg|png|webp)$/.test(f.type)) {
                fail('Use a JPG, PNG or WebP photo.');
                return;
            }
            lock(true);
            bitmap(f, function (bm) {
                var p = photo(bm);
                if (!p.full || !p.thumb) {
                    fail('That photo could not be made small enough. Try another one.');
                    return;
                }
                api('Upload', [{ key: 'rid', vlu: current }, { key: 'full', vlu: p.full }, { key: 'thumb', vlu: p.thumb }, { key: 'mw', vlu: String(p.w) }, { key: 'mh', vlu: String(p.h) }]);
            }, function () {
                fail('That image could not be read.');
            });
            return;
        }
        if (k === 'video' && !isMp4(f)) {
            fail('Use an MP4 video.');
            return;
        }
        if (k === 'doc' && !/\.pdf$/i.test(f.name) && f.type !== 'application/pdf') {
            fail('Use a PDF file.');
            return;
        }
        chunks({ file: f, kind: k, rid: current, title: title });
    }

    function unsave() {
        if (el('md-savebtn')) {
            el('md-savebtn').disabled = false;
        }
    }

    function save() {
        if (el('md-savebtn')) {
            if (el('md-savebtn').disabled) {
                return;
            }
            el('md-savebtn').disabled = true;
        }
        if (el('md-e-etitle')) {
            el('md-e-etitle').textContent = '';
        }
        api('SaveInfo', [{ key: 'mid', vlu: current }, { key: 'mtitle', vlu: val('md-etitle') }, { key: 'malt', vlu: val('md-ealt') }, { key: 'mtags', vlu: val('md-etags') }].concat(filters(), [{ key: 'mpg', vlu: String(page) }]));
    }

    function saved() {
        stopPreview();
        closeModal();
        toast('Saved.');
    }

    function del(id, title, used) {
        if (used > 0) {
            api('Remove', [{ key: 'mid', vlu: id }]);
            return;
        }
        ask({ title: 'Delete “' + title + '”?', text: 'It is removed from the library for good.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('Remove', [{ key: 'mid', vlu: id }, { key: 'mpg', vlu: String(page) }].concat(filters()));
        });
    }

    function inUse(title, where) {
        ask({ title: '“' + title + '” is still in use', text: 'It is used on: ' + where.join(', ') + '. Choose another picture in those places first, then delete it.', ok: 'OK', no: 'Close' }, null);
    }

    function removed() {
        stopPreview();
        closeModal();
        toast('Deleted.');
    }

    function shut() {
        stopPreview();
        closeModal();
    }

    function reveal() {
        if (!getCookie('ah_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', []);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                stopPreview();
                closeModal();
            }
        });
        document.addEventListener('click', function (e) {
            if (e.target && (e.target.id === 'md-modal' || (e.target.closest && e.target.closest('.md-mclose')))) {
                stopPreview();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('md-', 'md-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    function openSecs() {
        try {
            return JSON.parse(localStorage.getItem('ah_secs') || '[]');
        } catch (e) {
            return [];
        }
    }

    function sec(btn) {
        var box = btn.parentNode;
        var open = !box.classList.contains('md-sopen');
        box.classList.toggle('md-sopen', open);
        btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        var key = box.getAttribute('data-sec');
        var list = openSecs().filter(function (k) { return k !== key; });
        if (open) {
            list.push(key);
        }
        try {
            localStorage.setItem('ah_secs', JSON.stringify(list));
        } catch (e) {
            return;
        }
    }

    function secs() {
        var list = openSecs();
        var all = document.querySelectorAll('.md-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('md-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.md-sec.md-shere');
        var k = h ? h.getAttribute('data-sec') : '';
        if (!k || list.indexOf(k) >= 0) {
            return;
        }
        list.push(k);
        try {
            localStorage.setItem('ah_secs', JSON.stringify(list));
        } catch (e) {
            return;
        }
    }

    function navSave() {
        var m = el('md-menu');
        var w = el('md-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminMediaJs.').join('{js}.').split('md-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('md-').join('{p}-'));
            }
        } catch (e) {
            return;
        }
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

    function navY() {
        var n = el('md-nav');
        if (!n) {
            return;
        }
        var y = 0;
        try {
            y = parseInt(sessionStorage.getItem('ah_navy') || '0', 10) || 0;
        } catch (e) {
            y = 0;
        }
        n.scrollTop = y;
        var a = n.querySelector('.md-mi.md-on');
        if (!a) {
            return;
        }
        var r = a.getBoundingClientRect();
        var b = n.getBoundingClientRect();
        if (r.top < b.top) {
            n.scrollTop -= b.top - r.top + 12;
        } else if (r.bottom > b.bottom) {
            n.scrollTop += r.bottom - b.bottom + 12;
        }
    }

    function navWatch() {
        if (el('md-menu')) {
            navPaint();
            return;
        }
        if (!window.MutationObserver) {
            return;
        }
        var o = new MutationObserver(function () {
            if (navDone) {
                o.disconnect();
                return;
            }
            if (el('md-menu')) {
                o.disconnect();
                navPaint();
            }
        });
        o.observe(document.documentElement, { childList: true, subtree: true });
    }

    function navPaint() {
        if (navDone) {
            return;
        }
        var n = el('md-nav');
        var m = el('md-menu');
        var w = el('md-who');
        if (!n || !m) {
            return;
        }
        navDone = true;
        var mh = '';
        var wh = '';
        try {
            mh = sessionStorage.getItem('ah_menu') || '';
            wh = sessionStorage.getItem('ah_who') || '';
        } catch (e) {
            mh = '';
        }
        if (w && wh && !w.innerHTML.trim()) {
            w.innerHTML = wh.split('{p}-').join('md-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminMediaJs.').split('{p}-').join('md-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.md-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('md-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.md-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.md-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('md-shere', here);
                all[j].classList.toggle('md-sopen', open);
                all[j].firstChild.setAttribute('aria-expanded', open ? 'true' : 'false');
            }
        }
        navY();
        if (window.requestAnimationFrame) {
            requestAnimationFrame(navY);
        }
        n.addEventListener('scroll', function () {
            try {
                sessionStorage.setItem('ah_navy', String(n.scrollTop));
            } catch (e) {
                return;
            }
        });
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        unsave: unsave,
        sec: sec,
        secs: secs,
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: shut,
        loaded: loaded,
        list: list,
        search: search,
        setKind: setKind,
        ppick: ppick,
        pupload: pupload,
        vpick: vpick,
        vupload: vupload,
        dpick: dpick,
        dcov: dcov,
        dupload: dupload,
        chunkOk: chunkOk,
        fail: fail,
        done: done,
        postered: postered,
        view: view,
        rpick: rpick,
        save: save,
        saved: saved,
        del: del,
        inUse: inUse,
        removed: removed
    };
})();

AdminMediaJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminMediaJs.navPaint();
        AdminMediaJs.reveal();
    });
} else {
    AdminMediaJs.navPaint();
    AdminMediaJs.reveal();
}
