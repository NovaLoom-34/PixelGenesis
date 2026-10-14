using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Content
{
    // Bölüm 1.14 mobile + web: StreamingAssets is a plain folder on desktop, lives inside the
    // APK jar on Android (System.IO enumeration/reads fail), and is a same-origin URL on WebGL
    // (no System.IO and NO directory listing over HTTP). All content readers go through this
    // helper: direct file IO on desktop, Java AssetManager on Android, and a manifest.json +
    // synchronous fetch bridge on WebGL. Every method takes a full path and keeps desktop
    // behavior byte-identical.
    public static class ContentFiles
    {
        const int ReadChunk = 1 << 16;

        static AndroidJavaObject _assets;
        static IntPtr _readMid; // java/io/InputStream.read([B)I

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern IntPtr PG_FetchSyncBase64(string url);
        [DllImport("__Internal")] static extern void PG_Free(IntPtr ptr);

        static string[] _manifest = Array.Empty<string>();
        static bool _manifestTried;
#endif

        public static bool IsAndroidAsset(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return path != null && path.StartsWith(Application.streamingAssetsPath, StringComparison.Ordinal);
#else
            return false;
#endif
        }

        public static bool IsWebAsset(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return path != null && path.StartsWith(Application.streamingAssetsPath, StringComparison.Ordinal);
#else
            return false;
#endif
        }

        static string Rel(string path)
        {
            return path.Substring(Application.streamingAssetsPath.Length).TrimStart('/');
        }

        // DataSync publishes every bundled file as {"files":[...]} relative to StreamingAssets.
        static string[] Manifest()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_manifestTried)
            {
                _manifestTried = true;
                try
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(
                        System.Text.Encoding.UTF8.GetString(Fetch("manifest.json")));
                    if (parsed != null && parsed.TryGetValue("files", out var files))
                        _manifest = files ?? Array.Empty<string>();
                }
                catch (Exception) { _manifest = Array.Empty<string>(); }
            }
            return _manifest;
#else
            return Array.Empty<string>();
#endif
        }

        static byte[] Fetch(string rel)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr p = PG_FetchSyncBase64(Application.streamingAssetsPath + "/" + rel);
            if (p == IntPtr.Zero) return Array.Empty<byte>();
            string b64 = Marshal.PtrToStringAnsi(p);
            PG_Free(p);
            return Convert.FromBase64String(b64);
#else
            return Array.Empty<byte>();
