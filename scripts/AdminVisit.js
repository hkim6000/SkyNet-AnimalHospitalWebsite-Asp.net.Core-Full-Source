var AdminVisitJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function call(method, list) {
        var data = (list || []).slice();
        data.push({ key: 'tok', vlu: getCookie('ah_tok') });
        $ApiRequest('AdminVisit/' + method, JSON.stringify(data));
    }

    function toast(msg) {
        var t = el('vs-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('vs-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('vs-open');
        }, 3400);
    }

    function openNav() {
        el('vs-nav').classList.add('vs-open');
        el('vs-scrim').classList.add('vs-open');
    }

    function closeNav() {
        el('vs-nav').classList.remove('vs-open');
        el('vs-scrim').classList.remove('vs-open');
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
        var open = !box.classList.contains('vs-sopen');
        box.classList.toggle('vs-sopen', open);
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
        var all = document.querySelectorAll('.vs-sec');
        for (var i = 0; i < all.length; i++) {
            if (list.indexOf(all[i].getAttribute('data-sec')) >= 0) {
                all[i].classList.add('vs-sopen');
                all[i].firstChild.setAttribute('aria-expanded', 'true');
            }
        }
        navHere(list);
        navSave();
        navY();
    }

    var navDone = false;

    function navHere(list) {
        var h = document.querySelector('.vs-sec.vs-shere');
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
        var m = el('vs-menu');
        var w = el('vs-who');
        try {
            if (m && m.innerHTML) {
                sessionStorage.setItem('ah_menu', m.innerHTML.split('AdminVisitJs.').join('{js}.').split('vs-').join('{p}-'));
            }
            if (w && w.innerHTML) {
                sessionStorage.setItem('ah_who', w.innerHTML.split('vs-').join('{p}-'));
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
        var n = el('vs-nav');
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
        var a = n.querySelector('.vs-mi.vs-on');
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
        if (el('vs-menu')) {
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
            if (el('vs-menu')) {
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
        var n = el('vs-nav');
        var m = el('vs-menu');
        var w = el('vs-who');
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
            w.innerHTML = wh.split('{p}-').join('vs-');
        }
        if (mh && !m.innerHTML.trim()) {
            m.innerHTML = mh.split('{js}.').join('AdminVisitJs.').split('{p}-').join('vs-');
            var page = location.pathname.replace(/^.*\//, '') || 'Admin';
            var links = m.querySelectorAll('.vs-mi');
            for (var i = 0; i < links.length; i++) {
                links[i].classList.toggle('vs-on', links[i].getAttribute('href') === page);
            }
            var list = openSecs();
            var all = m.querySelectorAll('.vs-sec');
            for (var j = 0; j < all.length; j++) {
                var here = !!all[j].querySelector('.vs-on');
                var open = here || list.indexOf(all[j].getAttribute('data-sec')) >= 0;
                all[j].classList.toggle('vs-shere', here);
                all[j].classList.toggle('vs-sopen', open);
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

    function openModal(html) {
        el('vs-mbody').innerHTML = html;
        el('vs-modal').classList.add('vs-open');
        document.body.style.overflow = 'hidden';
        times(el('vs-mbody'));
    }

    function closeModal() {
        el('vs-modal').classList.remove('vs-open');
        document.body.style.overflow = '';
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

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    function signOut() {
        call('SignOut', []);
    }

    function out() {
        navClear();
        setCookie('ah_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function expired() {
        out();
    }

    function denied(html) {
        el('vs-main').innerHTML = html;
    }

    var openV = '';
    var ftimer = null;
    var ctimer = null;
    var pending = null;
    var upFile = null;
    var up = null;
    var busy = false;
    var CHUNK = 1048576;
    var locked = false;
    var ltimer = null;
    var fkey = '';
    var dkey = '';
    var lastAct = null;
    var pend = null;

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function lock(b) {
        if (locked) {
            return false;
        }
        locked = true;
        if (b) {
            b.disabled = true;
            b.setAttribute('data-vslk', '1');
        }
        clearTimeout(ltimer);
        ltimer = setTimeout(unlock, 20000);
        return true;
    }

    function unlock() {
        locked = false;
        clearTimeout(ltimer);
        var all = document.querySelectorAll('[data-vslk]');
        for (var i = 0; i < all.length; i++) {
            all[i].disabled = false;
            all[i].removeAttribute('data-vslk');
        }
    }

    function act(method, list) {
        lastAct = { m: method, l: (list || []).slice() };
        var data = (list || []).slice();
        data.push({ key: 'visitid', vlu: openV });
        call(method, data);
    }

    function reveal() {
        var s = el('vs-site');
        openV = s ? (s.getAttribute('data-open') || '') : '';
        call('Load', [{ key: 'openv', vlu: openV }]);
    }

    function ready() {
        times(el('vs-main'));
    }

    function drawn() {
        dkey = hex();
        times(el('vs-main'));
    }

    function find(pg) {
        call('Find', [
            { key: 'term', vlu: val('vs-term') },
            { key: 'dfrom', vlu: val('vs-dfrom') },
            { key: 'dto', vlu: val('vs-dto') },
            { key: 'vstat', vlu: val('vs-vstat') },
            { key: 'vtype', vlu: val('vs-vtype') },
            { key: 'vstaff', vlu: val('vs-vstaff') },
            { key: 'pg', vlu: String(pg || 1) }
        ]);
    }

    function typed() {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            find(1);
        }, 350);
    }

    function go(id) {
        location.href = 'AdminVisit?v=' + encodeURIComponent(id);
    }

    function shown(focus) {
        fkey = hex();
        el('vs-modal').classList.add('vs-open');
        document.body.style.overflow = 'hidden';
        times(el('vs-mbody'));
        var f = focus ? el(focus) : null;
        if (f) {
            f.focus();
        }
    }

    function ask(title, text, btn, fn, danger) {
        pending = fn;
        openModal('<h2>' + esc(title) + '</h2><p class="vs-mlead">' + esc(text) + '</p><div class="vs-mfoot"><button type="button" class="vs-btn vs-btn-ghost" onclick="AdminVisitJs.closeModal()">Not now</button><button type="button" class="vs-btn ' + (danger ? 'vs-btn-danger' : 'vs-btn-accent') + '" onclick="AdminVisitJs.yes()">' + esc(btn) + '</button></div>');
    }

    function yes() {
        var f = pending;
        pending = null;
        closeModal();
        if (f) {
            f();
        }
    }

    function newNote() {
        act('NoteForm', []);
    }

    function editNote(id) {
        act('NoteForm', [{ key: 'noteid', vlu: id }]);
    }

    function addendum(id) {
        act('NoteForm', [{ key: 'parentid', vlu: id }]);
    }

    function saveNote(id, parent) {
        if (!lock(el('vs-nsave'))) {
            return;
        }
        act('NoteSave', [
            { key: 'newid', vlu: fkey },
            { key: 'noteid', vlu: id },
            { key: 'parentid', vlu: parent },
            { key: 'nkind', vlu: val('vs-nkind') },
            { key: 'ntemp', vlu: val('vs-ntemp') },
            { key: 'nhr', vlu: val('vs-nhr') },
            { key: 'nrr', vlu: val('vs-nrr') },
            { key: 'nwt', vlu: val('vs-nwt') },
            { key: 'ns', vlu: val('vs-ns') },
            { key: 'no', vlu: val('vs-no') },
            { key: 'na', vlu: val('vs-na') },
            { key: 'np', vlu: val('vs-np') }
        ]);
    }

    function delNote(id) {
        ask('Delete this note?', 'The note is removed for good. Signed notes can\'t be deleted.', 'Delete note', function () {
            act('NoteDelete', [{ key: 'noteid', vlu: id }]);
        }, true);
    }

    function sign(id) {
        ask('Sign this note?', 'A signed note is locked. Later changes are made with an addendum.', 'Sign note', function () {
            act('NoteSign', [{ key: 'noteid', vlu: id }]);
        });
    }

    function signMine(n) {
        ask('Sign all your notes?', n + (n === 1 ? ' note' : ' notes') + ' you wrote on this visit will be locked.', 'Sign all', function () {
            act('SignMine', []);
        });
    }

    function dxAdd() {
        if (!lock(el('vs-dxgo'))) {
            return;
        }
        act('DxAdd', [{ key: 'newid', vlu: dkey }, { key: 'dxtext', vlu: val('vs-dxtext') }, { key: 'dxstat', vlu: val('vs-dxstat') }]);
    }

    function dxStat(id, st) {
        act('DxStatus', [{ key: 'dxid', vlu: id }, { key: 'dxstat', vlu: st }]);
    }

    function dxDel(id, text) {
        ask('Remove "' + text + '"?', 'The diagnosis is taken off this visit.', 'Remove', function () {
            act('DxDelete', [{ key: 'dxid', vlu: id }]);
        }, true);
    }

    function addOrder() {
        act('Catalog', [{ key: 'cfull', vlu: '1' }]);
    }

    function cat() {
        clearTimeout(ctimer);
        ctimer = setTimeout(function () {
            var all = el('vs-callsp');
            act('Catalog', [{ key: 'cterm', vlu: val('vs-cterm') }, { key: 'ctype', vlu: val('vs-ctype') }, { key: 'callsp', vlu: all && all.checked ? '1' : '' }]);
        }, 250);
    }

    function pick(id) {
        act('LineForm', [{ key: 'itemid', vlu: id }]);
    }

    function editLine(id) {
        act('LineForm', [{ key: 'lineid', vlu: id }]);
    }

    function saveLine(item, line) {
        if (!lock(el('vs-lsave'))) {
            return;
        }
        act('LineSave', [
            { key: 'newid', vlu: fkey },
            { key: 'itemid', vlu: item },
            { key: 'lineid', vlu: line },
            { key: 'qty', vlu: val('vs-lqty') },
            { key: 'dose', vlu: val('vs-ldose') },
            { key: 'instr', vlu: val('vs-linstr') },
            { key: 'disp', vlu: val('vs-ldisp') },
            { key: 'lnote', vlu: val('vs-lnote') }
        ]);
    }

    function lineSt(id, st) {
        act('LineSet', [{ key: 'lineid', vlu: id }, { key: 'lstat', vlu: st }]);
    }

    function cancelLine(id, name, res) {
        ask('Cancel ' + name + '?', res > 0 ? 'This test already has results. Cancelling removes them; the line stays on the visit, crossed out, and is not charged.' : 'The line stays on the visit, crossed out, and is not charged.', res > 0 ? 'Cancel and remove results' : 'Cancel line', function () {
            lineSt(id, 'cancelled');
        }, true);
    }

    function resForm(id) {
        act('ResultForm', [{ key: 'lineid', vlu: id }]);
    }

    function resView(id) {
        act('ResultForm', [{ key: 'lineid', vlu: id }, { key: 'rview', vlu: '1' }]);
    }

    function flag(inp) {
        var v = parseFloat(inp.value);
        var lo = parseFloat(inp.getAttribute('data-lo'));
        var hi = parseFloat(inp.getAttribute('data-hi'));
        inp.classList.remove('vs-fh', 'vs-fl');
        if (inp.value.trim() === '' || isNaN(v)) {
            return;
        }
        if (!isNaN(lo) && v < lo) {
            inp.classList.add('vs-fl');
        } else if (!isNaN(hi) && v > hi) {
            inp.classList.add('vs-fh');
        }
    }

    function saveRes(id, cid, btn) {
        if (!lock(btn)) {
            return;
        }
        var box = el('vs-rc-' + cid);
        var list = [];
        var ins = box ? box.querySelectorAll('[data-k]') : [];
        for (var i = 0; i < ins.length; i++) {
            list.push({ k: ins[i].getAttribute('data-k'), v: ins[i].value });
        }
        act('ResultSave', [{ key: 'lineid', vlu: id }, { key: 'rvals', vlu: JSON.stringify(list) }]);
    }

    function mb(n) {
        return n >= 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.ceil(n / 1024)) + ' KB';
    }

    function hex() {
        var a = new Uint8Array(16);
        crypto.getRandomValues(a);
        return Array.prototype.map.call(a, function (b) {
            return ('0' + b.toString(16)).slice(-2);
        }).join('');
    }

    function pickFile(input) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        input.value = '';
        var e = el('vs-e-file');
        e.textContent = '';
        upFile = null;
        el('vs-fname').textContent = 'PDF, photo (JPG, PNG, WebP), TXT or CSV';
        if (!f) {
            return;
        }
        var m = f.name.match(/\.[^.]+$/);
        var ext = m ? m[0].toLowerCase() : '';
        if (['.pdf', '.jpg', '.jpeg', '.png', '.webp', '.txt', '.csv'].indexOf(ext) < 0) {
            e.textContent = 'Use a PDF, JPG, PNG, WebP, TXT or CSV file.';
            return;
        }
        if (f.size === 0) {
            e.textContent = 'This file is empty.';
            return;
        }
        if (f.size > max) {
            e.textContent = 'Files must be ' + Math.round(max / 1048576) + ' MB or smaller. This one is ' + mb(f.size) + '.';
            return;
        }
        upFile = f;
        el('vs-fname').textContent = f.name + ' · ' + mb(f.size);
        if (!val('vs-ftitle')) {
            el('vs-ftitle').value = f.name.replace(/\.[^.]+$/, '').slice(0, 120);
        }
    }

    function lockUp(on) {
        busy = on;
        var b = el('vs-fgo');
        if (b) {
            b.disabled = on;
            b.textContent = on ? 'Uploading…' : 'Upload';
        }
        var p = el('vs-fprog');
        if (p) {
            p.classList.toggle('vs-on', on);
        }
    }

    function bar(f) {
        var b = el('vs-fbar');
        if (b) {
            b.style.width = Math.round(f * 100) + '%';
        }
    }

    function upload() {
        if (busy) {
            return;
        }
        var e = el('vs-e-file');
        if (!upFile) {
            e.textContent = 'Choose a file first.';
            return;
        }
        e.textContent = '';
        var s = el('vs-fsend');
        up = { file: upFile, uid: hex(), total: Math.max(1, Math.ceil(upFile.size / CHUNK)), title: val('vs-ftitle'), line: val('vs-fline'), send: s && s.checked ? '1' : '' };
        lockUp(true);
        bar(0);
        send(0);
    }

    function send(i) {
        if (!up) {
            return;
        }
        var r = new FileReader();
        r.onload = function () {
            var d = String(r.result);
            act('FilePart', [{ key: 'uid', vlu: up.uid }, { key: 'idx', vlu: String(i) }, { key: 'total', vlu: String(up.total) }, { key: 'part', vlu: d.substring(d.indexOf(',') + 1) }]);
        };
        r.onerror = function () {
            upFail('That file could not be read.');
        };
        r.readAsDataURL(up.file.slice(i * CHUNK, (i + 1) * CHUNK));
    }

    function chunkOk(i) {
        if (!up) {
            return;
        }
        bar((i + 1) / up.total);
        if (i + 1 < up.total) {
            send(i + 1);
            return;
        }
        act('FileDone', [{ key: 'uid', vlu: up.uid }, { key: 'fname', vlu: up.file.name }, { key: 'ftitle', vlu: up.title }, { key: 'fline', vlu: up.line }, { key: 'fsend', vlu: up.send }]);
    }

    function upDone() {
        up = null;
        upFile = null;
        lockUp(false);
    }

    function upFail(msg) {
        up = null;
        lockUp(false);
        bar(0);
        var e = el('vs-e-file');
        if (msg && e) {
            e.textContent = msg;
        }
    }

    function delFile(id, title) {
        ask('Delete "' + title + '"?', 'The file is removed from this visit for good.', 'Delete file', function () {
            act('FileDelete', [{ key: 'fileid', vlu: id }]);
        }, true);
    }

    function saveTake() {
        if (!lock(el('vs-tgo'))) {
            return;
        }
        act('TakeSave', [{ key: 'summ', vlu: val('vs-summ') }, { key: 'rdate', vlu: val('vs-rdate') }]);
    }

    function move() {
        act('MovePick', []);
    }

    function roomPick() {
        var e = el('vs-e-room');
        if (e) {
            e.textContent = '';
        }
    }

    function moveGo() {
        var r = document.querySelector('input[name="vs-room"]:checked');
        var e = el('vs-e-room');
        if (!r) {
            e.textContent = 'Pick a room.';
            return;
        }
        act('MoveGo', [{ key: 'roomid', vlu: r.value }, { key: 'staffid', vlu: val('vs-mstaff') }]);
    }

    function startAsk() {
        pend = null;
        act('StartAsk', []);
    }

    function needStart() {
        unlock();
        var p = lastAct;
        if (p && (p.m === 'FilePart' || p.m === 'FileDone')) {
            upFail('');
            pend = { up: true };
        } else if (p && p.m !== 'StartAsk' && p.m !== 'StartGo') {
            pend = p;
        } else {
            pend = null;
        }
        shown('');
    }

    function startGo() {
        var r = document.querySelector('input[name="vs-room"]:checked');
        var e = el('vs-e-room');
        if (!r) {
            e.textContent = 'Pick a room.';
            return;
        }
        if (!lock(el('vs-stgo'))) {
            return;
        }
        act('StartGo', [{ key: 'roomid', vlu: r.value }, { key: 'staffid', vlu: val('vs-mstaff') }]);
    }

    function resume() {
        var p = pend;
        pend = null;
        unlock();
        if (!p) {
            return;
        }
        if (p.up) {
            upload();
            return;
        }
        act(p.m, p.l);
    }

    function toCheckout(pet) {
        ask('Send ' + pet + ' to checkout?', 'The room becomes free and the visit moves to Ready for checkout at the front desk.', 'Ready for checkout', function () {
            act('Ready', []);
        });
    }

    return {
        navPaint: navPaint,
        navWatch: navWatch,
        reveal: reveal,
        ready: ready,
        drawn: drawn,
        unlock: unlock,
        find: find,
        typed: typed,
        go: go,
        shown: shown,
        yes: yes,
        newNote: newNote,
        editNote: editNote,
        addendum: addendum,
        saveNote: saveNote,
        delNote: delNote,
        sign: sign,
        signMine: signMine,
        dxAdd: dxAdd,
        dxStat: dxStat,
        dxDel: dxDel,
        addOrder: addOrder,
        cat: cat,
        pick: pick,
        editLine: editLine,
        saveLine: saveLine,
        lineSt: lineSt,
        cancelLine: cancelLine,
        resForm: resForm,
        resView: resView,
        flag: flag,
        saveRes: saveRes,
        pickFile: pickFile,
        upload: upload,
        chunkOk: chunkOk,
        upDone: upDone,
        upFail: upFail,
        delFile: delFile,
        saveTake: saveTake,
        move: move,
        roomPick: roomPick,
        moveGo: moveGo,
        startAsk: startAsk,
        needStart: needStart,
        startGo: startGo,
        resume: resume,
        toCheckout: toCheckout,
        secs: secs,
        sec: sec,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        openModal: openModal,
        closeModal: closeModal,
        signOut: signOut,
        out: out,
        expired: expired,
        denied: denied
    };
})();

AdminVisitJs.navWatch();
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        AdminVisitJs.navPaint();
        AdminVisitJs.reveal();
    });
} else {
    AdminVisitJs.navPaint();
    AdminVisitJs.reveal();
}
