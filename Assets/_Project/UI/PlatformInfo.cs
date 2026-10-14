using UnityEngine;

namespace PG.UI
{
    // One answer to "is this a touch device?". Application.isMobilePlatform is not reliable for WebGL running in a
    // phone browser, so the web build asks the browser (PGWebBridge.jslib).
    public static class PlatformInfo
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PG_IsTouchDevice();
        static readonly bool Touch = Application.isMobilePlatform || PG_IsTouchDevice() != 0;
#else
        static readonly bool Touch = Application.isMobilePlatform;
#endif
        public static bool IsTouchDevice => Touch;
    }
}
