#!/usr/bin/env python3
"""Generate the release QA checklist from current power, window, and scenario catalogs."""
from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Docs" / "Yol_Haritasi" / "13_QA_Beta_Kontrol_Listesi.md"


def read_items(path: Path) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    items = payload.get("items")
    if not isinstance(items, list):
        raise ValueError(f"Expected an items array in {path}")
    return items


def windows() -> list[str]:
    result = set()
    pattern = re.compile(r"public\s+(?:sealed\s+)?class\s+(\w+Window)\b")
    for path in (ROOT / "Assets" / "_Project" / "UI" / "Windows").glob("*.cs"):
        result.update(pattern.findall(path.read_text(encoding="utf-8", errors="replace")))
    return sorted(result)


def md(value: object) -> str:
    return str(value or "").replace("|", "\\|").replace("\r", " ").replace("\n", " ").strip()


def render() -> str:
    powers = read_items(ROOT / "Data" / "powers.json")
    scenarios = read_items(ROOT / "Data" / "scenarios.json")
    by_tab: dict[str, list[dict]] = defaultdict(list)
    for power in powers:
        by_tab[md(power.get("tab", "other")) or "other"].append(power)

    lines = [
        "# Yayın QA ve Kapalı Beta Kontrol Listesi",
        "",
        "> Bu liste `Tools/generate_qa_checklist.py` ile mevcut içerik kataloglarından üretilir. Her satır gerçek bir testte çalıştırılmadan işaretlenmez. Kayıtlar, ekran görüntüleri ve Unity sonuçları final kabul kanıtıdır.",
        "",
        "## Test çalışması kimliği",
        "",
        "| Alan | Değer |",
        "|---|---|",
        "| Sürüm / commit |  |",
        "| Build türü / tarihi |  |",
        "| Platform / cihaz / OS / tarayıcı |  |",
        "| Ekran boyutu / yönü / DPI |  |",
        "| Dünya tohumu / boyutu / senaryo / yıl |  |",
        "| Kayıt kimliği / önceki sürüm |  |",
        "| Testçi / sonuç klasörü |  |",
        "",
        "Durum işaretleri: **PASS**, **FAIL**, **BLOCKED**, **N/A**. Her başarısızlık için adımları, beklenen/gerçek sonucu ve gizli bilgi içermeyen tanı çıktısını kaydet.",
        "",
        "## Platform açılış ve temel akış matrisi",
        "",
        "| Platform | Temiz başlatma | Yeni dünya | Kaydet / yükle | Oturum / yön değişimi | Ses | Sonuç / kanıt |",
        "|---|---|---|---|---|---|---|",
        "| WebGL — masaüstü Chrome | [ ] | [ ] | [ ] | [ ] | [ ] |  |",
        "| WebGL — Android Chrome, dikey | [ ] | [ ] | [ ] | [ ] | [ ] |  |",
        "| WebGL — Android Chrome, yatay | [ ] | [ ] | [ ] | [ ] | [ ] |  |",
        "| Android Player, API 25+ | [ ] | [ ] | [ ] | [ ] | [ ] |  |",
        "| Windows masaüstü (yayınlanacaksa) | [ ] | [ ] | [ ] | [ ] | [ ] |  |",
        "",
        "## Yayın öncesi duman ve güvenlik kontrolleri",
        "",
        "| Kontrol | Durum | Kanıt / not |",
        "|---|---|---|",
        "| Yeni profil: öğretici, ilk güç, ilk yerleşim, pencere aç/kapat | [ ] |  |",
        "| Her ayar/pencere açılır, dar ekran ve dokunmatik hedefleri kullanılabilir | [ ] |  |",
        "| Profil sıfırlama ilerlemeyi, başarımları ve kayıtları korur | [ ] |  |",
        "| Normal kayıt/yükleme; yenileme sonrası kayıt; eski sürüm kaydı | [ ] |  |",
        "| Bozuk ana kayıt `.bak` kurtarması; kasıtlı kesinti testi | [ ] |  |",
        "| İnsan/elf kurucu deneyi: 6 tohum × 100 yıl; 7 erişkin/tür (4 dişi, 3 erkek), iki türde doğum ve tür başına son nüfus ≥7, şehir/krallık | [ ] |  |",
        "| Dünya istatistikleri toplam nüfusu medeniyet/vahşi yaşam olarak ayırır; vahşi nüfus medeniyet çöküşünü gizlemez | [ ] |  |",
        "| Medeniyet çöküşü uyarısı yalnız medeniyet nüfusu 0, yaşayan krallık 0 ve en az bir yıkılmış krallık varken görünür; boş sandbox/yaşayan krallıkta çıkmaz | [ ] |  |",
        "| Tanı paketi API anahtarı, e-posta ve yerel kullanıcı yolunu açığa çıkarmaz | [ ] |  |",
        "| Telemetri kapalı; AI sağlayıcı bağlantısı açık uyarı ve kullanıcı ayarıyla çalışır | [ ] |  |",
        "| Konsol hatası yok; desteklenmeyen çevrimdışı/ağ durumları anlaşılır | [ ] |  |",
        "| UI, güç, olay, ölüm, ortam ve dönem müziği sesleri; miks ve mobil hoparlör | [ ] |  |",
        "| Görsel referanslar kullanıcı tarafından onaylanmış; `Tools/visual_compare.py` raporu saklanmış | [ ] |  |",
        "| Web paket doğrulaması ve temiz cache ile açılış | [ ] |  |",
        "| Android imzalama, izin manifesti ve temiz kurulum | [ ] |  |",
        "",
        "## Senaryo kabulü",
        "",
        "Her senaryoda yeni dünyayı başlat; açıklama/hedef/kısıtları doğrula; en az bir izinli ve bir yasaklı güç dene; kazanma ve kayıp koşullarını, yeniden yükleme davranışını kaydet.",
        "",
        "| Senaryo kimliği | Ad | Başlat / hedefler | Kısıtlar | Kazanma | Kaybetme | Save/load | Durum / kanıt |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for scenario in sorted(scenarios, key=lambda item: md(item.get("id"))):
        lines.append(f"| `{md(scenario.get('id'))}` | {md(scenario.get('name'))} | [ ] | [ ] | [ ] | [ ] | [ ] |  |")

    lines.extend([
        "",
        "## Pencere kabulü",
        "",
        "Her pencere için açma, doğru veri, kapatma/yeniden açma, klavye/dokunmatik, dar görünüm ve kayıtlı durumda tekrar kontrol et.",
        "",
        "| Pencere sınıfı | Aç/kapat | İçerik ve eylemler | Dar ekran / dokunmatik | TR/EN | Save/load | Durum / kanıt |",
        "|---|---|---|---|---|---|---|",
    ])
    for window in windows():
        lines.append(f"| `{window}` | [ ] | [ ] | [ ] | [ ] | [ ] |  |")

    lines.extend([
        "",
        "## Güç kabulü",
        "",
        f"Katalogdan {len(powers)} güç; her satırda doğru hedef seçimi, geçersiz/hedef dışı durum, cooldown/ücret, ses/görsel geri bildirim ve mümkünse undo/save-load etkisini kontrol et.",
        "",
    ])
    for tab in sorted(by_tab):
        lines.extend([
            f"### Sekme: `{tab}` ({len(by_tab[tab])})",
            "",
            "| Güç kimliği | Ad | Hedef/etki | Geçersiz durum | Geri alma/kayıt | Görsel/ses | Durum / kanıt |",
            "|---|---|---|---|---|---|---|",
        ])
        for power in sorted(by_tab[tab], key=lambda item: md(item.get("id"))):
            lines.append(f"| `{md(power.get('id'))}` | {md(power.get('name'))} | [ ] | [ ] | [ ] | [ ] |  |")

    lines.extend([
        "",
        "## Faz kabul ölçümleri",
        "",
        "Her koşuda seed, build, platform ve metrik JSON'u sakla; ortalamayla birlikte en kötü tohumu da değerlendir. Aşağıdaki eşikler ilgili faz belgelerindeki başlangıç hedefleridir.",
        "",
        "| Faz / çalışma | Tohum / süre | Kabul ölçüleri | Sonuç / metrik JSON |",
        "|---|---|---|---|",
        "| A — doğruluk ve kalite kapısı | güncel sürüm · tüm EditMode + determinism/save round-trip | Tüm ilgili testler yeşil (grafik kategorisi ayrı); gate adımları yeşil; ölçüm baseline'ı sürüm ve cihaz bilgisiyle saklı | [ ] |",
        "| B — medeniyet ekonomisi | 2024, 7, 99, 11, 23, 42 · 100 yıl | Ortalama yerleşim ≥5; en yüksek salon ≥2 (bir tohumda 3); en yüksek insan ≥200; kıtlık ≤6; eşya üretimi >0; işsiz ≤%10; uzun nüfus çöküşü yok | [ ] |",
        "| B — insan/elf kurucu topluluğu | 2024, 7, 99, 11, 23, 42 · 100 yıl | Her tohumda iki tür ayrı ayrı 7 erişkinle (4 dişi, 3 erkek) başlar; her ikisinde doğum görülür; her tür son yılda en az 7 canlıyla başlangıç nüfusunu korur; en az bir şehir ve yaşayan krallık kalır | [ ] |",
        "| C — vahşi ekosistem | 6 tohum · 200 yıl, insansız | Başlangıç türlerinin ≥%85'i hayatta; tür patlaması yok (>3× pay); bitki örtüsü başlangıca göre ±%25; avcı–av salınımı sönümlenir | [ ] |",
        "| D — iklim ve kıtlık | 6 tohum · 100 yıl | Kıtlık olaylarının ≥%50'si kuraklık/soğukla işaretli; iklim sabit kontrolüne kıyasla kıtlık değişkenliği artar ama nüfus çökmez; B hedefleri korunur; aylık iklim maliyeti ≤0,3 ms | [ ] |",
        "| E — çok krallıklı dünya | 6 tohum · 300 yıl | Her tohumda ≥4 krallık; krallık başına savaş; aynı iki krallık arasında kesintisiz savaş ≤50 yıl; antlaşma/ittifak ve ticaret görülür; hakimiyet >%80 ise kırılma olayı kaydedilir; meta maliyeti ≤0,3 ms | [ ] |",
        "| F — dünya üretimi | 20 tohum · 256×256 | Şablona uygun nehir sistemi; denize ulaşmayan akış <%5; yerleşilebilir büyük kara parçalarında temel kaynaklar; üretim süresi hedefi ≤3 sn | [ ] |",
        "| G — tarih ve dünya raporu | 6 tohum · 200 yıl | Önemli olaylar yılda ortalama 1–5; hükümdar/komutan biyografileri ve savaş sebep/sonuçları okunur; save/load turunda tarih korunur; bellek artışı ≤5 MB | [ ] |",
        "| H — oyuncu deneyimi | yeni profil · masaüstü ve telefon | Yeni oyuncu ≤3 dakikada ilk gücü kullanır (≥4/5 gözlem); en az 8 senaryo kazanılabilir/kaybedilebilir; geri alma 30 sn içindeki büyük fırçayı geri yükler; telefonda HUD/dokunma çalışır | [ ] |",
        "| I — Player performansı | masaüstü / WebGL / Android | Masaüstü 5000 birimde ≤8 ms ve ≥60 FPS; web 2500 birimde ≥30 FPS; telefon 1200 birimde ≥30 FPS, 10 dakikada çökme/ısınma yok; tick GC tahsisi 0 | [ ] |",
        "| J — uzun süre ve bellek | 2024 + 7, 99, 11, 23, 42 · 1000 yıl | Çökme yok; bellek/tick bütçesi içinde; kayıt-yükleme çevrimleri ve raporlar korunur | [ ] |",
        "| 8 saat hızlandırılmış oturum | cihaz/sürüm kaydet | Çökme/veri kaybı yok; periyodik kayıt ve bellek/tick gözlemi | [ ] |",
        "| Yeniden açma ve son save geri yükleme | kullanılan build / kayıt sürümü | Son tamamlanmış kayıt yüklenir; bozuk/kesik yazımda yedek kurtarma gözlenir | [ ] |",
        "",
        "Bu koşular test/soak aşamasında çalıştırılır; Unity ve kabul edilmiş Player build kullan. Süreç kesilirse son kayıt ve tanı paketini koru. Başarıyı yalnız logun varlığıyla değil metrik raporu ve kayıt turuyla doğrula.",
        "",
        "## Kapalı beta geri bildirim formu",
        "",
        "- Build / platform / cihaz / OS / tarayıcı:",
        "- Senaryo / dünya tohumu / yıl / kayıt kimliği:",
        "- Sorunun özeti:",
        "- Yeniden oluşturma adımları:",
        "- Beklenen sonuç:",
        "- Gerçek sonuç:",
        "- Sıklık: her zaman / sık / bazen / bir kez:",
        "- Etki: çökme-veri kaybı / ilerlemeyi durduruyor / işlev bozuk / görsel-ses kusuru:",
        "- Ekran görüntüsü veya kısa video (kişisel bilgi görünmeden):",
        "- Oyun içi tanı paketi (API anahtarı/kişisel bilgi içermediği kontrol edilerek):",
        "",
        "### Hata önceliği",
        "",
        "| Öncelik | Tanım | Müdahale hedefi |",
        "|---|---|---|",
        "| P0 — Engelleyici | Başlatılamıyor, kayıt kaybı, güvenlik/gizlilik ihlali, çoğu oyuncuda ilerleme imkânsız | Beta durdur; düzeltme ve yeniden kabul |",
        "| P1 — Yüksek | Sık çökme veya temel döngünün önemli bölümü bozuk; uygulanabilir geçici çözüm yok | Yayın öncesi düzelt |",
        "| P2 — Orta | Özellik/cihaz kombinasyonuyla sınırlı kusur; geçici yol var | Sürüm kararında açıkça değerlendir |",
        "| P3 — Düşük | Kozmetik/metin/seyrek küçük rahatsızlık | İzle ve sırala |",
        "",
        "Dış beta katılımcılarına bağlantı gönderilmedi; form ve önceliklendirme hazırlandı. Dağıtım, yanıtların toplanması ve sıfır P0/P1 kabulü ayrıca yürütülmeli.",
        "",
    ])
    return "\n".join(lines)


def main() -> None:
    output = render()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(output, encoding="utf-8")
    power_count = len(read_items(ROOT / "Data" / "powers.json"))
    scenario_count = len(read_items(ROOT / "Data" / "scenarios.json"))
    print(f"Wrote {OUTPUT.relative_to(ROOT)}: {power_count} powers, {scenario_count} scenarios, {len(windows())} windows")


if __name__ == "__main__":
    main()