#endif
        }

        static AndroidJavaObject AssetManager()
        {
            if (_assets == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _assets = activity.Call<AndroidJavaObject>("getAssets");
            }
            return _assets;
        }

        // list() on a missing path returns an empty array (no throw), so an existing-but-empty
        // directory is indistinguishable from a missing one; our content roots are never empty.
        static string[] AndroidList(string rel)
        {
            try { return AssetManager().Call<string[]>("list", rel); }
            catch (Exception) { return null; }
        }

        // open() succeeds for files and throws for directories — the only way to tell them apart.
        static bool AndroidIsFile(string rel)
        {
            try
            {
                using (var input = AssetManager().Call<AndroidJavaObject>("open", rel))
                    return true;
            }
            catch (Exception) { return false; }
        }

        // Unity marshals byte[] ARGUMENTS one-way (C# -> Java): a native read() filling such an
        // argument never reaches C# — the caller keeps the original (zeroed) buffer, so every
        // asset came back as NUL bytes and JSON parse failed at end-of-input. Read into a
        // self-created jbyteArray instead and copy back with ConvertFromJNIArray, whose
        // Java -> C# direction is exact.
        static byte[] AndroidRead(string rel)
        {
            using (var input = AssetManager().Call<AndroidJavaObject>("open", rel))
            {
                IntPtr inCls = AndroidJNI.FindClass("java/io/InputStream");
                if (_readMid == IntPtr.Zero) _readMid = AndroidJNI.GetMethodID(inCls, "read", "([B)I");
                IntPtr jbuf = AndroidJNI.NewByteArray(ReadChunk);
                try
                {
                    var one = new jvalue[1];
                    one[0] = new jvalue { l = jbuf };
                    using (var ms = new MemoryStream())
                    {
                        int n;
                        while ((n = AndroidJNI.CallIntMethod(input.GetRawObject(), _readMid, one)) > 0)
                        {
                            byte[] chunk = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(jbuf);
                            ms.Write(chunk, 0, n);
                        }
                        return ms.ToArray();
                    }
                }
                finally
                {
                    AndroidJNI.DeleteLocalRef(jbuf);
                    AndroidJNI.DeleteLocalRef(inCls);
                }
            }
        }

        static bool Match(string name, string pattern)
        {
            int star = pattern.IndexOf('*');
            if (star < 0) return string.Equals(name, pattern, StringComparison.Ordinal);
            string prefix = pattern.Substring(0, star);
            string suffix = pattern.Substring(star + 1);
            return name.Length >= prefix.Length + suffix.Length
                && name.StartsWith(prefix, StringComparison.Ordinal)
                && name.EndsWith(suffix, StringComparison.Ordinal);
        }

        public static bool DirExists(string dir)
        {
            if (IsWebAsset(dir))
            {
                var m = Manifest();
                string rel = Rel(dir);
                if (rel.Length == 0) return m.Length > 0;
                string prefix = rel + "/";
                foreach (var e in m)
                    if (e.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }
            if (!IsAndroidAsset(dir)) return Directory.Exists(dir);
            var entries = AndroidList(Rel(dir));
            return entries != null && entries.Length > 0;
        }

        public static string[] Dirs(string dir)
        {
            if (IsWebAsset(dir))
            {
                var m = Manifest();
                string rel = Rel(dir);
                string prefix = rel.Length == 0 ? "" : rel + "/";
                var seen = new HashSet<string>();
                var dirs = new List<string>();
                foreach (var e in m)
                {
                    if (!e.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string rest = e.Substring(prefix.Length);
                    int slash = rest.IndexOf('/');
                    if (slash <= 0) continue;
                    string seg = rest.Substring(0, slash);
                    if (seen.Add(seg)) dirs.Add(dir + "/" + seg);
                }
                return dirs.ToArray();
            }
            if (!IsAndroidAsset(dir)) return Directory.Exists(dir) ? Directory.GetDirectories(dir) : Array.Empty<string>();
            var entries = AndroidList(Rel(dir));
            if (entries == null) return Array.Empty<string>();
            string relDir = Rel(dir);
            var androidDirs = new List<string>();
            foreach (var e in entries)
            {
                string r = relDir.Length == 0 ? e : relDir + "/" + e;
                if (!AndroidIsFile(r)) androidDirs.Add(dir + "/" + e);
            }
            return androidDirs.ToArray();
        }

        public static string[] Files(string dir, string pattern)
        {
            if (IsWebAsset(dir))
            {
                var m = Manifest();
                string rel = Rel(dir);
                string prefix = rel.Length == 0 ? "" : rel + "/";
                var files = new List<string>();
                foreach (var e in m)
                {
                    if (!e.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string rest = e.Substring(prefix.Length);
                    if (rest.Contains('/')) continue; // deeper level: belongs to a subdirectory
                    if (!Match(rest, pattern)) continue;
                    files.Add(dir + "/" + rest);
                }
                return files.ToArray();
            }
            if (!IsAndroidAsset(dir)) return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : Array.Empty<string>();
            var entries = AndroidList(Rel(dir));
            if (entries == null) return Array.Empty<string>();
            string relDir = Rel(dir);
            var androidFiles = new List<string>();
            foreach (var e in entries)
            {
                if (!Match(e, pattern)) continue;
                string r = relDir.Length == 0 ? e : relDir + "/" + e;
                if (AndroidIsFile(r)) androidFiles.Add(dir + "/" + e);
            }
            return androidFiles.ToArray();
        }

        public static bool Exists(string path)
        {
            if (IsWebAsset(path))
            {
                string rel = Rel(path);
                foreach (var e in Manifest())
                    if (e == rel) return true;
                return false;
            }
            if (!IsAndroidAsset(path)) return File.Exists(path);
            return AndroidIsFile(Rel(path));
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (IsWebAsset(path)) return Fetch(Rel(path));
            return IsAndroidAsset(path) ? AndroidRead(Rel(path)) : File.ReadAllBytes(path);
        }

        public static string ReadAllText(string path)
        {
            if (IsWebAsset(path)) return System.Text.Encoding.UTF8.GetString(Fetch(Rel(path)));
            return IsAndroidAsset(path)
                ? System.Text.Encoding.UTF8.GetString(AndroidRead(Rel(path)))
                : File.ReadAllText(path);
        }
    }
}
