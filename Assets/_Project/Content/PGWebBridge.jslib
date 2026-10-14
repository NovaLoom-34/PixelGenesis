mergeInto(LibraryManager.library, {
  // Synchronous same-origin fetch for StreamingAssets on WebGL: the boot flow reads
  // content without async plumbing, so the bridge uses legacy sync XHR (allowed for
  // same-origin). Binary bodies ride in as charset=x-user-defined and are returned
  // base64-encoded in a malloc'd C string (caller frees via PG_Free). Returns 0 on failure.
  PG_FetchSyncBase64: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try {
      var xhr = new XMLHttpRequest();
      xhr.open('GET', url, false);
      xhr.overrideMimeType('text/plain; charset=x-user-defined');
      xhr.send(null);
      if (xhr.status !== 200 && xhr.status !== 0) return 0;
      var text = xhr.responseText;
      var bin = '';
      for (var i = 0; i < text.length; i += 8192) {
        var slice = text.substr(i, 8192);
        var part = '';
        for (var j = 0; j < slice.length; j++) part += String.fromCharCode(slice.charCodeAt(j) & 0xff);
        bin += part;
      }
      var b64 = btoa(bin);
      var len = lengthBytesUTF8(b64) + 1;
      var ptr = _malloc(len);
      stringToUTF8(b64, ptr, len);
      return ptr;
    } catch (e) {
      return 0;
    }
  },
  PG_Free: function (ptr) { _free(ptr); },
  // 1 when the browser's primary input is touch (phones, tablets incl. iPadOS that reports as a Mac); 0 for mouse devices.
  PG_IsTouchDevice: function () {
    try {
      var coarse = window.matchMedia && window.matchMedia('(pointer: coarse)').matches;
      var noHover = window.matchMedia && window.matchMedia('(hover: none)').matches;
      var ua = /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
      var ipadOs = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
      return (coarse && noHover) || ua || ipadOs ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },
  PG_CopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text);
      else {
        var input = document.createElement('textarea'); input.value = text;
        input.style.position = 'fixed'; input.style.opacity = '0'; document.body.appendChild(input);
        input.select(); document.execCommand('copy'); input.remove();
      }
    } catch (_) {}
  },
  PG_DownloadText: function (namePtr, textPtr, mimePtr) {
    var blob = new Blob([UTF8ToString(textPtr)], { type: UTF8ToString(mimePtr) });
    var link = document.createElement('a'); link.href = URL.createObjectURL(blob);
    link.download = UTF8ToString(namePtr); link.click();
    setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
  },
  PG_DownloadWorldSummary: function (titlePtr, detailsPtr) {
    var source = document.querySelector('#unity-canvas') || document.querySelector('canvas');
    if (!source) return;
    var scale = Math.max(1, Math.min(2, 1600 / source.width));
    var canvas = document.createElement('canvas'); canvas.width = Math.round(source.width * scale);
    canvas.height = Math.round(source.height * scale + 180 * scale);
    var ctx = canvas.getContext('2d'); ctx.drawImage(source, 0, 0, canvas.width, source.height * scale);
    var title = UTF8ToString(titlePtr), details = UTF8ToString(detailsPtr);
    var panelY = source.height * scale;
    ctx.fillStyle = '#10171d'; ctx.fillRect(0, panelY, canvas.width, canvas.height - panelY);
    ctx.fillStyle = '#f1d27b'; ctx.font = 'bold ' + (24 * scale) + 'px sans-serif';
    ctx.fillText(title, 24 * scale, panelY + 36 * scale);
    ctx.fillStyle = '#e5edf2'; ctx.font = (14 * scale) + 'px sans-serif';
    var lines = details.split('\n');
    for (var i = 0; i < lines.length; i++) ctx.fillText(lines[i], 24 * scale, panelY + (68 + i * 20) * scale);
    canvas.toBlob(function (blob) {
      if (!blob) return;
      var link = document.createElement('a'); link.href = URL.createObjectURL(blob);
      link.download = 'world-summary.png'; link.click();
      setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
    }, 'image/png');
  }
});
