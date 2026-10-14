# Credits

Third-party code and assets used by PixelGenesis. Every entry pins its version.

| Component | Version | License | Source |
|-----------|---------|---------|--------|
| Newtonsoft Json.NET (`com.unity.nuget.newtonsoft-json`) | 3.2.2 | MIT | https://github.com/JamesNK/Newtonsoft.Json |
| Firebase JavaScript SDK (compat app/auth CDN) | 10.12.2 | Apache-2.0 (SDK; bundled protobuf notices may include BSD-3-Clause) | https://github.com/firebase/firebase-js-sdk |
| Figtree (website, Google Fonts CDN) | CDN hosted; stylesheet version not pinned | SIL Open Font License 1.1 | https://github.com/erikdkennedy/figtree |
| Pixelify Sans (website, Google Fonts CDN) | CDN hosted; stylesheet version not pinned | SIL Open Font License 1.1 | https://github.com/eifetx/Pixelify-Sans |

Unity packages (`com.unity.*`) are covered by the Unity Companion License / Unity Terms of Service.

The browser page fetches Firebase SDK files from `gstatic.com` and Figtree/Pixelify Sans stylesheets from Google Fonts; these are runtime network dependencies, not locally bundled binaries. No additional planned packages are treated as included dependencies. Recheck the dependency tree and transitive notices against the exact shipping build before distribution.
