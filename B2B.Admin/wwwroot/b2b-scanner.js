// Minimal barcode/QR scanner for Blazor Server (camera -> decode once -> return text).
// Uses ZXing via CDN at runtime; no build tooling required.
(function () {
  const ZXING_CDN = "https://cdn.jsdelivr.net/npm/@zxing/browser@0.1.5/umd/index.min.js";

  async function ensureZxing() {
    if (window.ZXing && window.ZXing.BrowserMultiFormatReader) return;

    await new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[src="${ZXING_CDN}"]`);
      if (existing) {
        existing.addEventListener("load", resolve, { once: true });
        existing.addEventListener("error", reject, { once: true });
        return;
      }
      const s = document.createElement("script");
      s.src = ZXING_CDN;
      s.async = true;
      s.onload = resolve;
      s.onerror = reject;
      document.head.appendChild(s);
    });
  }

  function stopStream(videoEl) {
    try {
      const stream = videoEl && videoEl.srcObject;
      if (stream && stream.getTracks) stream.getTracks().forEach(t => t.stop());
      if (videoEl) videoEl.srcObject = null;
    } catch {
      // ignore
    }
  }

  window.b2bScanner = {
    /**
     * Scan once from the given <video> element and resolve with decoded text.
     * @param {string} videoElementId
     * @returns {Promise<string>}
     */
    scanOnce: async function (videoElementId) {
      await ensureZxing();
      const videoEl = document.getElementById(videoElementId);
      if (!videoEl) throw new Error("Video element not found.");

      const reader = new window.ZXing.BrowserMultiFormatReader();
      try {
        const result = await reader.decodeOnceFromVideoDevice(undefined, videoEl);
        return (result && result.text) ? result.text : "";
      } finally {
        try { reader.reset(); } catch { }
        stopStream(videoEl);
      }
    },
    stop: function (videoElementId) {
      const videoEl = document.getElementById(videoElementId);
      stopStream(videoEl);
    }
  };
})();

