# PixelGenesis — özgün ses paketi

61 ses: 30 oyun efekti, 5 arayüz/bildirim sesi, 6 çevre döngüsü ve 10 çağın her biri için 2 müzik katmanı.

Bu dosyalar PixelGenesis için matematiksel sentezle özgün olarak üretildi; dışarıdan ses kaydı, örnekleme veya başka oyundan müzik kullanılmadı. Projenin parçası olarak düzenlenip dağıtılabilir. Doğa ve şehir sesleri de tasarlanmış efektlerdir; saha kaydı veya gerçek konuşma içermez.

- Üretici: `python Tools/generate_audio.py` (Python + NumPy).
- Dosya eşlemesi: `Data/_generator/p6_audio.py` → `Data/sounds.json`.
- Biçim: 22.050 Hz, 16 bit, mono PCM WAV. Dünya efektlerinin stereo konumu oyunun AudioManager'ı tarafından verilir.
- Çevre döngüleri 16 saniye; aynı çağın iki müzik katmanı aynı tempoda, armonide ve 26,667 saniye uzunluğundadır. Yankı kuyrukları döngünün başına sarılır.
- Müzik ve efektlerin sesi oyun ayarlarından ayrı ayrı değiştirilebilir.
- Kısa dinleme seçkisi: `Logs/audio-preview.wav` (oyun paketine dahil değildir).
- `manifest.json`: üretim ölçümleri. `Tools/audit_audio.py` dosyaların kendisini ve veri referanslarını doğrular.

Unity DataSync, WAV dosyalarını `Assets/StreamingAssets/Audio` altına kopyalar ve WebGL dosya listesine ekler. Yeni oynatıcı düzeltmesinin yayına girmesi için yeni Unity build gerekir; eski ZIP dosyaları bu ses güncellemesini içermez.
