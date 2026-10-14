## 2026-10-08 Faz J4 · tüm bina atlas kapsamı

- [x] İki sütunlu atlas metadata desteği eklendi; altı kültür için sur/kapı ile yuva/kovan/harabe atlasları runtime seçiciye bağlandı. Eski üç sütunlu sheet düzeni varsayılan olarak korunuyor.
- [x] `HouseArtTests` kaynak kapsamı 41 katalog türünün tamamına çıktı; önizleme 14 aile/246 kültür-yapı eşleşmesi/738 durumu gösteriyor.
- [x] Statik tarama 738/738 bina anahtarını çözüyor: 9 final-color + 729 atlas, 0 placeholder, 0 fallback. 44/44 dünya özelliği çözülüyor. Altı `fortifications` ve altı `habitats` runtime PNG/JSON çiftinin hash eşitliği, çerçeveleri, GUID'leri ve 2.346 yollu manifest denetlendi.
- [ ] Unity EditMode, gerçek oyun ölçeği/örtüşme ve WebGL/Android kabulü kullanıcı final aşamasında; J4/Faz J açık.

## 2026-10-08 Faz J4 · anıt atlası

- [x] Altı kültürde tanrı heykeli, anıt ve cenaze ateşi için on ikinci saydam üç durumlu atlas ailesi üretildi ve runtime seçiciye bağlandı.
- [x] `HouseArtTests` 36 bina türünü; önizleme 12 aile/216 tür/648 durumu kapsıyor. Kaynak/runtime PNG+JSON eşleşmeleri, 12 yeni metadata GUID'si ve 2.322 dosyalı manifest hazırlandı.
- [x] Statik kapsam 738 anahtarda 9 final-color + 639 atlas, 72 placeholder ve 18 fallback; dünya özellikleri 44/44 çözülüyor.
- [ ] Unity testleri ve oyun içi görsel/ölçek kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Derleme düzeltmeleri

- [x] Offline derleme kontrolünde `wideRiver` kapsam hatası ve `UnityEngine.Object` ad çakışması giderildi; 12 üretim assembly'si 0 hatayla derlendi. Önceki kullanıcı derleme kayıtlarındaki `DataSync` ve `ushort` satırları mevcut test kaynaklarında tam nitelikli çağrı/açık dönüşüm olarak bulunuyor. Unity EditMode/PlayMode çalıştırılmadı.

## 2026-10-08 Faz J4 · dini yapılar atlası

- [x] Altı kültür için büyük tapınak, şifacı evi ve mezarlığın üç durumlu on birinci 3×3 atlas ailesi runtime seçiciye eklendi.
- [x] Test kaynağı 33 bina türünü, tarayıcı önizlemesi 11 atlas ailesi/198 tür/594 durumu kapsıyor. Kaynak/runtime PNG+JSON kopyaları üretildi, 12 benzersiz Unity metadata GUID'si ve 2.310 yollu manifest hazırlandı.
- [x] Statik kapsam 738 anahtarda 9 final-color + 585 atlas, 126 placeholder ve 18 fallback; dünya özellikleri 44/44 çözümleniyor.
- [ ] Unity testi ve gerçek oyun içi ölçek/örtüşme kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J4 · kültür yapıları atlası

- [x] Altı kültür için kütüphane, okul ve tapınağın inşaat/tamamlanmış/harabe karelerini içeren onuncu saydam 3×3 atlas ailesi runtime seçiciye bağlandı.
- [x] `HouseArtTests` 30 bina türünü; önizleme 10 aile, 180 tür ve 540 durumu kapsıyor. Kaynak/runtime PNG+JSON çiftleri ve manifest güncel.
- [x] Statik kapsam 738 anahtarda 9 final-color + 531 atlas, 180 placeholder ve 18 fallback gösteriyor; dünya özellikleri 44/44 çözümleniyor.
- [ ] Unity testi ve oyunda ölçek/örtüşme kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J4 · kıyı yapıları atlası

- [x] Balıkçı kulübesi (4/şehir), iskele (2/şehir) ve tersane (1/şehir) altı kültür için saydam üç durumlu 3×3 atlas olarak hazırlandı ve runtime seçiciye bağlandı.
- [x] Önizleme 9 atlas ailesi/162 tür/486 duruma; `HouseArtTests` kaynağı 27 bina türüne genişletildi. Kaynak/runtime PNG+JSON aynaları, 12 runtime `.meta` GUID'si ve 2.286 dosyalı manifest güncellendi.
- [x] Statik kapsam denetimi 738 yapı anahtarında 9 final-color + 477 atlas, 234 placeholder ve 18 fallback gösteriyor; 44/44 dünya özelliği çözülüyor. Altı `naval` PNG hücresi saydamlık ve çerçeve açısından doğrulandı.
- [ ] Unity derleme/EditMode ve gerçek oyun ölçeği/örtüşme kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J4 · ileri şehir yapıları atlası

- [x] Malikâne, ahır ve kışla için altı kültürün üç durumlu yeni 3×3 atlas ailesi `SpriteLibrary`, metadata üreticisi ve salt okunur kapsam denetimine bağlandı.
- [x] Test kaynağı 24 yapı türüne, tarayıcı önizlemesi 8 aile/144 tür/432 duruma genişletildi. Kaynak/runtime PNG ve JSON aynaları ile manifest güncellendi.
- [x] Statik kapsam 738 bina anahtarında 9 final-color + 423 atlas, 288 placeholder ve 18 fallback; özellikler 44/44. Tüm 432 atlas karesi üretim metadata'sında bulunuyor; insan evi için 9 mevcut tekil çizim yüklemede atlası geçiyor.
- [ ] Unity derleme/EditMode ve oyunda ölçek/örtüşme kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J4 · kamusal yapılar atlası

- [x] Katalogdaki kullanım sayılarıyla önceliklendirilmiş kuyu (4/şehir), yel değirmeni (2/şehir) ve han (1/şehir) altı kültür için atlaslandı; her yapıda üç durum bulunuyor.
- [x] Runtime seçici, atlas üreticisi, denetim aracı ve test kaynağı 21 yapı türüne çıkarıldı. Tarayıcı önizlemesi yedi aile/126 tür/378 durum gösteriyor.
- [x] Statik kapsam 738 bina anahtarında 9 final-color + 369 atlas, 342 placeholder ve 18 fallback; özellikler 44/44. Manifest 2262 girdiye ve kaynak/runtime aynalarına güncellendi.
- [ ] Unity derleme/EditMode ve oyun içi görsel/ölçek kabulü kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J4 · kuruluş yapıları atlası

- [x] Altı kültür için kamp ateşi, çadır ve kulübenin inşaat/tamamlanmış/harabe durumlarını içeren altıncı 3×3 atlas ailesi eklendi. İnsan kulübesinin mevcut tekil renkli çizimleri atlasın önceliğini koruyor.
- [x] `SpriteLibrary`, metadata üreticisi, kapsama denetimi, 18 türlük test kaynağı ve tarayıcı önizlemesi genişletildi; önizleme 108 tür/324 durum gösteriyor.
- [x] Statik kapsam 738 bina anahtarında 9 final-color + 317 atlas, 394 placeholder ve 18 fallback; özellikler 44/44. Kaynak/runtime aynaları ve manifest (2250 girdi) güncellendi.
- [ ] Unity derleme/EditMode, oyun içi ölçü/örtüşme ve görsel kabul kullanıcı final aşamasında; J4/Faz J açık.

## 2026-10-08 Faz J4 · tarım ve gıda yapı atlası

- [x] Altı kültür için fırın, ağıl ve arı kovanı; inşaat/tamamlanmış/harabe evreli altı 3×3 atlas üretildi. Kaynak PNG'ler işlendi; alfa maskesi, sprite ölçeği ve runtime kopyaları çıkarıldı.
- [x] Yükleyici, `DataSync`, kapsama denetimi ve test kaynağı `food` atlas ailesini tanıyor. Önizleme beş atlas grubunu, 90 bina türünü ve 270 durumu yükleyecek şekilde güncellendi.
- [x] Statik kapsama: 738 bina anahtarının 275'i final/atlas, 445'i placeholder, 18'i fallback; özellikler 44/44. StreamingAssets manifesti 2238 kaynak girdisine ve yeni metadata GUID'lerine güncellendi.
- [ ] Unity derleme/EditMode, oyundaki ölçü/örtüşme ve görsel kabulü çalıştırılmadı; kullanıcı final test aşamasında. J4/Faz J açık.

# PixelGenesis geliştirme takibi

## 2026-10-08 Faz A4 · çok tohumlu soak argümanı

- [x] Kalite kapısındaki `powershell -File ... -Seeds @(...)` aktarımı altı tohumu tek metin argümanı yaptığı için düzeltildi; soak betiği artık aynı süreçte `[int[]]` olarak çağrılıyor.
- [x] Gate PowerShell AST'si temiz ayrıştırıldı; `gate_thresholds.json` içindeki 6 tohumun parametre bağlaması izole çağrıda doğrulandı.
- [ ] Unity testleri, çok krallıklı soak ve performans kapısı kullanıcı final aşamasında; A4/Faz A kabul edilmedi.

## 2026-10-08 Faz A1 · test assembly offline derlemesi

- [x] `compile_check.py` 12 üretim assembly'sinden sonra EditMode test assembly'sinin güncel kaynaklarını da offline derliyor; Test Runner veya Unity testi çalıştırmıyor.
- [x] Test kaynaklarındaki `EditSystems.Install`, `PG.Core.EntityId` alias'ı ve `UnitLifeSystem` API çağrısı düzeltildi. Son derleme: 13/13 assembly, 0 hata.
- [ ] Unity EditMode/Graphics testi, soak ve kalite kapısı kullanıcı final aşamasında; A1/Faz A kabul edilmedi.

## 2026-10-08 Faz A5 · tam kayıt durumu determinism karması

- [x] Dünya karmasına bütün zone alanları eklendi; nem/biyokütle için test kaynağı yazıldı.
- [x] Determinizm ve save-load-devam karşılaştırması artık saat/RNG ile doğa, yasalar, tüm birim-alt tür-yol verisi, medeniyet, meta, güç, politika ve icatların kayıt serileştirmesini kapsıyor.
- [x] Offline derleme 12 üretim + EditMode assembly, 0 hata. Unity testleri çalıştırılmadı; A5/Faz A kabul edilmedi.

## 2026-10-08 Faz J4 · detaylı canlı atlasında yuvarlama uyumu

- [x] Kaynak poz maskesi denetimindeki başarısızlık `water_elemental` ve `crystal_beetle` ölçeklerinde yarım piksel sınırından kaynaklandı: üretici `floor(x + 0.5)` kullanırken kaynak atlas geri dönüşü midpoint-to-even `Mathf.RoundToInt` kullanıyordu. Runtime atlas mevcutken normal yükleme bunu gizliyor; runtime PNG bulunmadığında kaynak geri dönüşü hata verebiliyordu.
- [x] `DetailedUnitSheet.Build`, maske üreticisiyle aynı yarım-yukarı örnekleme kuralına geçirildi. `audit_unit_art.py` yuvarlama modelini hizaladı; `DetailedUnitArtTests` iki yarım değer durumunu kapsayan sentetik regresyon kaynağı içeriyor.
- [x] Statik denetim sonrası: 2.304 kaynak poz/maske geometrisi geçti; ağaç sanatı 24/24, ses envanteri 61/61 WAV geçti. 192 runtime canlı atlası/düzeni ile kaynak öğe atlası/eşlemesinin 2 StreamingAssets kopyası bayt bayt eşleşti.
- [ ] Unity derleme/EditMode ve gerçek runtime kaynak geri dönüşü kullanıcı final test aşamasında; StreamingAssets aynası ve görünür poz kabulü de bekliyor. J4/Faz J açık.

## 2026-10-08 Faz J4 · runtime sanat kapsamı ve yeni mimari setler

- [x] `Tools/audit_runtime_art_coverage.py` yükleyicinin gerçek kaynak sırasını bina anahtarlarına ve 44 özellik türüne uygulayan salt okunur envanter olarak eklendi.
- [x] Altı halka için ev/kule/demirhane, yerleşim, salon/depo ve çiftlik/maden/oduncu atlasları var: 72 yapı türü, 216 durum karesi.
- [x] 738 bina anahtarının 221'i final/atlas sanatı, 499'u placeholder, 18'i fallback kullanıyor. Stil kapsamı: insan 41/123; elf/cüce/ork/beast/insect 36/123'er. Özelliklerin 44/44'ü çözülüyor.
- [x] `HouseArtTests` kapsamı 6 stil × 12 yapı × 3 durum; Unity testi çalıştırılmadı.
- [ ] 499 placeholder bina anahtarı ve oyun içi görsel kabulü açık. Unity derleme/testi kullanıcı final aşamasında; J4/Faz J açık.

- [x] `Tools/audit_runtime_art_coverage.py` yükleyicinin gerçek kaynak sırasını bina anahtarlarına ve 44 özellik türüne uygulayan salt okunur envanter olarak eklendi.

## 2026-10-08 Faz H8 · nüfus istatistiği ayrımı

- [x] Dünya istatistiklerinde tüm canlıların toplamına ek olarak `IsCiv` türlerini medeniyet nüfusu, diğer türleri yaban/diğer canlılar olarak ayrı sayıp gösteriyor. Mevcut toplam ve tür dağılımı değişmedi.
- [x] Türkçe/İngilizce UI metinleri `p0_ui.py` üzerinden üretildi.
- [x] Kullanıcının ek ekran görüntüsü incelendi: yıl 171'de toplam 170 canlı, 148 papağan + 22 maymun; insan/elf/başka medeniyet türü ve yaşayan krallık görünmüyor. Bu kayıt o anda medeniyet nüfusunun sıfırlandığını gösteriyor. Ekran, kaynakta eklenmiş “medeniyet nüfusu / yaban hayatı” satırlarını göstermediği için güncel kaynak sürümünden eski bir oyun/WebGL paketi olabilir.
- [ ] Kayıt dosyası olmadan başlangıçtaki insan/elf grubunun hangi ölüm nedeni veya üreme engeliyle tükendiği belirlenemez. `sp.human` ve `sp.elf` tanımları canlı doğuruyor; medeniyet eşleşmesi ayrıca yetişkinlik, karşılıklı istek, yeterli doygunluk/mutluluk, akrabalık ve düşmanlık kontrollerine bağlı. Kayıt/ölüm dökümüyle inceleme kullanıcı final test aşamasında.
- [ ] Unity UI testi ve ekranda sayım doğrulaması kullanıcı final aşamasında; H8/Faz H kabul edilmedi.

## 2026-10-08 Faz J1 · yerelleştirme statik denetimi

- [x] `Tools/loc_audit.py`: 677 katalog girdisi; TR/EN metinleri, format yer tutucuları ve 244 literal kaynak anahtarı tutarlı. Denetim 8 senaryonun TR/EN adlarını ve açıklama/hedef localization anahtarlarını da kapsıyor.
- [x] Doğrudan kaynak eşleşmesi bulunmayan 280 anahtar not edildi; dinamik/içerik tabanlı metinler olabileceğinden otomatik silme veya hata sayılmadı.
- [ ] Dar ekran/telefonda metin taşması ve bütün dinamik anahtar yolları kullanıcı final UI kabulünde; J1/Faz J açık.

## 2026-10-08 Faz J2 · yedek rotasyonu hata güvenliği

- [x] Save yenilemesinde bilinen `.bak`, ana `File.Replace` başarılı olmadan silinmiyor. Birincil dosya değiştirme ve `.bak` rotasyonu benzersiz staging dosyalarıyla sıralanıyor; desteklenmeyen atomik değiştirme için aynı disk taşıma/geri koyma yolu var.
- [x] `SaveAtomicityTests` başarısız birincil değiştirmede mevcut ana kayıt ve bilinen iyi yedek baytlarının korunduğunu kapsıyor.
- [x] `python Tools/compile_check.py`: 12 üretim + EditMode assembly, 13/13 başarılı ve 0 hata. Bu yalnız kaynak derlemesidir; testler yürütülmedi.
- [ ] Unity EditMode testi ve platform kesinti/geri yükleme kabulü kullanıcı final aşamasında; J2/Faz J açık.

## 2026-10-08 Faz J3 · tanı paketi anahtar maskeleme

- [x] `RuntimeDiagnostics.Sanitize` boşluklu `API key`, `access token`, `client secret` ve benzer JSON alanlarını da maskeliyor; `DiagnosticsBundleTests` regresyon girdileri güncellendi.
- [x] `python Tools/compile_check.py`: 12 üretim + EditMode assembly, 13/13 başarılı ve 0 hata. Bu yalnız kaynak derlemesidir; testler yürütülmedi.
- [ ] Unity testleri ve WebGL tanı dışa aktarma kabulü kullanıcı final aşamasında; J3/Faz J açık.

## 2026-10-08 Faz J4 · kaynak sanat ve ses tetik kapsamı

- [x] Güncel runtime sanat envanteri: 738/738 bina durumu atlas/final kaynak, 44/44 dünya özelliği çözülüyor; placeholder, fallback ve eksik eşleşme yok.
- [x] Karakter statik denetimi 2.304 poza ve geometriye, ses denetimi 61 WAV ve 61 runtime trigger'a ek olarak 6 ambiyans, 10 çağ müzik çifti ve tüm güç fallback yollarına geçti.
- [ ] Unity renderer/audio, loop geçişi, gerçek Player/WebGL ve mobil cihaz çıkışı kullanıcı final testinde; J4/Faz J açık.

## 2026-10-08 Faz J2 · eski kayıt fikstürü regresyon kaynağı

- [x] Depodaki `Tests/SaveFixtures/format_1.pxgn` (oyun 1.0, Diagnostics, 256×256, yıl 0, 115 nüfus) için `LegacySaveFixtureTests` eklendi. Test gerçek ikili fikstürü bugünkü `SaveSystem.LoadFile` yoluyla açıp dünya ölçüsünü, nüfusu ve opsiyonel senaryo bölümü bulunmayan sandbox varsayılanını denetliyor.
- [x] `PolicySaveMigrationTests` ile diplomasi/politika bölümünün eski 1–8 alt biçim okuyucuları için tarihsel alan sıralı ikili örnekler kuruldu; tümünde önceki beş politika alanı korunmalı ve yeni vergi alanı %50 varsayılanına dönmeli. Mevcut biçim 9 round-trip kontrolleri `AllianceTests`/`GovernorTests` içinde zaten bulunuyor.
- [x] `HistoryMetadataMigrationTests`, meta kayıt yan bloğunun eski v1 düzenini sonradan eklenmiş biyografi ve dünya özeti bölümleri olmadan okuma kaynağı ekledi; mevcut v2 regresyonu `WorldSummaryTests` içinde.
- [x] `DeterminismTests.InterruptedTemporaryWriteDoesNotReplaceTheLastCommittedSave`, yarım yazılmış `.tmp` kalıntısının en son tamamlanmış asıl kaydın yüklenmesini engellemediğini denetleyecek kaynak olarak eklendi. Mevcut bozuk ana kayıt/`.bak` kurtarma testi de aynı fixture sınıfında.
- [x] Gerçek fikstür ikili olarak okundu: dış kayıt biçimi 1; zorunlu 1–14 bölümleri var, 15–19 (vali, politika, icatlar, senaryo, dünya tarifi) yok. Bunlar okuyucuda opsiyonel. `UnitSave` hastalık/gen/fenotip/keşif tablolarını dosya sonundaki opsiyonel kuyruk olarak ele alıyor; `NatureSave` ayrı bir iç sürüm numarası taşımıyor. Depoda bu eski Unit/Nature şekilleri için yalnız bu tek gerçek ikili fikstür bulundu.
- [x] Test, repo kökünü `Application.dataPath` üzerinden buluyor; Unity `.meta` dosyası eklendi. Fikstür yalnız okunuyor, slotlara yazmıyor.
- [ ] Unity derlemesi/EditMode çalıştırılmadı; gerçek `.bak` kurtarma ve yarım `.tmp` senaryoları için kaynak testler var ama çalıştırılmadı. İkinci/önceki ürün sürümlerinden gerçek save dosyaları depoda yok; daha eski Unit/Nature sürümleri için doğrulama fikstürü bekleniyor. J2/Faz J kabul edilmedi.

## 2026-10-08 Faz J3 · tanı paketinde profil izin listesi

- Günlük rollover düzeltildi: UTF-8 byte bütçesine göre son tam satırlar tutuluyor ve yeni satırla birlikte dosya en fazla 512 KiB kalıyor; dosyanın mevcut tail'i de en fazla 512 KiB okunuyor. DiagnosticsBundleTests için çok baytlı UTF-8 rollover regresyon kaynağı eklendi.
- Statik inceleme, önceki maskelemenin eşittir biçimini yakalarken JSON apiKey/password anahtar-değer çiftlerinde değeri açık bırakabildiğini gösterdi. JSON dizge değerleri de artık maskeleniyor; DiagnosticsBundleTests bu iki biçimi de kapsayacak şekilde genişletildi.

- [x] `RuntimeDiagnostics.BuildBundle` profilin yalnız allowlist ayarlarını yayımlıyor; yeni `DiagnosticsBundleTests.BundleExportsOnlyAllowlistedProfileSettings` kaynak testi hassas keşif, başarımlar, favori/son güçler, krallık izlemeleri ve sayaçların pakete girmediğini, dil/performans/FPS/ölçek değerlerinin normalize edildiğini doğrulayacak.
- [ ] Test çalıştırılmadı; gerçek panoya kopyalama, WebGL hata callback'i ve masaüstü yerel log sınır davranışı kullanıcı final aşamasında. J3/Faz J açık.

## 2026-10-08 Faz F2e · otomatik taşkının nehir ovasına yerleşmesi

- [x] `dis.flood` afetinin merkezi, `River` işaretine altı karo içinde bulunan seviye 3–4 su dışı araziyi hedefliyor. 256 seeded adaydan sonra seeded döngüsel tarama uygun alanı kaçırmıyor; nehir ovası yoksa eski rastgele kara seçimine düşüyor.
- [x] `ClimateAgricultureTests.AutomaticFloodPlacementTargetsLowlandNearARiver` regresyon kaynağı yakın nehir ovası ve nehir işareti olmayan durumu kapsıyor. Karar #184, Faz F belgesi ve changelog kaydedildi.
- [ ] Unity derleme/EditMode, afet güç efekti, çok tohumlu afet sıklığı ve ekonomi/şehir etkisi çalıştırılmadı; kullanıcı final test aşamasında. F2/Faz F açık.

## 2026-10-08 Faz F2d · geniş nehir ve köprü

- [x] `tile.river` içerik tanımı eklendi; p1 dünya üreticisi JSON kataloğunu güncelledi. Yüksek akış kanalı akış yönüne dik genişliyor; normal akış dar ve yürünebilir nehir olarak kalıyor.
- [x] Yol fırçası yalnız geniş nehir suyunda köprü kuruyor. Köprü ekleme/kaldırma `Walkable` hareket bayrağını değiştiriyor, region chunk'ını kirletiyor ve harita flag kaydı üzerinden saklanıyor. Render, köprü rengini su gölgelemesinin üstünde çiziyor.
- [x] Save/load incelemesi `RebuildDerivedData` aşamasında su türünün `Walkable` bitini kaldırdığını gösterdi. Türetilmiş bayrak kurulumu kayıtlı `River+Road` su karesinde köprü geçişini yeniden açacak şekilde düzeltildi.
- [x] `RegionSplitTests` nehirin kara adalarını ayırmasını, köprünün birleştirmesini, kaldırılınca ayrımın geri gelmesini kapsıyor; ayrı save/load regresyon kaynağı köprü bayraklarını ve yeniden kurulan kara region bağlantısını denetliyor. F2 uygulama planı güncellendi; karar #183, Faz F belgesi ve changelog mevcut.
- [ ] Yalnızca içerik üreticisi çalıştırıldı. Unity derleme/EditMode, aynı seed harita karması, save/load, görsel kabul, çok tohum, performans ve 100+ yıllık köprü/şehir testi çalıştırılmadı; kullanıcı final test aşamasında. F2/Faz F açık.

## 2026-10-07 Faz H4 · krallık izleme

- [x] Profil, izlemeleri dünya tohumu + harita boyutu + krallık UID ile saklıyor; en fazla 20 izleme var, eski profiller boş listeyle yükleniyor.
- [x] Krallık penceresine izlemeyi aç/kapat kontrolü eklendi; izlenen krallıkla bağlantılı kronik olayları önem eşiğinden bağımsız bildirme politikası bağlandı.
- [x] Türkçe/İngilizce metin üretildi (`602` UI girdisi); profil kapsamı ve izlenen olay bildirimi için regresyon testi kaynağı eklendi.
- [ ] Unity derleme/testi çalıştırılmadı. Tür koruma bölgesi ve H4 performans/maliyet kabulü açık; Faz H kabul edilmedi.

## 2026-10-07 hata düzeltmeleri ve Faz H4 geri alma kaynak dilimi

- [x] `History.cs` içine `PG.Core` ve `System` ad alanları eklendi; bu, `EntityId` ve `Math` derleme hatalarını giderir.
- [x] Derleme hata listesinde bildirilen kaynak sorunları kök nedenine göre düzeltildi: `Settlement` yerel değişken kapsamı; senaryo üreticisinde `PG.Core`; `UnitLifeSystems` içinde `System.Array`; `CityEconomySystem` doğru tür adı; byte `FeatureAquatic` bayrağında `== 0`; göç mesafesinin `float` tipi.
- [x] Sonraki Unity hata çıktısındaki `SimRandom.Derive` argüman türü uyuşmazlığı, senaryo akış kimliğini imzanın `int index, long tick` parametrelerine uydurarak giderildi.
- [x] `p0_ui.py` ve içerik kataloğu çalıştırılarak Undo TR/EN anahtarları UI JSON'a üretildi (`600` UI girdisi).
- [x] Undo entegrasyonu için snapshot geri yükleme/kullanım testi kaynağı eklendi; Unity testi çalıştırılmadı.
- [x] Faz H4 kararı ve değişiklik günlüğü güncellendi. Undo kaynak dilimi tamamlandı; tür koruma bölgesi, krallık izleme, maliyet ölçümü ve final Unity doğrulaması hâlâ açık.
- [ ] Kullanıcı testleri son aşamaya bırakma talimatı nedeniyle Unity derlemesi ve testleri çalıştırılmadı; bildirilen tüm hata satırlarının düzeldiği henüz derleyici çıktısıyla doğrulanmadı.

## 2026-10-07 yol haritası F fazı · F4a yükseklik/rüzgâr iklimi

- [x] Continuous relief veya tile seviye yüksekliği, seed'e bağlı başlangıç rüzgârı yönünde örneklenerek orografik nem artışı ve lee-side rain shadow üretir.
- [x] `NatureState` ile aynı derived stream kullanılarak iklim rüzgârı ilk bulut hareketiyle eşleştirildi; worldgen ana PRNG akışı kaydırılmıyor.
- [x] Continuous elevation sıcaklık kuşağına katıldı; tile nem alanı biyom seçiminde ve zone başlangıç moisture ortalamasında kullanılıyor.
- [x] Rüzgâr yönü ve generated zone nemi için EditMode regresyon kaynakları yazıldı.
- [ ] Test, derleme, çok tohumlu biyom dengesi, yağış/kuraklık soaku ve üretim performansı kullanıcı testleri son aşamaya bırakıldığı için çalıştırılmadı. F4/Faz F kabul edilmedi.

## 2026-10-07 yol haritası F fazı · F3a hafif hidrolik erozyon

- [x] Sınıflandırma öncesi sürekli yükseklikten, harita kenarlarına drenaj yapan mevcut priority-flood işiyle akış ebeveyni ve birikimi kuruldu.
- [x] Yeni Burst erozyon işi şablon kuvvetine göre yüksek akış kanallarını oyuyor ve düşük eğimli devamında sınırlı tortu biriktiriyor; düz/boş/görsel şablonlarda kapalı.
- [x] Yüzdelik sınıflandırma sırası ve seed bağımsız ek RNG kullanılmaması korundu; test kaynağı deterministik sonuç, yüksek akışlı kanal değişimi, düşük akış değişmezliği ve sınırları kapsıyor.
- [ ] Unity testi/derleme, 256×256 süre bütçesi ve görsel galeri kullanıcı testleri son aşamaya bırakıldığı için yapılmadı. Kıyı yumuşatma, deltaların görsel kabulü ve F2 kabulü açık; Faz F tamamlanmadı.

## 2026-10-07 yol haritası F fazı · F3b kıyı yüksekliği yumuşatma

- [x] Ön sınıflandırma kara/su maskesinden kıyı hücreleri bulunuyor; yükseklikleri yerel 8 komşu ortalamasına şablon erozyon kuvvetine bağlı düşük oranda harmanlanıyor.
- [x] Son yükseklik histogramı, yüzdelik eşikleri ve zemin sınıflandırması tekrar hesaplanıyor; hedef kara oranı son alan üzerinden korunuyor.
- [x] Deterministik kıyı harmanlama ve kıyıdan uzaktaki hücrelerin değişmemesi için test kaynağı yazıldı.
- [ ] Unity testi/derleme, gerçek harita görseli ve süre ölçümü çalıştırılmadı. Deltaların görsel kabulü ve F2/Faz F işleri açık.

## 2026-10-07 yol haritası F fazı · F1a sırtlar ve F2a hidroloji kaynağı

- [x] Yükseklik gürültüsüne şablon kontrollü sırt etkisi; önceki F1a kaydı için kaynak ve kullanıcı-son-test sınırı F belgesinde.
- [x] Sürekli yükseklik alanından deterministic priority-flood dolumu, suya giden akış ebeveynleri, flow accumulation ve dar sığ nehir işaretleri eklendi. Kapalı çukurların sığ göle dönüşmesi harita alanının yaklaşık %1'iyle sınırlandı.
- [x] Nehir yüzey kodu/shader detayı eklendi; river bayrağı tür maskesi dışında ve yürünebilir kaldı, arazi değişiminde temizleniyor. Kayıt biçimi değişmedi; mevcut ushort bayrağı zaten kaydediliyor.
- [x] F1/F2 için determinism ve üretilmiş dünya regresyonlarının kaynakları yazıldı; istek doğrultusunda Unity testleri, derleme, soaku ve görsel kabul çalıştırılmadı.
- [x] Nehirli/komşu zone başlangıç nemine +14, nehir içeren zone aylık +1 nem kaynağı eklendi; mevcut D3 mahsul büyümesi ve hasadı bu nemden etkileniyor. Nehir kıyısı ayrı düşük kontrastlı zemin yüzeyi alıyor.
- [x] Nehir tile'ı bina ayak izinden çıkarıldı, yerleşim çekiminde kıyı kabul edildi; nehir kaldırılınca etkilenen yakın chunk'lar yenileniyor.
- [x] Başlangıç/aylık nem, yürünebilirlik ve nehir karesine bina kurulamamasını doğrulayan test kaynakları yazıldı; hiçbir Unity testi/derlemesi veya iklim soaku çalıştırılmadı.
- [ ] F2 için 20 tohumda nehir/göl dağılımı, denize ulaşma oranı, görsel galeri, üretim performansı ve medeniyet soaku son kullanıcı doğrulamasını bekliyor. Köprüler/geniş nehirler, sel ve doğal biyokütle etkisi sonraki F2 dilimleri.

## 2026-10-07 yol haritası E fazı · E1a ikili antlaşmalar

- [x] E belgesindeki teklif, ticaret ve save akışları yeniden izlendi; mevcut pazar tüccarları şehirler arası kara ticaretini yapıyordu, ancak yalnızca görüş/ittifak ilişkisine göre.
- [x] `MetaWorld` süreli ticaret/saldırmazlık antlaşmaları ve türetilen ilişki durumu ekledi. Policy save biçimi 2 antlaşma listesini saklıyor; biçim 1 eski kayıt okuması korunuyor.
- [x] AI teklifleri aynı `ProposalKind` kabul/ret yolunu kullanıyor; görüş, ortak düşman, göreli güç ve hükümdar özellikleri karar eşiğine katılıyor. Vali `ProposeTrade` / `ProposeNonAggression` eylemleri ve doğrulaması eklendi.
- [x] Saldırmazlık antlaşması savaş seçiminde/emrinde kontrol ediliyor; ticaret antlaşması düşük görüşte de mevcut pazar kervanını çalıştırıyor. Bildirim, vali durumu ve kabul ekranı teklif türünü gösteriyor.
- [x] `Data/_generator/p0_ui.py` yeniden üretildi; iki yeni TR/EN teklif metni `ui_strings.json` içinde.
- Ruling: E0 çok krallıklı soak, kullanıcının testleri en son yapma isteği nedeniyle implementasyondan önce ertelendi; E1a kapsamı mevcut ticaret/savaş işleyişini koruyan süreli antlaşmalarla sınırlandı. Denge riski çok krallıklı son soak'ta ölçülecek.
- [ ] E1a/E1b/E1c–E1e kaynak dilimleri tamamlandı; Unity derlemesi, edit/play testleri, biçim 1–9 eski save turları ve çok tohumlu diplomasi soak son kabul aşamasında. E1/Faz E açık.
- [x] E1b: `Vassalage` yönlü subject→overlord kaydı, döngü engeli ve kalıcı vasal durumu eklendi; politika kaydı biçim 3, biçim 1/2 okumayı koruyor.
- [x] Vali vassalage teklifi gönderebiliyor; AI göreli güç/görüş/hükümdar özelliğiyle kabul ediyor veya reddediyor. Vasal yılda bir defa şehir altınının %5'ini üst hükümdarın başkentine aktarır; ödeme ve vasallık tarihçe olayına eklenir.
- [x] Vasal ilişkisi krallık ilişki ekranında ve vali JSON durumunda görünüyor; vasal ile üst hükümdar birbirine savaş ilan edemiyor ve aynı ittifaka katılamıyor.
- [ ] E1b save/load, döngü, tribute ekonomi testi ve Unity/çok krallıklı soak kullanıcı isteğiyle son kabul aşamasına ertelendi; E1 barış şartı ve elçi arayüzü açık.

## 2026-10-07 yol haritası E fazı · E2a savaş planı

- [x] `war_types.json` ve üreticisine casus belli/hedef izin listeleri eklendi; içerik yüklemesi boş veya bilinmeyen tanımları reddediyor.
- [x] `WarSystem.Declare` var olan savaş türü, yakın dönem yenilgisi, dini/kültürel fark ve şehir kıtlığından nedeni belirliyor; türün izin listesine uymayan seçim verideki ilk geçerli nedene düşüyor. Hedef şehir, intikam, din ve güç dengesine göre izinli hedef seçiliyor.
- [x] Savaş planı `MetaWorld` yan listesinde; hedef şehir alınması veya 80/100 yorgunluk mevcut çift taraflı barış teklifini açıyor. Savaş sona erince ilgili bekleyen teklifler kaldırılıyor.
- [x] Policy save biçim 4 savaş planını ekledi; biçim 1–3 okuma yolları korundu. Savaş tarihçesi paketi nedeni/hedefi saklıyor ve tarih penceresi gösteriyor.
- [x] WarGoal kayıt, hedef fethi ve eski biçim 3 göçü için EditMode testleri yazıldı. Kullanıcının isteğiyle hiçbir Unity testi/derlemesi çalıştırılmadı; E2a doğrulanmış/kabul edilmiş sayılmaz.
- [x] E2b barış şartları (haraç, şehir devri, vasallık, saldırmazlık), kayıt biçimi 5 ve regresyon kaynakları daha sonra eklendi; doğrulama bekliyor (bkz. 2026-10-07 E2b bölümü).

## 2026-10-07 yol haritası A fazı · ilk altyapı dilimi

- [x] 00 genel bakış ve A fazı bu oturumda tekrar okundu; eski 15 test kırığı mevcut logla doğrulanmadığı için güncel gerçek sayılmadı. Çalışma ağacındaki önceki kullanıcı değişiklikleri ve commitlenmemiş işler korundu.
- [x] Ortak düşman artık iki bağımsız savaş kaydında aynı üçüncü krallığı paylaşan rakipleri kapsıyor. Vali arayüzünde oyuncu devri, LLM beynine bağlı olmadan kapsam dışında kalıyor; AI'a geri verince kaydedilmiş devralma listesi de temizleniyor.
- [x] Vali HTTP testinde tam `Exception` türü beklentisi kaldırıldı, bozuk 32×32 harita 64×64 oldu; icat yükleme fikstürü aynı krallık indeksini kuruyor. GPU palet testi ayrı `Graphics` kategorisi/modu.
- [x] `CivSoak` yıl sonu nüfus, tür dağılımı, yaşayan krallık, şehir, savaş, kıtlık, eşya üretimi ve tick sürelerini JSON raporuna yazıyor. `civ_ab.ps1` senaryo/yıl/tohum parametrelerini aktarabiliyor; `metrics_compare.py` raporları kıyaslıyor.
- [x] A4 `gate.ps1` ve başlangıç eşiği JSON'u kaynakta hazır; kalite kapısı EditMode+Graphics, diagnostics, PlaySmoke, çok tohumlu çok krallık soak ve PerfBench sonuçlarını koşul bazında doğruluyor.
- [x] A5 aynı seed ve komut determinizmi için 1200 tick testi, kayıt-yükleme-sonrası 120 tick devam karması testi eklendi.
- [ ] A1–A5 Unity Test Runner/Graphics/soak/quality gate çalıştırmaları kullanıcı testleri en son isteği nedeniyle bekliyor; JSON çıktısının gerçek koşuda şeması ve değerleri henüz doğrulanmadı.
- [ ] A6 ve gerçek metrik taban çizgisi açık. Commit kullanıcı kararı olmadan yapılmayacak. A tamamlandı sayılmıyor; README'de tüm fazlar açık kalıyor.

## 2026-10-07 yol haritası B fazı · ekonomi teşhis ve büyüme dilimi

- [x] B fazı belgesi ve şehir ekonomisi/planlayıcı/yerleşim/doğum yolları incelendi. `StatLost`, kaynak eklenirken depo sınırını aşan kısmı sayıyor; taşıma veya bozulma ölçüsü değil. Beş tohumlu tanı kullanıcı testleri sonuna bırakıldığı için çalıştırılmadı.
- [x] `SoakMetrics` JSON'una şehir başına konut/işsizlik/işler/stoklar/bölge ve salon durumu ile planlayıcının en öncelikli bina için alan/kaynak durumu eklendi.
- [x] Planlayıcının 64 rastgele örnek sonrası tüm sahip olunan bölgelerde kesin alan taraması, salon yükseltmesi için genişleyen arama yarıçapı ve herhangi bir adayda yer yoksa bölge genişletme geri dönüşü eklendi.
- [x] Konut talebi büyüme payı eklenerek öne alındı; şehir doğum olasılığı konut ve yiyecek yeterliliğine bağlandı; oduncu/taşçı kotası stok açığına göre büyütüldü.
- [x] Yiyecek akışı için şehir başına üretim/tüketim/bozulma/depo taşması sayaçları JSON raporuna bağlandı. `StatLost` artık belgede doğru şekilde yalnızca depoya sığmayan üretim olarak tanımlı.
- [x] Demirhane ilk üretim için odun/kemik stoğuyla da planlanabiliyor; göçmen yollama kalabalık ve büyük kent koşullarına ek olarak yeterli yiyecek ile yüksek işsizliği de değerlendiriyor.
- [x] Gıda JSON alanları depoya ekleme, stoktan/tasırken tüketim, bozulma, taşma ve stok farkını ayrı gösteriyor; dönüşüm/ithalat nedeniyle kapalı bilanço olduğu iddia edilmiyor.
- [x] Tüccar krallık altın artışı vergi hesabına bağlandı; kervanlar yalnız alıcının ihtiyacı kadar ve ödeme gücü kapsamında kaynak götürüyor, alıcıya altın gideri ekleniyor.
- [x] Araştırma üretimi krallık nüfusu ile şehir bilginlerinin emeğini içeriyor. B7–B8 kararları `DECISIONS.md`'ye, faz günlüğü B belgesine yazıldı.
- [ ] Kaynaklar Unity'de derlenmedi ve test/soak çalıştırılmadı. B0 darboğazları, çok tohumlu denge ve kabul kriterleri doğrulanmadan B fazı tamamlanamaz.

## 2026-10-07 yol haritası C fazı · besin ağı verisi

- [x] C0 kaynak taraması: türlerde diyet/habitat zaten var; açık av listesi, besin ihtiyacı, trofik seviye ve vahşi ekoloji baseline yok. Wild 200 yıllık soak ve 6 tohum kullanıcı testleri sonuna bırakıldı.
- [x] C1 üretici, şema ve `SpeciesDef` yeni alanlarla genişletildi. `UnitWorld.Prey` ve şehir avcısı hedefleme açık av listelerini tüketiyor; generator 92 türü yeniden üretti.
- [ ] İçerik/şema doğrulaması, EditMode testleri ve C0 baseline çalıştırılmadı; C1 henüz doğrulanmadı, C fazı kabul edilmedi.

## 2026-10-07 C2 · yerel bitki biyokütlesi

- [x] `TileTables.FeatureFoodValue` feature verisinden, `BiomeFoodValue` doğal otlaklardan besin değeri üretir; `ZoneData.FoodBiomass` 8×8 bölgede ikisinin kalan stoğunu tutar. Feature/otlak tüketimi, örneklemeli yenilenme, biyom/zemin değişimi ve kayıt yükleme sayaçlara bağlandı; kayıt formatı değişmedi.
- [x] Şehirli olmayan otçul türlerin aylık doğurganlığı, kendi zone'undaki biyokütle ve tür `foodNeed` alanıyla çarpılır (`foodNeed × 32` doygunluk eşiği). Global nüfus tavanı değişmedi.
- [x] Otçullar tükenmemiş besin feature'larını hedefler ve yedikçe kaynaklarını azaltır; doğa örneklemesi yenebilir kaynakları yeniden büyütür.
- [x] Feature bulunmayan uygun biyom karoları sınırlı otlak stoğu sağlar; stoğu var olmayan karoda otlama seçilmez ve kaynak değeri mevcut `FeatureState` kayıt alanında saklanır.
- [ ] Carrying-capacity regresyonları, veri doğrulama ve vahşi dünya soaku kullanıcı testlerinin son aşamasına ertelendi; formül kalibrasyonu ve aşırı otlatma geri beslemesi bekliyor.

## 2026-10-07 C3 · yerel avcı baskısı

- [x] `UnitWorld.CountPreyNear` C1 whitelist'indeki canlıları `SpatialIndex` üzerinden 16 tile yarıçapta sayar; tam dünya taraması yok.
- [x] Carn/Blood/Brains üreme şansı yerel av sayısına, Omni daha hafif biçimde av varlığına bağlı. Aç birim daha geniş görüş ve 8–28 tile prowl alanı kullanır; mevcut açlık hasarı/ölüm zinciri korunur.
- [ ] Gecikmeli avcı–av dalgalanması, testler, performans maliyeti ve 200 yıllık çok tohumlu wild soak henüz ölçülmedi; C2/C3 ve C fazı kabul edilmedi.

## 2026-10-07 C4 · göç ve yayılma

- [x] `UnitWorld` uzamsal indeksiyle tür ve av sayısını zone bazında alıyor; tam harita taraması yok.
- [x] Aç/kalabalık vahşi yetişkinler düşük sıklıkla sekiz komşu zone'daki gıda avantajını değerlendiriyor. Otçul bitki biyokütlesi, etçil yerel av, hepçil karma puan kullanıyor; hedefteki hemcins yoğunluğu seçime katılıyor.
- [x] Hedef kare yürünebilirlik ve kara/su ada bağlantısından geçiyor; uygun yüzücü/amfibi/uçan hareket sınıflarına izin veriliyor. Mevcut `Migrate`/`Dest` hareketi kullanıldığından kayıt biçimi genişlemedi.
- [ ] Göç davranış testleri, adalar arası senaryolar, deterministik soak ve 200 yıllık çok tohumlu wild baseline son doğrulama aşamasına bırakıldı. C4/C fazı kabul edilmedi.

## 2026-10-07 C5 · tükenme ve yeniden yerleşme

- [x] İlk kez alt tür kaydı oluşmuş türler izleniyor; nüfus iki yıl üst üste 2 veya altındaysa tarihçede tehlike kaydı, sıfıra düşerse tükenme kaydı oluşturuluyor. Hiç yaşamamış türler yanlışlıkla tükenmiş işaretlenmiyor.
- [x] Doğal yıllık hayvan ekleme tükenmiş türleri atlıyor; oyuncunun güçle yaptığı spawn etkilenmiyor. Tarih budaması tür tehlikesi/tükenme kayıtlarını kalıcı tutuyor; yeni kayıt bölümü eklenmedi.
- [ ] Sınır koşulları, yükleme/kayıt, doğal spawn engeli, UI çevirisi ve uzun süreli popülasyon davranışı test edilmedi; C5/C fazı kabul edilmedi.

## 2026-10-07 C6 · hastalık ve yoğunluk

- [x] Temas hastalığında bulaşan birimin 4 tile çevresindeki nüfus `SpatialIndex` sorgusuyla ek çarpan olarak kullanılıyor; düşük yoğunlukta 0.75, 12+ komşuda 2.0.
- [x] Var olan tek temas/ay sınırı, hastalık direnci, bataklık ve şehir sağlık/nüfus katsayıları korunuyor; aktif hasta başına dar komşu sorgusu dışında genel tarama yok.
- [ ] Hastalık verisi sınırları, nüfus yoğunluğu A/B testi, kalabalık salgın simülasyonu ve performans ölçümü çalıştırılmadı; C6/C fazı kabul edilmedi.

## 2026-10-07 C7 · çürüme ve besin döngüsü

- [x] İki yıl sonra yok olan yenmemiş ceset, iki tile içindeki en verimli yenebilir bitki veya doğal otlak karesine 8 kaynak puanı ekliyor. Artış mevcut `SetFeatureResource` yolundan geçtiği için zone biyokütlesi ve kayıt işaretleri güncelleniyor.
- [x] Var olan etçil/hepçil corpse arama ve yeme yolu gövdeyi tüketiyor; tüketilen gövde ayrıca gübre katkısı yapmıyor. Ceset kalırsa mevcut kemik yığını davranışı korunuyor.
- [ ] Yenme/çürüme karşılaştırması, resource sayaçları, besin katkısı ve uzun dönem büyüme test edilmedi; C7/C fazı kabul edilmedi.

## 2026-10-07 C8 · insan ve doğa etkileşimi

- [x] Mevcut avcılar vahşi av birimlerini öldürüyor; C3'ün açık av listesi ve yerel av sayımı, yerel baskıyı doğurganlığa taşıyor. Oduncu ağaç kaynağını azaltıyor; C2 biyokütle/feature sayaçları ve doğa büyümesi aynı kaynağı kullanıyor.
- [x] Balıkçı kulübesi artık 12 tile içindeki en yakın balık verimli su bitkisinden tek kaynak tüketip 3 balık veriyor; yakın doğal kaynak yoksa sonsuz balık üretmiyor. Kaynak yenilenmesi mevcut doğa örneklemesiyle sürüyor.
- [ ] Av/odun/balık aşırı kullanımı, doğal yenilenme, şehir ekonomisi ve 200 yıllık çok tohumlu denge test edilmedi; C8/C fazı kabul edilmedi.

## 2026-10-07 D0–D1 · mevsim döngüsü

- [x] D0 doğrulaması: sıcaklık sistemi ay numarasından enleme göre ±10°C sinusoidal mevsim eki zaten veriyordu; ama tarım, doğa büyümesi, hayvan açlığı/doğurganlığı, hareket ve bozulma ortak mevsim state'ini kullanmıyordu. Yağmur damlaları bitki büyümesini tetikliyor; zon nem deposu yok. Şehir buğdayı her ay sabit bir evre ilerliyordu.
- [x] `Data/season_rules.json` ile dört mevsimin sıcaklık genliği ve bitki/tarım büyümesi, doğurganlık, açlık, hareket, bozulma çarpanları ayarlanabilir hale geldi. `law.seasons` şiddeti 0–2 aralığında düzenliyor; sonsuz yaz/kış mevcut sıcaklık kuralıyla uyumlu.
- [x] `SeasonSystem` her tick mevcut saatten mevsim state'i üretir; mevsim doğa büyümesi, crop evreleri, tür açlığı/doğurganlığı, yürüme hızı ve şehir yiyecek bozulmasına bağlandı. Durum saatten türediği için yeni kayıt alanı yok.
- [ ] Mevsim geçişi, yoğunluk kaydırıcısı, crop hasadı, kayıt/yükleme ve ekonomi dengesi testleri en son doğrulama aşamasına bırakıldı; D1/D fazı kabul edilmedi.

## 2026-10-05 WebGL yapay zekâ uç noktası

- [x] Salt okunur URL kökü bulundu. URL/model ve bulut/yerel profile göre oturum anahtarı düzenlenebilir ve kaydedilebilir hale geldi.
- [x] Browser UnityWebRequest + ana iş parçacığında async AI/test yolu; kapalı özelliği açınca etkinleşen brain; WebGL persistent settings flush. API anahtarı JsonIgnore ve ancak Authorization başlığında kullanılır.
- [x] UI çevirileri üretildi, referans doğrulaması 0 hata; C# 12 üretim assembly'si ve yeni test kaynakları derleme kontrolü geçti.
- [ ] Unity testleri/WebGL build + tarayıcı servis doğrulaması. Editor hâlâ Terms penceresinde; yeni WebGL derleme mevcut ZIP'te değil. Kullanıcının sağlayıcısı CORS izni vermelidir.

## 2026-10-04 dört halkın görsel geliştirmesi

- [x] Render/sanat taraması, öncelikler ve kabul ölçütleri: `Docs/Visual_Upgrade_2026-10-04.md`.
- [x] İnsan, ork, cüce, elf için dört özgün sayfa, 96 poz; PNG + poz metadata'sı ve eski görsele fallback. Krallık renkleri/yaş/yön mevcut toplu mesh üzerinden sürer.
- [x] Sim tick'ine bağlı animasyon, gerçek yer değişimiyle yürüyüş, tek saldırı çevrimi ve hafif nefes; simülasyon/kayıt biçimi değişmedi.
- [x] 96 poz/geometri sayısal denetimi; 12 üretim assembly'si ve yeni test kaynağı hatasız derlendi. Tarayıcı varlık önizlemesi açıldı.
- [ ] Unity EditMode testlerini yürüt, gerçek dünyada ölçek/yön/renk/yaş/pause kontrolü yap; WebGL ve hosting paketini yeniden üret. 2026-10-05 tekrarında `audit_unit_art.py` 96 pozu geçti, `compile_check.py` 12/12 assembly derledi ve `git diff --check` temiz kaldı. Unity GUI lisansı/koşulları çözüldü; ancak `run_unity.ps1 -Mode EditMode -Tag unit-visual-step1` headless lisans istemcisinde `Access token is unavailable`, `Found 0 entitlement groups`, çıkış 198 ile durdu; Test Runner XML'i ve yeni WebGL build'i oluşmadı. Gerçek oyun görsel kontrolü henüz yapılmadı.
- [ ] Meslek/ekipman ve çalışma animasyonları; ölüm pozlarını ceset çizimine bağlama; sonra ırka özgü binalar ve doğa (rapordaki sıra).

## 2026-10-05 meslek ve ekipman görünümü — 2. adım

- [x] Sunum katmanı gerçek `job.*` ve takılı `eq.*` kimliklerini okur: çiftçi/tırpan, oduncu/balta, madenci/kazma, inşaatçı/çekiç; savaşçı silahı ekipman tipinden seçer.
- [x] Küçük indeksli alet/silah çizimleri mevcut birim atlasına ve tek birim mesh'ine eklendi; oyun simülasyonu ve kayıt biçimi değişmedi.
- [x] İnsan/ork/cüce/elf saldırı pozu yalnızca çizimdeki ırk silahı takılı olduğunda kullanılır. Boş elde silahlı poz yerine yumruk işareti; farklı silahlarda doğru ekipman çizimi kullanılır.
- [x] `UnitAppearanceTests.cs` eklendi; 12 üretim assembly'si ve EditMode test assembly'si çevrimdışı derlemeden geçti; `git diff --check` temiz.
- [ ] Unity EditMode testlerini ve oyundaki alet yerleşimini doğrula. Headless EditMode testi bu makinedeki lisans istemcisi nedeniyle çıkış 198 ile durdu; bu nedenle Unity testleri çalışmış sayılmaz. Kullanıcıya oyun içi kontrol için henüz paket/build sunulmadı.

## 2026-10-04 özgün ses paketi

- [x] 61 özgün WAV üretildi: 35 kısa efekt, altı çevre döngüsü, on çağın sakin/gergin müzik çiftleri. `Audio/` toplam 28,4 MiB. Üretici `Tools/generate_audio.py`; kısa seçki `Logs/audio-preview.wav`.
- [x] `Data/_generator/p6_audio.py` mevcut kimlikleri dosyalara bağlar; sesler/veri/manifest `Assets/StreamingAssets` içine eşlendi.
- [x] AudioManager'da yüklenen klibin devam eden ortam/müzik kaynağına geçmemesi düzeltildi; yüklenen döngü sessiz bekler; kısa efektler sıra ile önceden yüklenir. Dünya bırakıldığında müzik sessizleşir.
- [x] WAV denetimi 61/61: dosya referansı, biçim, enerji, tepe seviyesi, DC, uç/döngü sınırı ve on müzik çiftinin süresi. İçerik doğrulaması 0 hata; katalog üretildi.
- [x] Son kaynaklarla çevrimdışı C# derlemesi: 12 üretim assembly'si ve `AudioLoadingTests.cs` 0 hatayla derlendi. Bu sonuç Unity testlerinin çalıştığı anlamına gelmez. `git diff --check` temiz.
- [ ] Unity ses regresyon testleri ve WebGL build: ilk batch denemesi `No valid Unity Editor license found`, exit 198. Lisans hizmetine erişimli tekrar denemesi IPC/lisans başlatmada kaldı ve yalnız bu test süreci durduruldu. Mevcut `Unity Editor Software Terms` penceresi kullanıcıya bırakıldı. Henüz gerçek oyun içi dinleme veya yeni WebGL yayın doğrulaması yok; önceki ZIP değişmedi.

## 2026-10-03 harita tıklama ayrıntıları

- [x] Üst çubuktaki anlık hover konum satırı kaldırıldı. Haritada 10 px içinde biten kısa tıklama tek bir pencereyi açar/günceller; koordinat, arazi, biyom, sıcaklık ve varsa birim/şehir bağlantılarını gösterir.
- [x] Fırça/güç, UI tıklaması ve harita sürüklemesi ayrıntı penceresi açmaz. Mevcut “İncele” modu birim/şehir pencerelerini doğrudan açmaya devam eder.
- [ ] Tarayıcı kaydı Firebase bulutuna bağlı değil: `SaveSystem` `Application.persistentDataPath/saves` kullanıyor; WebGL'de bu IndexedDB'dir. Bulut senkronizasyonu ayrı özellik olarak yok.
- [ ] WebGL build ve `Builds/WebSite` paket güncellemesi: batch build lisans doğrulamasında exit 198 ile durdu. Lisanslı UI denemesi `Unity Editor Software Terms` ekranında bekliyor; kullanıcı şartları kabul edip geri bildirdikten sonra derleme/önizleme/paketleme tamamlanmalı. Şimdilik `Builds/WebSite` ve web ZIP'i yeni C# davranışını içermiyor.

## 2026-09-27 savan ve diğer ağaç görselleri

- [x] Savan akasyasının eski 5×7 yuvarlak yer tutucuyu kullandığı saptandı. `kind: tree` olan 24 türün tamamına ayrı saydam `_color.png` eklendi; kaynaklar `Art/Final/features/`, oyuna yüklenen kopyalar `Assets/StreamingAssets/Art/Final/features/` altında. Meşe/çam ve kaya korunuyor.
- [x] Ağaç galerisi ve savan/orman karşılaştırması gerçek URP kamerasında alındı: `Logs/all-trees-closeup.png`, `Logs/savanna-trees-closeup.png`. Görseller #91'deki en fazla 1 karo genişliğinde mesh parçalarına bölünerek çizilir.
- [x] `Tools/audit_tree_art.py`: 24/24 kaynak var ve saydam. Unity EditMode 19/19 (`Logs/all-tree-art-tests.xml`), çevrimdışı derleme 11/11, Diagnostics 1000 tick determinizm ve kayıt turu başarılı (`Logs/all-tree-diagnostics.log`).
- [ ] Geçici Unity proje kopyasında PlaySmoke 400 kare, UI ve kayıt/yükleme adımlarına erişti; UnityEditor.Search indeksleyicisi bir `ArgumentOutOfRangeException` kaydettiğinden koşu temiz geçmedi (`Logs/all-tree-playsmoke.log`). Ana proje kullanıcı editöründe açık olduğundan aynı koşu ana projede tekrar edilmedi.

## 2026-09-27 köy grafik geliştirmesi

- [x] Beş yeni özgün görsel: köy merkezi, kuyu, değirmen, ev inşaatı, ev harabesi. Son ikisi kulübeye de uygulanır. Kaynak ve promptlar `Art/Final/` altında.
- [x] `BuildingRenderer`: tek ek katmanda hafif zemin gölgesi; bina başına 25 vertex; değişiklik olunca güncellenir; Bind/Unbind ve ölü bina temizliği testli.
- [x] `Preview Village Graphics`: sabit seed, yeni kaydedilmeyen dünyada sekiz yapı; gerçek URP yakın/uzak kamera çıktısı. `Logs/village-closeup.png`, `Logs/village-in-game.png`.
- [x] Derleme 11/11; Unity EditMode **13/13**; 1000 tick determinizm; kayıt turu; eski kayıt fixture'ı; tüm Diagnostics **ALL OK**. Korunan 149 çekirdek/sim/dünya/güç/kayıt/içerik kaynağında 0 değişiklik.
- [x] Kanıtlar ve ölçüm sınırları: `Docs/Visual_Pass_2026-09-27.md`. Uzun süreli FPS/GC ölçümü yapılmadı; tüm diğer stiller yeniden çizilmiş sayılmaz.

## 2026-09-27 tek ev modeli denemesi

- [x] Tek özgün kiremit çatılı, açık duvarlı, kapı/pencere/bacası belirgin ev üretildi. Kaynaklar `Art/Final/buildings/human_{house,hut}_complete_color.png`; üretim promptu ve kullanım notu `Art/Final/README.md`.
- [x] `SpriteLibrary` `_color.png` dosyalarını atlas öncesinde en fazla 64 px nokta örnekler; `BuildingRenderer` UV1.y ile `PG_Unit` shader'ında özgün rengi korur. Aynı model insan evi ve erken kulübede kullanılır. Diğer yapılar, inşaat/harabe görselleri ve simülasyon ayak izleri korunur.
- [x] `HouseArtTests`: gerçek atlas yükleme, doğru durum/stil seçimi, kırmızı çatı/mavi pencere ve alpha kontrolleri — Unity EditMode 1/1 geçti (`Logs/house-tests.xml`).
- [x] C# assembly derlemesi 11/11 hatasız. Unity gerçek oyun kamerasında ev/kulübe ve eski inşaat/harabe birlikte çizildi. `HousePreview` menüsü yeni kaydedilmeyen Play Mode dünyasında aynı karşılaştırmayı açar; kamera çıktıları `Logs/house-in-game.png`, `Logs/house-closeup.png`.
- Not: İlk sandbox Unity çalıştırması lisans IPC bağlantısında kaldı; yerel çalıştırma ile test geçti. Batch Game View `ScreenCapture` çıktı vermediğinden gerçek URP kamera `StandardRequest` render hedefiyle görüntülendi. Ekran görüntüleri oyun dünyasıdır; HUD dahil değildir.

## 2026-09-27 devam planı

Kullanıcının eklediği oturum metni ve mevcut dosyalar esas alınmıştır. "Geri kalanını sen hallet" talimatı mevcut tasarımı sürdürme yetkisidir. Önceki değişiklikler korunur; bu oturumda doğrulanmayan eski test sonuçları yeni sonuç olarak sunulmaz.

- Faz 1–9: önceki uygulamalar mevcut. Faz 8 kurt/izolasyon ve zombi dengesi açık kabul konularıdır; tamamlandı varsayılmaz.
- Faz 10 başlangıcı: komutlar, profil, birim/meta pencereleri var; kabuğa bağlı değiller. İlk derleme 6 hatayla başarısız.
- [ ] 1. Eksik trait/gen/eşya, tarihçe, kanun ve meta kontrol pencerelerini tamamla. Hedefler: UI/Windows, Powers/EditCommands. Kabul: derleme, komutla düzenleme ve bozuk hedef testleri.
- [ ] 2. UiRoot'a pencere yöneticisi, inceleme, takip, possession, araç menüsü bağla. HUD arama/favori, profil ve pencere yaşam döngüsünü tamamla. Kabul: gerçek PlayMode aç/kapat, dünya değişiminde temizleme, kontrol bırakma.
- [ ] 3. Yerelleştirme, stiller ve kullanılabilirlik kusurlarını gider. Hedefler: Data/_generator/p0_ui.py, UI/Resources. Kabul: eksik sabit anahtar taraması, içerik doğrulama.
- [ ] 4. Unity EditMode/PlayMode ve determinizm/kayıt diagnostics çalıştır, kalan kapsamı ve kanıtları güncelle.
- [ ] 5. Faz 10 ileri kapsam: tüm overlay'ler, isim plakaları, grafik/PNG, keşif/başarım koşulları, mobil ve erişilebilirlik kabul ölçümleri.
- [ ] 6. Faz 11–12: ses, öğretici, tam mod desteği, performans bütçeleri, içerik/sanat ve yayın hazırlığı (Docs/09). Dış hesap/mağaza yayınları ayrı somut işlem gerektirir.

## Arayüz bağlantıları ve kararlar

UI → IGameHost.Edits → InputCommands → simülasyon; UI dizilere yazmaz. Pencere anahtarları aynı hedefi tekrar açarken tek pencere üretir. Dünya değişince eski hedefler ve kontrol durumu temizlenir. Oyuncu metinleri üretici üzerinden TR/EN eklenir. Önceki tüm uncommitted dosyalar bu devamın başlangıç durumudur.

## Doğrulama

### 2026-09-27 görsel devamı

- [x] Linear renk uzayındaki gri indeks paleti düzeltildi; gerçek URP testinde 28/112/224 değerleri doğru renge gitti (`Logs/palette-green.xml`).
- [x] Altı yapı stili ve beş bina tipi oyun kamerasında karşılaştırıldı (`Logs/all-styles-closeup.png`). Meşe, çam, kaya eklendi.
- [x] Kullanıcı geri bildirimiyle bina PNG'leri 3×3 karo yapı hücreleri, yeni çevre sprite'ları en çok 1 karo genişliğindeki çizim parçaları olarak haritaya yerleştirildi. Tek kare hatasını yakalayan testler önce başarısızdı, son Unity EditMode 19/19 geçti (`Logs/final-visual-tests.xml`).
- [x] PlaySmoke: 400 kare, kayıt/yükleme ve pencere akışları başarılı (`Logs/sliced-art-playsmoke.log`). `compile_check.py` eksik Editor yanıt dosyası bulunan Bee dizinini seçmeyecek biçimde düzeltildi; 11/11 assembly derlendi.
- [x] Korunan Sim/World/Powers/Persistence/Content kaynaklarından önceki 149 SHA-256 karşılaştırmasında değişen yok; diagnostics 1000 tick determinizm ve kayıt turu başarılı (`Logs/all-styles-diagnostics.log`).


- Başlangıç: `python Tools/compile_check.py` — PG.UI 6 derleme hatası (eksik sınıflar, UnitTraitDef.Description).

## 2026-10-05 aileler ve ekipman tutuşu

- [x] Dört halkın dişi karakter sheetleri ve erkek/dişi atlas seçimi; 192 poz, 48 el noktası denetlendi. Önizleme: `Docs/previews/families.html`.
- [x] Ele sabitlenen ekipman, yürüyüşe bağlı el noktası, çalışma/vuruş salınımı ve kalp animasyonu kaynak koda eklendi.
- [x] Karşılıklı yetişkin eşleşmesi, yakınlaşma süresi, mevcut gebelik/doğuma bağlantı, aile durumu UI; çocuk/yakın akraba ve meşgul birey kontrolleri.
- [x] Üretim 12/12 ve EditMode test assembly'si çevrimdışı derlendi; gerçek hareket fonksiyonlarında 2378 managed assertion geçti.
- [ ] Unity aile regresyon testleri ve oyun içi görsel kabul: standart batch lisans bağlantısı başarısız; açık lisans kanalını kullanma girişimi otomatik incelemece reddedildi. Testler çalışmış sayılmaz.
- [ ] Yeni WebGL ve hosting paketi. Bu kapsam ve kanıt sınırları: `Docs/Family_And_Grip_2026-10-05.md`. Grafik adım 2 oyun içi kabulü henüz kapatılmadı.

## 2026-10-05 çalışma ve savaş hareketleri

- [x] Çalışma döngüsüne gövde hareketi; yaya hareketli kiriş ve çekiş eli eklendi. Yay/arbalet mermileri mevcut uçuş yönünde ince çizilir; diğer mermiler küçük nokta olarak kalır.
- [x] Can azalmasında kısa renk/beden tepkisi; iyileşme, yeni birim slotu ve duraklatma için tekrar tetikleme önlenir.
- [x] Mevcut ceset listesi çizime bağlandı: üç düşüş pozu bir kez, toplam 8 simülasyon saniyesi görünürlük, son 2 saniye solma. Gömme/dirilme/tüketme ceset kaydını kaldırdığında çizim de kalkar. Kayıt formatı değişmedi.
- [x] Önizlemeye Hasar tepkisi ve Düşüş ve kaybolma düğmeleri eklendi; 8138 portu bu çalışma klasörünü sunar. Eski 8136 önizlemesi farklı/eski dosyalar sunuyordu.
- [x] 12 üretim assembly'si ve EditMode test assembly'si çevrimdışı derlendi. Derlenmiş fonksiyonlarda 2388 managed assertion geçti; el noktası denetimi 48/48. Tarayıcıda çalışma ve düşüş pozları incelendi. Unity testleri çalıştırılmış sayılmaz.
- [ ] Canlı oyun kabulü ve WebGL yayını bekliyor. Yayın çekiş/mermi çıkışının gerçek simülasyon olay zamanıyla görsel senkronu ayrıca kontrol edilmeli. Can tepkisi render örnekleri arasındaki HP farkını izler; arada iyileşerek kapanan hasarı tespit etmez. Görünürken örneklenmemiş veya yüklenmiş cesetler cinsiyet/giysi verisi kayıtlarında olmadığından türün varsayılan görünümünü kullanır.

## 2026-10-05 mimari atlasları · 2026-10-08 altı halk kapsamı

- [x] İnsan, ork, cüce, elf, Beast ve Insect için ev/kule/demirhane setleri üretildi;
- [x] SpriteLibrary: tekil onaylı görsellerden sonra kaynak sheet+maske yükleme; 64 piksel sınırı, ortak durum ölçeği, tür ve yapı kimlikleriyle eşleme. StreamingAssets ve manifest eşlendi.
- [x] 54 mimari kare kaynak/metadata denetiminden geçti; yeni kaynak/runtime PNG ve JSON kopyaları eşlendi. Unity derleme ve EditMode testi bu eklemeler için çalıştırılmadı.
- [x] Karşılaştırma: `Docs/previews/architecture.html` (8138); altı halk, üç yapı, üç durum. Kaynak görsel önizlemesidir.
- [ ] Unity oyun içi ölçek/örtüşme ve Test Runner kabulü, ardından WebGL/hosting paketi. Yeni kaynaklar mevcut yayın oyununa henüz derlenmedi.
- Detaylar, üretim tarifi ve geri dönüş kapsamı: `Docs/Architecture_2026-10-05.md`.

## 2026-10-08 Faz J4 · merkez, ambar ve pazar atlası

- [x] Altı halk için şehir merkezi/tahıl ambarı/pazarın inşaat, tamamlanmış ve harabe halleri üretildi; her set 3×3 ve saydam arka planlıdır.
- [x] `SpriteLibrary` ev/kule/demirhane ile merkez/ambar/pazar atlaslarını çözüyor ve mevcut tekil renkli görsellere öncelik veriyor. Üreteç 3×3 hücreleri doğrudan örnekleyerek ayrık ya da birbirine değen scaffold bileşenlerine bağımlılığı kaldırıyor.
- [x] `HouseArtTests` hedef kapsamı 6 stil × 6 tür × 3 durum olarak genişletildi. Kaynak/runtime kopyaları ile manifest oluşturuldu; Unity testleri çalıştırılmadı.
- [x] Statik sanat denetimi: 738 bina anahtarında 9 final renkli + 104 atlas, 607 placeholder, 18 fallback; dünya özellikleri 44/44 çözülüyor.
- [ ] Unity derleme/Test Runner ve gerçek oyunda yapı ölçeği/örtüşme kabulü kullanıcı final aşamasında; J4/Faz J açık.

## 2026-10-08 Faz J4 · üst seviye salon ve depo atlası

- [x] Altı halk için Salon II, Salon III ve genel deponun inşaat/tamamlanmış/harabe durumları ayrı 3×3 saydam atlaslarda üretildi.
- [x] Runtime eşleyici `hall_2`, `hall_3`, `storage` kimliklerini üçüncü atlas grubuna bağladı; DataSync bina JSON eşlemelerini `Art/Final/buildings` dizininden StreamingAssets'e aynalıyor.
- [x] `HouseArtTests` kapsamı 6 stil × 9 yapı × 3 durum = 162 kimliğe çıktı. Önizleme üç atlas grubunu ve ortak durum seçicisini gösteriyor.
- [x] Statik sanat envanteri 738 anahtarda 9 final renkli + 158 atlas, 553 placeholder, 18 fallback; 44/44 dünya özelliği çözülüyor. Kaynak/runtime atlas hash'leri ve StreamingAssets manifesti kontrol edildi.
- [ ] Unity derleme/Test Runner ve gerçek oyunda salon/depo ölçek/örtüşme kabulü kullanıcı final aşamasında; J4/Faz J açık.

## 2026-10-08 Faz J4 · temel üretim atlası

- [x] Altı halk için çiftlik kulübesi, maden ve oduncu kampı; üç evreli saydam 3×3 atlaslar olarak eklendi.
- [x] Runtime eşleyici, maskeli atlas üreteci, DataSync bina metadata aynalaması, manifest ve önizleme dördüncü atlas grubunu içeriyor.
- [x] `HouseArtTests` kapsamı 6 stil × 12 yapı × 3 durum = 216 kimlik. Unity testleri çalıştırılmadı.
- [x] Statik envanter: 738 anahtarın 9'u tekil renkli, 212'si atlas, 499'u placeholder, 18'i fallback; 44/44 dünya özelliği çözülüyor.
- [ ] Unity derleme/Test Runner ve oyunda üretim yapı ölçeği/örtüşme kabulü kullanıcı final aşamasında; J4/Faz J açık.

## 2026-10-06 referansa göre küçük bina ölçeği

- [x] Kullanıcının gönderdiği köy görüntüsü esas alınarak görsel ölçek 0.4 yapıldı: 9 karo genişliğindeki ev 3.6 karo çizilir. En/boy %60, kaplanan görsel alan %84 azalır.
- [x] Yapı parselin merkezine yerleşir; gölge merkezi ve yarıçapları aynı dönüşümü kullanır. Tüm stiller ve inşaat/tamamlanmış/harabe durumları birlikte küçülür.
- [x] Yerleşim/navigasyon parseli, bina maliyetleri ve kayıt biçimi değişmedi. Bu değişiklik daha sık bina yerleştirme sağlamaz; yalnız çizim ölçeğini düzeltir.
- [x] 12/12 üretim assembly'si ve güncellenmiş test assembly'si çevrimdışı derlendi. Mesh genişliği ve merkezleme için regresyon assertions eklendi; Unity'de çalıştırılmadı.
- [x] `Docs/previews/architecture.html` üstünde aynı kamera/yerleşim için eski-yeni düğmesi. Tarayıcıda iki durum ve yeni ölçek kontrol edildi. Kaynak görsel temsili, Unity ekran görüntüsü değil. Kanıt: `Docs/previews/building-scale-preview.png`.
- [ ] Gerçek Unity dünya çizimi ve yeni WebGL derlemesi bekliyor. Referansın kesin kamera/karakter oranı bilinmediği için 0.4 ilk görsel ayardır.

## 2026-10-06 oyun içi kabul denemesi

- Standart Unity 6000.3.25f1 batch EditMode çalıştırması, BuildingVisualTests/HouseArtTests/UnitCourtshipTests/UnitAppearanceTests filtresiyle başlatıldı. Aktif Editor süreci yoktu.
- Lisans servisine normal bağlantı kuruldu; `No valid Unity Editor license found` ve headless entitlement yok hatasıyla exit 198. Test XML üretilmedi; hiçbir test geçmiş sayılmaz. Kanıt: `Logs/visual-release-2026-10-06.log`.
- Bu nedenle yeni WebGL veya hosting ZIP'i oluşturulmadı. Devam koşulu: Unity Hub'da bu sürüm için geçerli Editor lisansının etkinleştirilmesi; ardından aynı testler ve Web build. Oturum/lisans bağlantısı başka süreçten aktarılmadı.

## 2026-10-06 doğa sanat bütünlüğü

- [x] Rüzgâr yalnız bitki üstlerini etkiler; taş/cevher/kristal/dikilitaş sabittir. Mevsim tonu taşlara uygulanmaz. Parçalı ağaçlarda gerçek kök yüksekliği kullanılır.
- [x] Üç yeni final sanat varlığı: çalı, çiçek, ot. Şeffaf kenar yerine görünen piksel sınırından ölçeklenir; çalı/ot 8×6, çiçek 8×8, kaya 9×6, meşe 15×14 px. StreamingAssets ve manifest eşlendi.
- [x] 12/12 üretim assembly'si çevrimdışı derlendi. `Docs/previews/nature-scale.html` 11 doğa varlığı ve referans eviyle açıldı; rüzgâr/mevsim seçicileri kontrol edildi. Görsel `Docs/previews/nature-scale.png`.
- [ ] Gerçek Unity shader/oyun kamerası ve WebGL kabulü, lisans doğrulama engeli nedeniyle bekliyor. Kaynak önizlemesi oyun testinin yerine geçmez. Detay: `Docs/Nature_Visual_2026-10-06.md`.

## 2026-10-06 su, ateş ve büyü efektleri

- [x] Güç efekt materyalinde yarı saydam alfa etkinleştirildi: su halkası, duman, iz ve gölge artık shader tarafından kesilmez.
- [x] Düşen su/iyileştirme/ateş güçlerine ayrı temas işaretleri; tsunami dünya nesnesine köpük cephesi eklendi.
- [x] Patlama, duman ve yıldırım görsel listelerine üst sınır; yeniden dünya bağlamasında olay aboneliği temizliği eklendi.
- [x] Ayarlarda kalıcı ekran sarsıntısı anahtarı. JSON doğrulandı, üretim assembly derlemesi 12/12.
- [ ] Bu efektlerin gerçek Unity dünya görüntüsünde karşılaştırması ve WebGL kabulü bekliyor. Mevcut Unity CLI lisans engeli sürüyor.

## 2026-10-06 kalan canlılar · ilk grup

- [x] Koyun, kurt, ayı ve geyik için özgün 4×6 hareket sayfaları üretildi. Her kaynakta alfa eşiğinde 24 ayrı poz bulundu; poz maskeleri ile mevcut 40×40 atlas yükleyicisine bağlandı.
- [x] Kaynak PNG/JSON çiftleri StreamingAssets'e kopyalandı. Kare sınırları: en çok 36×23, her pozda en az 126 maskeli piksel.
- [x] Ejderha, slime, iskelet ve peri için 24'er pozlu ilk fantastik grup da bağlandı. Slime'ın ayrık dört küçük ölüm damlası atlas dışında bırakıldı.
- [x] Yeni sekiz tür tam renk shader yolunu kullanır; dört halkın gri indeks paleti korunur. 16 kaynak/runtime varlığının hashleri ve manifest kayıtları eşit. `Tools/audit_unit_art.py` 384 pozu, 12/12 üretim assembly derlemesi kodu doğruladı.
- [x] `Docs/previews/animals.html` tarayıcıda iki grup, hareketler, tam renk/eski palet ve yaklaşık iki karo ölçeğiyle incelendi.
- [ ] Gerçek Unity zoom/animasyon kabulü ve kalan 80 türün sanat grupları henüz bitmedi. Önizleme oyun görüntüsü değildir.

## 2026-10-06 fantastik tehditler · ikinci grup

- [x] Zombi, kor cini, kum solucanı, yürüyen ağaç, hayalet, ateş/su elementali ve taş golem için 24'er pozlu özgün kaynak ve poz maskeleri üretildi.
- [x] Sekiz kaynak/runtime PNG+JSON çifti eşlendi; StreamingAssets manifesti 1970 dosya. 576 pozun geometrisi, yürüyüş farklılığı ve transparan olukları çevrimdışı denetlendi; 12/12 üretim assembly'si derlendi.
- [x] Karşılaştırma `Docs/previews/animals.html` içindeki Tehditler A/B gruplarında tam renk ve yaklaşık oyun ölçeğinde tarayıcıda gözden geçirildi.
- [ ] Gerçek Unity zoom/animasyon kabulü ve kalan 72 tür bekliyor. Canlı oyun veya güncel WebGL derlemesi oluşturulmadı.
- 2026-10-06 yeniden normal batch EditMode denemesi: Unity exit 198, `No valid Unity Editor license found`; test XML yok (`Logs/visual-animals-2026-10-06-EditMode.log`). Lisans kanalına müdahale edilmedi.

## 2026-10-06 diğer canavarlar · ilk dört

- [x] Kraken, çürüme sürüngeni, şeker golemi ve jel ayı için 24'er pozlu yeni kaynak, metadata maskesi ve StreamingAssets kopyaları bağlandı. Jel ayının ilk 20 pozlu çıktısı reddedilip 24 pozlu sürümle değiştirildi.
- [x] `Docs/previews/animals.html` içinde dört silüet ve tam renkli harita ölçeği görsel incelendi. 672 pozluk sayısal denetim geçti; manifest 1978 dosya.
- [x] Bu grubun kalan sekiz türü aşağıdaki adımda kaynak ve önizleme olarak tamamlandı; Unity oyun içi kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 diğer canavarlar · kalan sekiz

- [x] Mantar yaratık, kristal böcek, kül/kemik sürüngeni, saat yengeci, boşluk güvesi, et yığını ve yutucu için 24'er pozlu özgün kaynaklar üretildi. Ayrık küçük efekt parçaları dışarıda tutularak 40×40 poz maskeleri hazırlandı.
- [x] Sekiz PNG/JSON çifti StreamingAssets'e eşlendi; manifest 1994 dosya. `Tools/audit_unit_art.py` toplam 864 pozu, poz sınırlarını ve yürüyüş farklılığını doğruladı; 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` içinde Diğer canavarlar B/C grupları 40×40 ve yaklaşık iki karo harita ölçeğinde tarayıcıda gözden geçirildi.
- [ ] Gerçek Unity zoom/animasyon kabulü, yeni WebGL derlemesi ve kalan 60 tür bekliyor. Kaynak önizlemesi Unity oyun testi değildir.

## 2026-10-06 zombi varyantları · ilk dört

- [x] Hızlı, iri, şişkin ve sürünen zombiler için farklı silüetlerle 24'er pozlu özgün kaynaklar üretildi; 40×40 maske ve atlas verisi hazırlandı.
- [x] StreamingAssets PNG/JSON çiftleri ve manifest eşlendi (2002 dosya). `Tools/audit_unit_art.py` toplam 960 pozu doğruladı; 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Zombi varyantları A grubunda normalleştirilmiş harita ölçeği ve animasyon görsel incelendi. Sayfa yüklenirken grup değiştirildiğinde eski kartın görünmesi de düzeltildi.
- [ ] Bu gruptaki 11 tür, gerçek Unity oyun kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 undead/magic · ikinci dört

- [x] Zombi canavarı, zombi ejderha, zombi lordu ve ölüm büyücüsü için farklı silüetli 24'er pozlu sayfalar oyunun atlas yoluna bağlandı.
- [x] StreamingAssets manifesti 2010 dosya; sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` 1056 pozu doğruladı, 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Zombi varyantları B grubunda dört karakter 40×40 ve yaklaşık harita ölçeğinde görsel incelendi.
- [ ] Aynı gruptaki yedi tür, gerçek Unity oyun kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 undead/magic · üçüncü dört

- [x] Kan emici, gök balinası yavrusu, kara ve ak büyücü için 24'er pozlu özgün tam renkli sayfalar atlas yükleme yoluna bağlandı.
- [x] Manifest 2018 dosya; sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` 1152 pozu doğruladı, 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Büyü ve gök canlıları grubunda 40×40 ve yaklaşık harita ölçeği görsel incelendi.
- [ ] Aynı gruptaki druid, snowman, storm_spirit ve gerçek Unity/WebGL kabulü bekliyor.

## 2026-10-06 undead/magic/elemental · son üç

- [x] Druid, kardan adam ve fırtına ruhu için 24'er pozlu kaynaklar atlas yoluna bağlandı. Fırtına ruhunun ayrık ölüm gövdeli ilk sürümü reddedilip tutarlı silüetli sürümle değiştirildi.
- [x] StreamingAssets manifesti 2024 dosya, altı yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` 1224 pozu doğruladı; 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Doğa ve hava büyüsü grubunda üç karakter 40×40 ve yaklaşık harita ölçeğinde görsel incelendi. Böylece bu envanter grubunun 15 türü kaynak/önizleme düzeyinde tamamlandı.
- [ ] Unity oyun içi animasyon/zoom kabulü ve yeni WebGL derlemesi bekliyor; diğer gruplarda 45 tür kaldı.

## 2026-10-06 yerleşim ve orman hayvanları · ilk dört

- [x] Kedi, köpek, tavşan ve inek için 24'er pozlu tam renkli kaynaklar atlas yoluna bağlandı. Tavşanın eksik ölüm sıralı ilk sürümü reddedilip altı doğru satırlı sürümle değiştirildi.
- [x] StreamingAssets manifesti 2032 dosya; sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` toplam 1320 pozu doğruladı, 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Evcil ve orman A grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde kontrol edildi. Üç türlük önceki grubun taşan kedi kartı da düzeltildi.
- [ ] Bu grupta sekiz tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 yerleşim ve orman hayvanları · ikinci dört

- [x] Tavuk, sıçan, maymun ve tilki için 24'er pozlu sayfalar atlas yoluna bağlandı. Sıçanın yüzme, maymunun ve tilkinin satır düzeni revizyonları işlendi.
- [x] StreamingAssets manifesti 2040 dosya, sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` toplam 1416 pozu doğruladı; 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Evcil ve orman B grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu gruptaki dört tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 yerleşim ve orman hayvanları · son dört

- [x] Yaban domuzu, keçi, yabani at ve ren geyiği için 24'er pozlu sayfalar atlas yoluna bağlandı. Domuzun fazla koşu satırlı ilk sürümü reddedilip 4×6 sürümle değiştirildi.
- [x] StreamingAssets manifesti 2048 dosya; sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` toplam 1512 pozu doğruladı, 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Evcil ve orman C grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde görsel incelendi. Bu grubun 12 türü kaynak/önizleme düzeyinde tamamlandı.
- [ ] Unity oyun içi zoom/animasyon kabulü ve yeni WebGL derlemesi bekliyor. Sonraki iki canlı grubunda 33 tür kaldı.

## 2026-10-06 sulak/çöl/kutup hayvanları · ilk dört

- [x] Penguen, yengeç, akrep ve kertenkele için 24'er pozlu tam renkli kaynaklar atlas yoluna bağlandı. Yengeçteki iki ayrık küçük efekt parçası ana gövde maskesine alınmadı.
- [x] StreamingAssets manifesti 2056 dosya; sekiz yeni PNG/JSON kopyası kaynakla aynı. `Tools/audit_unit_art.py` toplam 1608 pozu doğruladı; 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Sulak, çöl, kutup A grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde görsel incelendi.
- [ ] Aynı grupta 11 tür, gerçek Unity zoom/animasyon kabulü ve yeni WebGL derlemesi bekliyor. Kaynak düzeyinde toplam 29 tür kaldı.

## 2026-10-06 sulak/çöl/kutup hayvanları · ikinci dört

- [x] Timsah, yılan, kaplumbağa ve fok için 24'er pozlu kaynaklar atlas yoluna bağlandı; gövde maskeleri 40×40 oyun karesinde taşmıyor.
- [x] StreamingAssets manifesti 2064 dosya. `Tools/audit_unit_art.py` toplam 1704 pozu doğruladı, 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Sulak, çöl, kutup B grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde incelendi. Timsah genişliği nedeniyle hedef yüksekliğinden kısa normalleştiriliyor.
- [ ] Bu grupta yedi tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor. Kaynak düzeyinde 25 tür kaldı.

## 2026-10-06 sulak/çöl/kutup hayvanları · üçüncü dört

- [x] Sırtlan, aslan, gergedan ve manda için 24'er pozlu atlas kaynakları, maskeleri ve StreamingAssets eşleri bağlandı.
- [x] Manifest 2072 dosya; `Tools/audit_unit_art.py` 1800 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Sulak, çöl, kutup C grubunda dört silüet 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu grupta deve, kutup ayısı ve semender; ayrıca gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor. Kaynak düzeyinde 21 tür kaldı.

## 2026-10-06 sulak/çöl/kutup hayvanları · son üç

- [x] Deve, kutup ayısı ve semender için 24'er pozlu atlas kaynakları, maskeleri ve StreamingAssets eşleri bağlandı; böylece bu grubun 15 türü kaynak/önizleme düzeyinde tamamlandı.
- [x] Manifest 2078 dosya; `Tools/audit_unit_art.py` 1872 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Sulak, çöl, kutup D grubunda üç tür 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor. Son canlı grubunda 18 tür kaldı.

## 2026-10-06 küçük, uçan ve özel canlılar · ilk dört

- [x] Kurbağa, arı, karınca ve böcek için 24'er pozlu kaynak, maske ve StreamingAssets kopyaları bağlandı.
- [x] Manifest 2086 dosya; `Tools/audit_unit_art.py` 1968 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Küçük canlılar A grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu grupta 14 tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 küçük, uçan ve özel canlılar · kuşlar

- [x] Baykuş, karga, kartal ve papağan için 24'er pozlu kaynak, maske ve StreamingAssets kopyaları bağlandı. Uçuş animasyonu dördüncü satırda hazırlandı.
- [x] Manifest 2094 dosya; `Tools/audit_unit_art.py` 2064 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Kuşlar grubunda dört silüet 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu grupta 10 tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 küçük, uçan ve özel canlılar · ikinci küçük grup

- [x] Salyangoz, balık, kelebek ve tırtıl için 24'er pozlu kaynak, maske ve StreamingAssets kopyaları bağlandı. Balık görselinin farklı kaynak en-boy oranı poz çıkarımında doğrulandı.
- [x] Manifest 2102 dosya; `Tools/audit_unit_art.py` 2160 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Küçük canlılar B grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu grupta 6 tür, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 küçük, uçan ve özel canlılar · özel dört

- [x] Pirana, sivrisinek sürüsü, gök ziyaretçisi ve mekanik dev için 24'er pozlu kaynak, maske ve StreamingAssets kopyaları bağlandı. Sürü pozları ayrı böceklerden oluştuğu için import aracı 4×6 hücre içindeki tüm alfa piksellerini koruyor.
- [x] Manifest 2110 dosya; `Tools/audit_unit_art.py` 2256 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Özel canlılar A grubunda dört tür 40×40 ve yaklaşık harita ölçeğinde incelendi.
- [ ] Bu grupta haydut ve tarikatçı, gerçek Unity zoom/animasyon kabulü ve WebGL derlemesi bekliyor.

## 2026-10-06 küçük, uçan ve özel canlılar · son iki

- [x] Haydut ve tarikatçı için 24'er pozlu kaynak, maske ve StreamingAssets kopyaları bağlandı. Saldırı pozlarında bıçak ve ritüel asa elde görsel olarak kontrol edildi.
- [x] Manifest 2114 dosya; `Tools/audit_unit_art.py` 2304 pozu doğruladı ve 12/12 üretim assembly'si çevrimdışı derlendi.
- [x] `Docs/previews/animals.html` Özel canlılar B grubunda iki tür bekleme ve saldırı karelerinde, 40×40 ve yaklaşık harita ölçeğinde incelendi. Böylece 92 türün tamamında ayrıntılı atlas kaynağı mevcut.
- [ ] Gerçek Unity zoom/animasyon kabulü ve yeni WebGL derlemesi bekliyor; çevrimdışı denetimler Unity Test Runner yerine geçmez.

## 2026-10-06 P3 · HUD ve ilk 20 araç simgesi

- [x] `Data/powers.json` sırasıyla ilk 20 dünya aracı için ayrı, düzenlenebilir SVG ve 64×64 PNG üretildi; final sanat ve StreamingAssets kopyaları eşit. Manifest 2134 dosya, 20 yeni ikon içeriyor.
- [x] `PgIcons` final ikonları önce yükleyip kalan araçlarda placeholder'a düşüyor. Araç adı, görsel kodu ve önizleme eşleşmeleri `Docs/previews/power-icons.html` üzerinde kontrol edildi; tüm 20 PNG yüklendi.
- [x] Mobil/dar HUD'da 44 tasarım piksellik düğmeler ve arama girişi, iki satırlı üst bar, dikey alt araç/fırça yerleşimi, dar pencereler ve küçültülmüş mini harita kaynakta ayarlandı. 390×844 önizleme sayfasında 20 ikon yüklendi, örnek düğmeler en az 44 CSS pikseli; bu Unity HUD çalışma zamanı testi değildir.
- [x] `Tools/compile_check.py` 12/12 üretim assembly'sini derledi; `git diff --check` ve ikon SHA/manifest denetimi geçti.
- [ ] Unity Editor Test Runner, gerçek mobil dokunma/taşma ve yeni WebGL derlemesi lisans kapısı nedeniyle doğrulanamadı. P3 performans/yayın ölçümleri sıradaki iş.

## 2026-10-06 P3 · performans ve yayın hazırlığı

- [x] `VisualBenchmark` ile sabit tohum/kamera ve 1.000/5.000 birim için 30 ısınma + 180 çizimlik CPU süre/managed allocation CSV ölçüm yolu hazırlandı; `run_unity.ps1 -Mode VisualBenchmark` bağlandı.
- [x] `verify_web_bundle.py` ve dağıtım betiği, eski ikili/manifest ve eksik kritik sanat verilerini yakalayacak şekilde bağlandı. Bugünkü negatif denetim eski paketi reddetti: WebGL ikilisi kaynaklardan eski, manifest uyumsuz.
- [x] 12/12 üretim assembly'si çevrimdışı derlendi, Python dosyaları sözdizimi kontrolünden geçti.
- [ ] Unity EditMode denemesi 198 kodu ve `No valid Unity Editor license found` ile durdu; XML yok. Bu yüzden benchmark CSV, yeni WebGL/Android paketleri, 60/30 FPS ve GC/frame cihaz kabulü henüz ölçülmedi. Ayrıntılı protokol: `Docs/Visual_Performance_2026-10-06.md`.

## 2026-10-06 menü simgeleri

- [x] Ana menü, hızlı araç şeridi, alt kategori sekmeleri ve kayıt/yükleme işlemleri için 25 SVG/PNG final simge üretildi. `WindowBase.Small`, `HudView` ve `SaveLoadWindow` simgeleri metinle birlikte gösteriyor; silme onayı metni güncellenebiliyor.
- [x] 21 ek pencere işlemi simgesiyle aile 46'ya çıktı. Ortak `WindowBase.Small` düğmeleri anlamına göre aynı simgeyi kullanıyor; dar başlık kontrolleri metin olarak bırakıldı. Önizlemede 46 kart ve 53 yüklenen görsel, bozuk bağlantı yok; manifest 2180 dosya.
- [x] Araç şeridi tek satırlı yatay kaydırmaya çevrildi; ikonlu düğmeler dar ekranda favorilerin üstüne taşmıyor. `Docs/previews/menu-icons.html` üzerinde ana menü ve 25 simge görsel olarak kontrol edildi.
- [ ] Gerçek Unity HUD/menü görüntüsü ve WebGL derlemesi, Editor lisans engeli nedeniyle henüz doğrulanmadı.

## 2026-10-06 zemin/çim görseli

- [x] Çimenli biyom ve toprak/tepe karolarındaki büyük varyant lekeleri yumuşatıldı; yakın zoom için deterministik kısa çim, kum için hafif tanecik eklendi. Uzak zoom, yol/kar/yanık alan ve özel biyom ayrımları kaynakta korundu.
- [x] Parıltılı çim demeti çizimi daha doğal, keskin kenarlı piksel varlıkla değiştirildi; eski kaynak `Art/Concept/grass_tuft_before_2026-10-06.png` olarak saklandı.
- [x] `Docs/previews/nature-scale.html` içinde güncel çim demeti ev, kaya ve çiçeğin yanında harita ölçeğinde incelendi; 11 doğa görseli yüklendi.
- [x] `Docs/previews/ground-detail.html` önce/sonra palet karşılaştırmasında incelendi; bu gerçek oyun görüntüsü değildir. Ayrıntı: `Docs/Ground_Visual_2026-10-06.md`.
- [ ] Unity shader derlemesi, oyun içi üç zoom ve WebGL/Android görsel-performans kontrolü lisans engeli nedeniyle bekliyor.
- [ ] `ground-20261006-EditMode` yeniden denemesi exit 198 ve `No valid Unity Editor license found` verdi; XML yok. Kaynak düzeyindeki 12/12 C# kontrolü canlı oyun kabulü sayılmıyor.
- [ ] Normal Unity Editor açılış yoklaması da `Unity Editor Software Terms` penceresinde kaldı; `Logs/gui-license-probe.log` UI entitlement bulunmadığını yazdı. Açılan yoklama süreci kapatıldı; Unity oturum/koşul adımı kullanıcı tarafından tamamlanmalı.
- [x] Yayın doğrulaması tüm WebSite dosyaları, final menü ikonları ve final doğa varlıklarını kapsayacak şekilde genişletildi. Mevcut eski `Builds/WebSite` paketi yeni kaynaklardan eski ve manifest uyumsuz olduğu için reddedildi.

## 2026-10-06 zemin · taş ve kar

- [x] Dağda seyrek taş dikişi, zirve/kar örtüsünde hafif rüzgâr izi; uzak zoom'da sönen iki yüzey ayrıntısı kaynakta eklendi. Karo varyantlarının sert farkı dağ/zirvede azaltıldı.
- [x] `Docs/previews/terrain-materials.html` çim, kum, taş ve kar için önce/sonra palet örneği olarak hazırlandı; Unity ekranı olduğu iddia edilmiyor.
- [x] Çayır/kum ve farklı kara biyomu sınırında tek karoluk %18 palet geçişi eklendi; su, yol, kar ve yanık alan bu geçişin dışında kaldı. Önizleme sadece çayır/kum örneğini yaklaşık gösterir.
- [ ] Kullanıcının isteğiyle Unity, shader, çevrimdışı derleme ve oyun içi görsel testleri geliştirme sırasının sonuna bırakıldı. `license-resumed-20261006-EditMode` lisans istemcisine bağlanamayarak test sonucu üretmedi; bu yoklama süreci kapatıldı.
- [ ] Kullanıcı Unity lisans hatasının artık görünmediğini bildirdi. Buna rağmen `license-resumed-20261006-EditMode` CLI oturumu iki lisans bağlantı zaman aşımına girdi ve XML üretmedi; `gui-resumed-probe` da sonuç üretmeden kapatıldı. Sonraki Unity denemeleri kullanıcının isteği doğrultusunda son kabul aşamasına ertelendi.

## 2026-10-06 zemin · referansa göre doğal doku

- [x] Kullanıcının görseli ada düzeni değil zemin dokusu referansı olarak yorumlandı; dünya üretimi ve kıyı geometrisi değiştirilmedi.
- [x] Çim üzerindeki karo başına belirgin ot çizimi kaldırıldı. Daha koyu zeytin tonlu çayırda iki ölçekte kümeli piksel farkı ve seyrek açık noktalar; kumda sakin ton kümeleri, suda düşük kontrastlı yüzey ayrıntısı kaynak shader'a eklendi.
- [x] `Docs/previews/terrain-materials.html` beş yüzeyi (çayır, kum, dağ, kar, su) yan yana gösterir. Bu kaynak palet önizlemesidir, yeni Unity/WebGL derlemesinin ekran görüntüsü değildir.
- [x] Kum kıyısındaki su geçişi yumuşatıldı, kayalık kıyının beyaz köpüğü azaltıldı. Aynı önizlemeye ada düzeni içermeyen çayır→kum→sığ su→okyanus→derin su kesiti eklendi.
- [x] Biyomsuz gri tepelerin yeşil çim efekti alması giderildi; taş ayrıntısı kullanılıyor. Su derinliği komşu rengini artık komşunun kendi varyantından hesaplıyor.
- [x] Çayır, huş/orman/jungle, akçaağaç ve çiçek/yonca için ayrı hafif ton katsayıları bağlandı; sonbahar ve çiçek paletleri tek tip koyu yeşile dönüşmüyor. Yedi biyom kaynak önizlemesine eklendi.
- [x] Son görsel kabul için gerçek Unity kamera çıktısını 50/35/20 ortografik ölçekte kaydedecek `Preview Ground Materials` geçici dünya aracı ve `GroundPreview` komut modu hazırlandı; kayıt yuvaları kullanılmıyor, ada üretilmiyor.
- [ ] Kullanıcının son aşama Unity kontrolünde `Logs/ground-materials-{wide,in-game,closeup}.png` çıktıları ve gerçek oyun görünümü incelenecek; bu üç PNG henüz üretilmedi.
- [x] Referansın ada düzeni değil zemin malzemesi olduğunu tekrar netleştirerek çimen desenini sekiz alt hücreye ve daha sık, küçük kümelere çevirdim; çayır/orman yeşil dengesini ayarladım. Shader'da su bölümündeki yinelenen değişken tanımı düzeltildi. Kaynak önizlemesi buna göre yenilendi.

## 2026-10-07 doğal çayır malzemesi v2

- [x] Yeni özgün çim dokusu, geniş organik ot kümeleri ve seyrek mat toprak izleriyle üretildi; eski doku korunarak `MapRenderer` yeni kaynak adına bağlandı.
- [x] `PG_MapChunk` içinde toprak maskesi düşük şiddette sıcak renk veriyor; tekrarlanan zemin örneklemesi ve biyom tonu korundu. Unity dışı önizleme yeni dokuyu gösteriyor.
- [x] Kaynak PNG 1254×1254; karşı kenar ortalama RGB farkı yatay 6.23, dikey 9.95. Unity görsel/performans testleri bu adımda çalıştırılmadı.
- [ ] Gerçek oyun görüntüsü, shader derlemesi, biyom uyumu ve uzak/yakın ölçek kontrolü kullanıcının son kabul aşamasında yapılacak.

## 2026-10-06 dört halk · son görsel kabul düzeneği

- [x] Unity Editor `Preview Four Races` menüsü ve `UnitPreview` komut modu hazırlandı. Geçici dünyada insan/ork/cüce/elf, her tür için yetişkin kadın/erkek, çocuk ve bebek birlikte diziliyor; yaşlar tür ömründen hesaplanıyor. Dört geçici krallığın ayrı sancak paleti görünüyor; simülasyon duraklatılıyor ve kayıt yuvaları kullanılmıyor.
- [x] Aynı gerçek oyun kamerasından 35/20/10 ortografik ölçekte üç PNG ve üç vuruş pozu PNG'si alma akışı kaynağa eklendi. Yetişkin erkekler ırka uygun kılıç/balta/çekiç taşır, kadınlar silahsız özgün vuruş pozunu kullanır.
- [x] Kadın karakterlerin silahsız vuruşu yürüyüş gövdesi yerine atlasın özgün üç saldırı karesini kullanıyor; ekipmanlı saldırıdaki katman korunuyor.
- [ ] Kullanıcının son aşama Unity kontrolünde `Logs/four-races-{wide,in-game,closeup,attack-1,attack-2,attack-3}.png`, silüet/ölçek/örtüşme ve animasyon gözlemi yapılacak; PNG'ler henüz üretilmedi.
- [ ] Kullanıcının belirttiği gibi Unity derlemesi ve oyun içi görsel testler tüm geliştirmelerin son kabulüne bırakıldı.

## 2026-10-06 meslek araçları · çalışma hareketi

- [x] Tırpan, balta, kazma ve çekicin çalışma açıları ayrı hareketlere bağlandı; hepsi aynı kılıç salınımını kullanmıyor. Tutma noktası ele sabit kalıyor, simülasyon ve kayıt biçimi değişmiyor.
- [x] `Preview Professions` / `ProfessionPreview` kaynağı, dört mesleği kadın/erkek iki sırada gerçek oyun kamerasına hazırlar; 35/20/10 ortografik ölçekte üç PNG hedefler.
- [ ] Kullanıcının son aşama testinde gerçek oyunda iş sırasında hareket, araç tutuşu, silüet ve `Logs/professions-{wide,in-game,closeup}.png` incelenecek; Unity akışı henüz çalıştırılmadı.
- [x] Can azalışında mevcut yaralanma sinyaline üç küçük, beş tikte sönen darbe pikseli bağlandı. Mevcut ceset kaydı üç ölüm pozu ve sekiz saniyelik solma için kaynakta kontrol edildi.
- [ ] Vuruş kıvılcımı, ölüm dizisi ve kalabalık savaşta okunaklılık son Unity kabulünde görülecek.

## 2026-10-07 dört halkın yapıları · durum kabulü

- [x] Mevcut `Preview All Building Styles` yalnız tamamlanmış beş yapı türünü gösteriyordu; mimari paketin ev/kule/demirhane ve inşaat/harabe durumlarını kapsamıyordu. Ayrı `Preview Architecture States` / `ArchitecturePreview` kabul sahnesi 4×3×3 = 36 bina durumunu kaynakta kuruyor.
- [x] Harabe örnekleri `CivWorld.Ruin` ile üretildi; böylece görsel bayrak ve geçilebilir arazi davranışı aynı gerçek oyun yolunu kullanıyor. Bina ölçeği ve dünya üretimi değiştirilmedi.
- [ ] Son Unity kabulünde `Logs/architecture-states-{wide,in-game,closeup}.png`, altı halkın mimari dili, üç durum ve mevcut `VisualScale` gerçek oyun kadrajında incelenecek; PNG'ler henüz oluşmadı.

## 2026-10-07 doğa ve gece ışığı

- [x] Zemin kararıp karakter, bina ve ağaçların gündüz parlaklığında kalması giderildi: `PG_Unit` ve doğa materyali global gece rengini %68 güçle alıyor. Bulut/duman/yağış malzemesi değişmedi; küçük silüetler zemin kadar karartılmıyor.
- [x] İlk karede `_DayLight` beyaza kuruluyor; palet render testinin global ışık durumunu kendisi ayarlayıp geri yüklemesi kaynakta bağlandı.
- [ ] Son Unity kabulünde gündüz/gece kıyası, karakter okunaklılığı, ağaç ve bina ton uyumu ile shader derlemesi incelenecek.

## 2026-10-07 iklim D1–D2

- [x] D1: Mevsim verisi ve sistem bağlantıları `season_rules.json` üzerinden eklendi; bitki/mahsul büyümesi, vahşi birim doğurganlık-açlık-hareketi ve depo bozulması mevsim çarpanlarını okuyor.
- [x] D2: 8×8 zonlarda kalıcı byte nemi, aylık sıcaklık buharlaşması, su/komşu-su tamponu, yağmur takviyesi ve üç aylık kuraklık eşiği eklendi.
- [x] D2 nemi iklim save bölümünün opsiyonel son eki olarak saklanıyor; eski kayıtlar için uyumlu okuma ve başlangıç nemi türetme yolu mevcut.
- [ ] D1/D2 testleri, kayıt turu, performans ve uzun iklim soaku kullanıcı talimatıyla son kabul aşamasına bırakıldı; bu fazlar henüz kabul edilmedi.
- [x] D3 kaynak uygulaması: Şehir buğdayı her ay yerel zon nemi/sıcaklığına göre ilerler; hasat miktarı iklim faktörüne ve Demir Saban, Sulama Kanalları, Su Değirmeni icatlarına göre ölçeklenir. Sulama kuru koşul cezasını azaltır.
- [ ] D3 hasat verimi/teknoloji karşılaştırması ve D fazı kabulü kullanıcı testleri son aşamaya bırakıldığı için yapılmadı.
- [x] D4: Kaynak incelemesiyle kıtlık → mutluluk/doğum, yıllık aynı-krallık göçü, düşük bağlılık/isyan ve çağ sıcaklık zincirleri bulundu. `FamineEvent` kuraklık/soğuk ipucunu taşıyor; UI bildirimi bunu olası iklim etkeni diye açıklıyor.
- [ ] D4 iklim-famine soak, bildirim ve göç/isyan doğrulaması yapılmadı; D4/D fazı kabul edilmedi.
- [x] D5: Var olan harita overlay seçicisine nem/kuraklık, mevsim ve tahmini mahsul verimi katmanları eklendi. Renk açıklaması ve canlı mevsim etiketi yerelleştirildi; renderer yalnızca state okur.
- [ ] D5 renk/katman okunaklılığı, mobil görünüm ve overlay yenilemesi Unity'de doğrulanmadı; D fazı kabul edilmedi.

## 2026-10-07 E2b barış şartları · kaynak dilimi

- [x] Barış tekliflerinde sabit altın haraç, devredilecek şehir, öneren krallığın vasallaşması ve saldırmazlık süresi taşınıyor. Haraç/vasallık savaş hedefleri otomatik tekliflere bu koşulları ekliyor.
- [x] Kabulde haraç kaynak krallığın var olan altınından alıcının başkentine aktarılıyor; şehir yalnız teklif sahibine aitse ve son şehir değilse devrediliyor; vasallık ilişki döngüsü kontrolünden geçiyor.
- [x] Diplomasi teklif listesi barış şartlarını yerelleştirilmiş özetle gösteriyor. Policy kayıt biçimi 5; biçim 1–4 göçü korunuyor.
- [x] Geçerli/son şehir/üçüncü taraf şehir devri, haraç-saldırmazlık, vasallık ve kayıt turu regresyonları test kaynağına eklendi.
- [ ] Kullanıcının talimatıyla test/derleme çalıştırılmadı. E2b ve Faz E kabul edilmedi; Unity regresyonları, eski kayıt turları ve çok tohumlu savaş diplomasisi soaku son kabul aşamasında çalıştırılacak.

## 2026-10-07 E3a ordu ikmali ve moral · kaynak dilimi

- [x] `ArmyData` ham kayıt düzenine dokunmadan ordu başına erzak/moral yan listesi eklendi; Policy biçim 6 yazılıyor ve biçim 1–5 okumaları korunuyor.
- [x] Ordu, kendi şehrinin 30 birim çevresinde sınırlı yiyecek ikmali alıyor; stok rezervi korunuyor. Erzak birlik doygunluğunu yeniliyor, yetersiz beslenme morali düşürüyor; düşük moral geri çekilme eşiğini yükseltiyor.
- [x] Savaş penceresi aktif orduların asker/erzak/moralini gösteriyor; uzaktaki askerin şehir stoğundan beslenmesi kaldırıldı.
- [x] Moral sınır davranışı ve kayıt turu için iki regresyon testi eklendi.
- [ ] Kullanıcının talimatıyla test/derleme/soak çalıştırılmadı. E3a/E3 ve Faz E kabul edilmedi; B5 ekipman etkisi, kuşatma savunması, uzak ikmal/yağma ve çok tohum denge kontrolü açık.

## 2026-10-07 E3b donanım gücü ve kuşatma · kaynak dilimi

- [x] Mevcut B5 eşya üretimi, asker donatımı ve kalite/malzeme stat çarpanları kuşatma gücüne bağlandı; güç, birimlerin gerçek hasar/zırh/savaş istatistiklerinden türetiliyor.
- [x] Tamamlanmış sur, gözetleme kulesi, kapı binası ve salon seviyesi şehir savunmasına ekleniyor. Şehirde savunmacı varsa ya da saldırgan merkeze ulaşmadıysa kuşatma ilerlemesi duruyor; en açık şehir bile en az iki ayda düşüyor.
- [x] Kuşatma ilerlemesi `ArmyLogistics` yan listesinde korunuyor; Policy save biçimi 7 oldu, biçim 1–6 okumaları sürüyor. Savaş penceresi kuşatma aylarını gösteriyor.
- [x] Kuşatma süresinin savunma/donanım ilişkisi ve kuşatma kayıt turu için regresyonlar yazıldı.
- [ ] Test, derleme veya soak çalıştırılmadı. E3b/E3/Faz E kabul edilmedi; ekipman üretim sayısı, savaş-şehir savunma dengesi, çok krallık uzun soaku ve uzakta yağma/ikmal açık.

## 2026-10-07 E3c yağma hedefi · kaynak dilimi

- [x] `WarGoal.Plunder`, başarılı kuşatmada şehri devretmeden çalışıyor: şehrin altınının %25'i saldıran ordunun ana şehrine gider; sivil rezerv üzerindeki yiyecekten ordunun boş erzak kapasitesi kadar alınır.
- [x] Yağmalanan şehrin bağlılığı 20 düşer; şehir, yağmacı krallık, altın ve rasyon miktarı `CityPillaged` olayıyla dünya tarihine eklenir. Hedef tamamlanınca savaş bir kere saldıran tarafın zaferiyle biter.
- [x] Eski tarihçe enum indekslerini korumak için yeni tür `CityPillaged` mevcut türlerin sonuna eklendi; TR/EN kaynak metni üretildi.
- [x] Altın ve erzak sınırları için EditMode regresyonu yazıldı.
- [ ] Test, derleme ve soak çalıştırılmadı. E3c/E3/Faz E kabul edilmedi; şehir kaynağı, kuşatma ve savaş dengesi son kabul aşamasında test edilecek.

## 2026-10-07 E4a pazar fiyatı ve teslim güvenliği · kaynak dilimi

- [x] Alıcı şehrin kaynak stoğuna göre birim fiyat 0.5×–1.5× arasında hesaplanıyor; `TradeKeep` stoğu dengeli fiyat, düşük stok kıtlık primi ve yüksek stok indirim üretiyor.
- [x] Kervan çıkışında ödeme gücü, teslimde tekrar güncel stok ve altınla doğrulanıyor. Yolculuk sürerken savaş başlarsa satmıyor; satılamayan yük kaynak şehre dönüyor.
- [x] Kıtlık/denge/fazla stok fiyat ilişkisi için regresyon testi yazıldı.
- [ ] Unity/oyun testi, deniz rotası, mesafe maliyeti, güvenlik ve çok krallıklı fiyat yakınsama soaku yapılmadı. E4a ve E4 kabul edilmedi.

## 2026-10-07 E4b kara ticaret rotası ve taşıma maliyeti · kaynak dilimi

- [x] Pazar, aynı ada ve diplomatik ilişki koşullarını geçen şehirlerdeki her alıcı/ihtiyaç kaynağı eşleşmesini puanlıyor; en yakın şehir yerine en yüksek pozitif net geliri seçiyor.
- [x] Miktar ve mesafeye bağlı gider satıcı kazancından düşülüyor; satış başlangıcında stok/altın, teslimde savaş/altın/depo kapasitesi tekrar kontrol ediliyor; kalan mal kervanla kaynak şehre dönüyor.
- [x] Mesafe ve yük miktarı arttıkça taşıma maliyetinin yükseldiğini denetleyen regresyon eklendi.
- [ ] Unity/oyun testi ve fiyat yakınsama soaku çalıştırılmadı. Deniz ticareti ve E4 kabulü hâlâ açık.

## 2026-10-07 E4c limanlar arası deniz ticareti · kaynak dilimi

- [x] Tamamlanmış pazarı/rıhtımı olan dost şehirler ortak su adasındaysa, kârlı kara rotası çıkmayan kaynak şehir stok fazlasını deniz teknesiyle gönderiyor.
- [x] Tekne su path servisini kullanıyor; teslimde güncel fiyat, alıcı altını/depo kapasitesi ve savaş durumu denetleniyor. Satılamayan yük kaynak şehre döner; tekne limana dönünce sefer kapanır.
- [x] `BoatData` raw kayıt düzeni ve mevcut enum sayısal değerleri değiştirilmedi; migration eklenmedi.
- [x] Alıcı kapasitesi/altını ve mesafe maliyeti sınırı için regresyon testi eklendi.
- [ ] Unity/oyun testi, çok tohumlu ticaret çalışması ve E4 kabulü yapılmadı; testler kullanıcının son aşama isteğiyle ertelendi.

## 2026-10-07 E4d ticaret anlaşması, ambargo ve kıtlık ithalatı · kaynak dilimi

- [x] Ticaret anlaşması fiyatı %10 ve taşıma maliyetini %25 azaltıyor; kara kervanı ve deniz ticareti seçim/teslim hesapları aynı etkileri taşıyor.
- [x] Yiyecek stoğu düştükçe ithalat rota puanı 1.25×–2.5× yükseliyor; altın, depolama, arz ve mesafe sınırları sürüyor.
- [x] AI, çok düşük görüşte 10 yıllık karşılıklı ambargo önerebiliyor. Kabul edilen ambargo yeni rota açılmasını engelliyor; yoldaki yük satılmayıp kaynağa geri dönüyor. Ambargo ve ticaret anlaşması çakışmıyor. Vali eylemi, UI etiketi ve Policy save biçim 8 eklendi; biçim 1–7 okunuyor.
- [x] Fiyat/gider, kıtlık önceliği ve kayıt turu için regresyon testleri yazıldı.
- [ ] Test, derleme ve çok tohumlu ticaret soaku çalıştırılmadı. E4d/E4 ve Faz E kabul edilmedi; kullanıcı testleri son aşamaya bırakmamı istedi.

## 2026-10-07 E5a açlık huzursuzluğu ve veraset krizi · kaynak dilimi

- [x] Açlık üç aydan sonra bağlılığa ayda 4 puan ek baskı yapıyor; toplam ek ceza 20'de sınırlanıyor.
- [x] Kral öldüğünde başka şehirde yaşayan yetişkin rakip çocuk, bağlılık tabanlı en çok %35 ihtimalle taht iddiası savaşı çıkarabiliyor; ayrılan şehir yeni krallığın başkenti ve claimant'in tahtı oluyor. Oyuncunun seçilmiş varisi korunuyor.
- [x] Succession dispute mevcut `war.succession` veri tipini kullanıyor; ham `WarData`/`KingdomData` kaydı büyütülmedi. Olasılık ve açlık-ceza regresyonları eklendi.
- [ ] Vergi politikası kullanıcı/vali tarafından ayarlanamıyor; E5'in yüksek vergi nedeni bu nedenle tamamlanmadı. Unity/test/derleme/soak ve Faz E kabulü açık.

## 2026-10-07 E5b değişken vergi politikası · kaynak dilimi

- [x] Vali `SetPolicy.tax` 0–100 düzeyini kabul ediyor; 50 mevcut vergiyi, 0 yarım geliri ve +10 mutluluğu, 100 1.5× geliri ve −10 mutluluğu üretir. Vergi mutluluğu şehir bağlılığı/isyan hesabına akar.
- [x] Vali snapshot'ında vergi politikası görünür; eski JSON eyleminde alan yoksa 50 kabul edilir.
- [x] Policy bölüm biçim 9 vergi değerini saklar; biçim 1–8 eski kayıtları dengeli varsayılanla okur.
- [x] Gelir/mutluluk sınırları ve policy save round-trip regresyonları yazıldı.
- [ ] Unity test, derleme ve çok krallıklı soak çalıştırılmadı. E5/E kabulü açık.

## 2026-10-07 F5a jeolojik kaynak dağılımı · kaynak dilimi

- [x] Maden damarları yükselti seviyesine bağlı JSON profilleriyle seçiliyor; bakır/demir eteklerde düşük ağırlık alırken yüksek değerli damarlar yüksek arazide, yıldız cevheri kristal biyomunda ve obsidyen volkanik biyomda sınırlandırılıyor.
- [x] Büyük kara bileşenlerinde (en az 128 tile veya dünya alanının 1/512'si) eksik bakır, demir, taş ve odun deterministik biçimde ekleniyor; odun için yerel biyom ağacı tercih ediliyor. Nehir tile'ları kaynak yerleşiminden hariç.
- [x] Özellik seçimi ve küme yerleşimi aynı feature indeksini taşıyor; grup denetimi için EditMode regresyon kaynağı eklendi.
- [ ] Kullanıcının talimatıyla test/derleme çalıştırılmadı. Çok tohumlu dağılım, 256×256 süre/bellek ve görsel kabul açık; F5/Faz F tamamlanmadı.

## 2026-10-07 F6a dünya kurulumunda nehir yoğunluğu · kaynak dilimi

- [x] Dünya oluşturma penceresine önizleme, rastgeleleştirme ve üretim ayarına bağlanan 0–1 nehir yoğunluğu eklendi. Sıfır, gölleri koruyup kanalları kapatıyor; artan değer akış eşiğini düşürüyor.
- [x] Ayar, `WorldGenSettings.Clamp()` içinde sınırlandırılıyor; TR/EN metni `_generator/p0_ui.py` ile `ui_strings.json` kaynağında.
- [ ] Kullanıcının talimatıyla Unity/arayüz testi, derleme ve görsel önizleme çalıştırılmadı. F6/Faz F tamamlanmadı.

## 2026-10-07 F6b yeni dünya biçimleri · kaynak dilimi

- [x] İç Deniz (`annulus`), Nehir Havzaları (`multiBlob`, erozyon/nehir yoğun) ve Dağlık Takımadalar (yüksek noise/ridge/dağ payı) şablonları içerik üreticisi ve çalışma JSON'una eklendi.
- [x] Şablon seçimi yeni `mountainRatioDefault`, `hillRatioDefault`, `riverDensityDefault`, `moistureDefault` alanlarını ayarlıyor; nullable defaults ve aralık doğrulaması mevcut şablonlarla geriye uyumlu.
- [x] Yeni preset geometrisi ve template varsayılanlarını kapsayan EditMode testi yazıldı.
- [ ] Kullanıcının talimatıyla generator, içerik doğrulaması, Unity testi/derlemesi ve gerçek önizleme çalıştırılmadı. F6/Faz F tamamlanmadı.

## 2026-10-07 F7a büyük harita belleği göstergesi · kaynak dilimi

- [x] 128–1024 world preset'leri yanında 64 byte/tile tahmini tile-array çalışma belleği ve 4× küçültülmüş önizleme boyutu/bellek tahmini gösteriliyor.
- [x] Hatalı enum harita boyutu `Medium`'e dönüyor; tahmin 256→512 boyutlarında 4× ve 4× preview'da 1/16 ölçeği bekleyen regresyon kaynağı eklendi.
- [ ] Gerçek üretim süresi, peak native/renderer/GPU bellek, 1024 kare üretim ve WebGL/Android sınırı ölçülmedi. Kullanıcı son test isteğiyle test/derleme çalıştırılmadı; F7/Faz F kabulü açık.

## 2026-10-07 G1 tarih önemi ve savaş nedenselliği · kaynak dilimi

- [x] `Data/history_kinds.json` ve generator kaynağı 25 `HistoryKind` için 0–100 temel önem tanımlıyor; önemli olaylar sıradan tarih gürültüsünden ayrılabilecek veri puanını taşıyor.
- [x] `HistoryEntry` ham layout'u sabit kaldı. Yan metadata listesi benzersiz `EventId`, `CauseEventId`, puan saklıyor; `HST1` v1 Meta section son eki round-trip yazılıyor ve eski kayıtlar eski byte düzeninden varsayılan metadata ile okunuyor.
- [x] Krallık üye sayısı temel öneme %0–20 arasında sınırlı çarpan veriyor. Barış olayları ilgili savaş ilanına; şehir ele geçirme/isyan aynı iki krallık arasındaki son savaş ilanına bağlanıyor. Tarihçe budaması iki listeyi aynı indekslerle koruyor.
- [x] Veri kapsamı, puan sınırları, deterministik skor ve savaş-barış save round-trip'i için EditMode test kaynakları eklendi.
- [ ] Kullanıcı talimatıyla test/derleme/oyun denemesi yapılmadı. 200 yıllık olay yoğunluğu ölçülmedi; ordu ölçeği, kral ölümü ayrımı ve kıtlık/göç kaynak olayları açık. G1/Faz G kabul edilmedi.

## 2026-10-07 G2 çevrimdışı kronik şablonları · kaynak dilimi

- [x] 26 tarih olayına Türkçe ve İngilizce anlatı varyantları `chronicle_templates.json` ve `_generator/p4_civ.py` üzerinden eklendi; ContentDB kayıtları yüklüyor ve boş dil/şablonları reddediyor.
- [x] History, krallık/şehir ve kişi geçmişi pencereleri ortak UI renderer'ını kullanıyor. Şehir, krallık, kişi, kültür, dil, din, klan, savaş, kitap, afet, tür ve buluş isimleri bağlama göre çözülüyor; tekrar kullanım per-world kısa cache'de.
- [x] Buluş keşfi yanlış `ItemForged` olayından ayrılıp enum sonundaki yeni `InventionDiscovered` değerine taşındı; eski HistoryKind sayısal değerleri değişmedi. Yağma anlatısı altın miktarını kaydın doğru alanından gösteriyor.
- [x] İki dilde kapsam, deterministik metin ve cache davranışı için `ChronicleTests` yazıldı.
- [ ] Test, derleme ve oyun içi dil/isim doğrulaması kullanıcı talimatıyla yapılmadı; G2 ve Faz G kabul edilmedi.

## 2026-10-07 G3 seçili kişi biyografileri · kaynak dilimi

- [x] Hükümdar, ordu kaptanı, seviye 5/10 öldürme kahraman eşiğini aşanlar ve eşsiz/efsanevi eşyası olanlar UID ile Meta v2 yan kaydında izleniyor; kişi başına son 6 olay sabit ring'de saklanıyor.
- [x] Önemli aile çevresinde doğum, eş bağı, çocuk; hükümdarlık, savaş/kent çatışması, kariyer eşiği, efsanevi/eşsiz eşya kuşanma ve ölüm olayı bağlandı. Ölümden önce isim/tür/yaş başlangıcı/aile/eş/öldüren/seviye/öldürme özeti yakalanıyor.
- [x] Meta save yan bloğu HST1 v2'ye genişletildi; HST1 v1 ve yan bloksuz eski kayıtlar boş biyografi deposuyla yükleniyor. Native kapasite tavanı yaklaşık 3.2 MB.
- [x] Halka sırası/sınırı, ölüm özeti round-trip ve v1 uyumu için `BiographyTests` kaynakları eklendi.
- [ ] Kullanıcı talimatıyla test, Unity derlemesi ve oyun içi/uzun dönem doğrulama yapılmadı; G3 ve Faz G kabul edilmedi. UI sekmesi Faz H işidir.

## 2026-10-07 G4 çağ ve yüzyıl özetleri · kaynak dilimi

- [x] Her 100 yılda ve çağ değişiminde dünya özeti oluşturuluyor; nüfus, şehir/krallık/savaş, hastalık/undead, en büyük krallık, en kayıplı savaş, biyografi skoru en yüksek kişi ve dönem içindeki kültür doğum/sonları raporlanıyor.
- [x] HST1 yan bloğu v3'e genişletildi; v1/v2 ve yan bloksuz kayıtlar hâlâ okunuyor. Rapor geçmişi yüklenince `GraphsWindow` dünya serilerine yüzyıl nüfus noktalarıyla başlanıyor ve simülasyon yıllık örnekleriyle sürüyor.
- [x] Summary kayıt turu ve yinelenen dönemi engelleme için `WorldSummaryTests` eklendi.
- [ ] Kullanıcı talimatıyla test/derleme/oyun grafiği ve uzun dönem soak çalıştırılmadı; G4 ve Faz G kabul edilmedi.

## 2026-10-07 G5 önemli olay yönlendirmesi · kaynak dilimi

- [x] Savaş/barış bildirimine tıklamak ilgili hedef şehre veya saldıran krallığın başkentine odaklanıp savaş ayrıntısını açıyor.
- [x] Krallık kuruluşu, yeni hükümdar ve buluş bildirimleri başkente odaklanıp ilgili krallık penceresini açıyor; yeni şehir bildirimi şehir merkezine gidip şehir ayrıntısını açıyor.
- [x] Var olan bildirim kategorileri ve kullanıcı susturma ayarları korunuyor; yeni kayıt biçimi veya simülasyon otomasyonu eklenmedi.
- [ ] İlk kaynak diliminde genel önem eşiği ve otomatik kamera tercihi eksikti; G5b bunları ekledi. UI/Unity testi kullanıcı talimatıyla çalıştırılmadı; G5 ve Faz G kabul edilmedi.

## 2026-10-07 G6a filtrelenebilir tarih çizgisi · kaynak dilimi

- [x] Tarih penceresine tüm/önemli (profil eşiği, varsayılan ≥60) olay filtresi, mevcut metin araması ve 100'lük sayfalarla eski olayları yükleme eklendi; TR/EN metinleri içerik üreteci kaynağında.
- [x] Şehir/krallık/savaş satırları tıklanınca UIden yeniden çözülüp nesne penceresi açılıyor ve varsa kamera harita hedefine gidiyor; UID çözümü tıklama anında yapılarak liste çizimindeki tekrar taramalar önlendi.
- [x] Olay sayısı aynı kalırken tarihçe başı budanıp yeni kayıt eklendiği durumda, son EventId değişikliği pencereyi yeniliyor.
- [ ] Kullanıcı talimatıyla UI/Unity testi çalıştırılmadı. Krallık soyağacı, kişi arama ve tarih hedefi harita işaretçisi G6b/G6c dilimlerinde kaynakta eklendi; kabul testi hâlâ açık.

## 2026-10-07 G6b kişi biyografisi ve krallık soyağacı

- [x] Tarih penceresinde kişi adı/tür araması, 100'lük yükleme, aile bağları ve son altı biyografi olayını gösteren yaşam özeti var; yaşayan kişi UID üzerinden yeniden çözülerek açılıyor.
- [x] Krallık soyağacı, isyan kayıtlarında `B=ebeveyn`, `C=ayrılan krallık` UID ilişkisini kullanıyor; döngü koruması ve girinti sınırı bulunuyor. Diğer kuruluş türleri için soy bağı uydurulmuyor.
- [x] Seçilmiş kişinin yeni yaşam olayı/ölüm durumu değişince açık pencere yenileniyor.
- [ ] Kişi/soyağacı görünümü, kayıt yükleme ve Unity UI testi çalıştırılmadı; G6/Faz G kabul edilmedi.

## 2026-10-07 G6c harita tarih işaretçisi

- [x] Tarih satırından şehir/krallık/savaş hedefi, biyografi kişisi veya canlı krallık başkentine gidince ekranda 5 saniyelik, tıklamaları yutmayan etiketli dünya işaretçisi gösteriliyor.
- [x] Kamera/ekran dışına çıkınca işaret gizleniyor; hedef tekrar görünür alana gelirse süresi bitmediyse tekrar beliriyor. Dünya değişiminde temizleniyor.
- [ ] Unity UI/gerçek kamera, farklı ekran ölçekleri ve harita performansı kullanıcı testlerinin sonuna bırakıldı; G6/Faz G kabul edilmedi.

## 2026-10-07 çayır zemini kümelenme katmanı

- [x] Kullanıcının görsel referansını ada biçimi değil, daha doğal piksel zemin olarak yorumlama isteği doğrultusunda; dünya/ada üretimine dokunmadan mevcut `PG_MapChunk` çim shader'ına geniş ve orta ölçekli yumuşak kümelenme eklendi.
- [x] Seyrek toprak açıklıkları shader maskesine bağlandı; biyomun temel rengi ve uzak zoom'da detay sönümü korundu. `MapRenderer` malzeme etkisi 0.28'den 0.32'ye yükseltildi.
- [x] `Docs/previews/terrain-materials.html` shader'a yakın kümeli çim ve seyrek toprak açıklıkları gösterecek şekilde güncellendi.
- [ ] Kullanıcının son aşama test talimatıyla Unity shader derlemesi, gerçek oyun ekranı, performans ve zoom kabulü çalıştırılmadı; sonuçlar doğrulanmadı.

## 2026-10-07 çayır zemin dokusu v3

- [x] Kullanıcının referanstan istediği hedef ada geometrisi değil, daha gerçekçi zemin dokusu olarak netleştirildi; harita üretimi ve kıyı şekli korunuyor.
- [x] Özgün, tekrar edilebilir, yoğun küçük ot kümeleri ve seyrek toprak açıklıkları taşıyan `terrain_grass_detail_v3.png` eklendi. `MapRenderer` v3'ü yükler; biyom rengi ve shader tabanlı uzak zoom sönümü korunur. v1/v2 varlıkları silinmedi.
- [x] `terrain-materials.html` v3 dokusunu gösteriyor; dokümantasyon güncellendi.
- [ ] Kullanıcı testleri sona bırakıldığı için Unity shader derlemesi, harita içi görünüm, texture repetition ve performans henüz kontrol edilmedi.

## 2026-10-07 Faz F0 tanısı ve F1a dağ sırtı

- [x] Kaynak üretim hattı sıralandı; multi-octave FBM ve domain warp'ın zaten mevcut olduğu, nehir/erozyon hattının bulunmadığı doğrulandı. Faz F belgesine tanı kaydı eklendi.
- [x] Yüksek arazide FBM sırt çizgilerini belirginleştiren `ridgeStrength` şablon ayarı eklendi. Efekt aynı mevcut noise örneğini kullandığı için octave maliyeti artmıyor; harita biçimleri/şablon maskeleri ve hedef kara oranı korunuyor.
- [x] Üreteç, içerik JSON'u, `HeightJob` ve determinizm/sınır regresyon kaynağı güncellendi.
- [ ] Test, içerik doğrulaması, tohum galerisi ve dünya üretim süresi kullanıcı son aşama isteği gereği çalıştırılmadı. F1 ve Faz F kabul edilmedi.

## 2026-10-07 G7/H7 dışa aktarma ve paylaşım kodu

- [x] Kronik, kronoloji sırasıyla Markdown olarak WebGL'de indirilir; masaüstünde persistentDataPath'e UTF-8 yazılır.
- [x] Dünya özeti tarih penceresinde temel istatistikleri gösterir. WebGL'de oyun görüntüsü + metin PNG olarak birleştirilip indirilir; masaüstünde ekran PNG'si ve istatistik Markdown'ı aynı ad köküyle oluşturulur.
- [x] Aktif oturumun üretim tarifi `PGW1` sürümü ve FNV-1a kontrol toplamıyla kopyalanır; yeni dünya formuna yapıştırılarak doğrulanıp uygulanır. Yerel özel görüntü şablonları dışarıda tutulur.
- [x] WebGL panoya kopyalama/Blob indirme ve masaüstü dosya/clipboard bağlantıları eklendi; Türkçe/İngilizce UI metinleri eşlendi.
- [x] Yeni save dosyalarına taşınabilir üretim tarifi isteğe bağlı 19. bölüme yazılıyor; eski save dosyalarında geçmiş ayarlar yok ve sonradan üretilemiyor. Normal round-trip ve tarifi olmayan eski kayıt için test kaynağı eklendi; çalıştırılmadı (bkz. G7 devamı, 2026-10-08).

## 2026-10-07 Faz A doğruluk kapısı ve kapsamlı hash düzeltmesi

- [x] A4 kalite kapısı test sayısı eşiği, ayrılmış `Graphics` vakalarını hesaba katacak biçimde 204 grafiksiz + 3 grafik vakası olarak güçlendirildi. Çoklu soak raporunda ilk yıl krallık sayısına ek olarak yaşayan medeniyet türü kapsamı da denetleniyor.
- [x] A5 deterministik karşılaştırma, Meta'nın tüm kaydedilebilir bölümünü sürümlü Meta serializer üzerinden hash'liyor. Kayıt turunda harita/birim/medeniyet/Meta durumları hem kayıt anında hem 120 devam tick'inde ayrı karşılaştırılıyor.
- [ ] Bu değişiklikler ve mevcut A1–A5 kaynak dilimleri derlenmedi/çalıştırılmadı; kullanıcının talimatı gereği testleri son aşamaya bırakıyorum. Çok tohumlu gerçek metrik raporu ve `Tools/baselines/` taban çizgisi hâlâ yok; A fazı kabul edilmedi.

## 2026-10-07 Faz A3 şema ve A6 repo grupları

- [x] Metrik JSON'ın `metrics_compare.py`/`gate.ps1` tarafından tüketilen alanları, yıllık tür nüfusu, şehir/yem akışı ve summary alanları için `SoakMetricsTests.cs` regresyon kaynağı eklendi.
- [x] `Tools/baselines/README.md` ile gerçek kabul raporunu saklama ve kıyaslama komutu belgelendi; eski loglardan sahte baseline oluşturulmadı.
- [x] A6 için inceleme grupları belgeye yazıldı. Kullanıcı commit kararı kendisinde olduğundan commit yapılmadı.
- [ ] Unity testi, gerçek soak raporu ve baseline üretimi yapılmadı; kullanıcı testleri son aşamaya bırakıyor. A fazı kabulü açık.

## 2026-10-07 Faz B4 yiyecek akış sayacı düzeltmesi

- [x] `CityFoodFlow.AddedToStock` sayaç hatası düzeltildi: kabul edilen gıda (`add`) sayılıyor; depo kapasitesini aşan talep yalnızca `Overflow` olarak kaydediliyor. Üretim toplamlarıyla sayaçlar aynı bazda ölçülüyor.
- [x] `FoodFlowTests.cs` kabul edilen stok/taşma ayrımını ve yıl sonu JSON raporuna aktarımı kapsayacak şekilde eklendi.
- [ ] Test/derleme ve soak çalıştırılmadı; kullanıcı talimatıyla final doğrulamaya bırakıldı. B4 ve B fazı kabulü açık.

## 2026-10-07 çayır zemini v4 · sakin piksel kümeleri

- [x] Referansın ada geometrisi değil çimen yüzeyi olduğu korunarak yeni tekrarlanabilir zemin malzemesi üretildi: geniş koyu yeşil alanlar, seyrek ot kümeleri ve az sayıda toprak izi.
- [x] Yeni malzeme `MapRenderer` kaynak yüklemesine bağlandı; önceki zemin dokuları geri dönüş için korundu. `terrain-materials.html` yeni yüzeyi gösteriyor.
- [x] Unity kaynaklarında tekrarlama, kare boyut ve point-filter ayarlarını denetleyecek EditMode test kaynağı eklendi.
- [ ] Unity derlemesi, test ve oyun içi kabul kullanıcı talimatıyla sona bırakıldı; gerçek harita görünümü henüz doğrulanmadı.

## 2026-10-07 Faz B şehir/iş/kraft regresyon kapsamı ve ekipman verimi

- [x] `CityGrowthTests`: minimum yatak rezervinde ev adayı ve açık komşu arazide zone büyütme senaryoları eklendi.
- [x] `JobQuotaTests`: odun stoğu açığı ile oduncu kotasının yükselip stok açığı kapanınca sıfırlanması ve küçük şehir tabanı kapsandı.
- [x] `CraftChainTests`: metal stok olmadan demirhanede eşya üretimi, tırpan/kazmanın eşleşen verim bonusları, alet kırılması ve eski/yeni kayıt dayanıklılık turu eklendi.
- [x] Ekipman davranış bonusu artık kalite ve malzeme çarpanıyla tırpan hasadına, kazma ocak/taş verimine uygulanıyor; her kullanımda alet aşınıyor ve kırılan eşya kuşanılmış slottan kaldırılıyor. `DUR1` ek bloğu yeni dayanıklılığı saklıyor; blok bulunmayan eski save'ler sağlamlık 100 ile yükleniyor. Odun kotası için test edilebilir hesaplayıcı üretim yolunda kullanılıyor.
- [ ] Unity derlemesi, EditMode ve soak kullanıcı isteğiyle final aşamasına bırakıldı. Faz B kaynak işleri test kanıtı ve çok tohumlu ekonomik kabul bekliyor.

## 2026-10-07 Faz C besin ağı ve biyokütle regresyon kaynakları

- [x] `EcosystemTests` eklendi: türlerin av verisi/trofik alanları, spatial index üzerinden yakındaki izinli av sayımı ve bölgesel biyokütlenin bitki kaynağı tüketimi ile harita türetilmiş verilerinin yeniden kurulmasında tutarlılığı kapsanıyor.
- [ ] Kullanıcı talimatıyla Unity derlemesi ve testler çalıştırılmadı. Göç, tükenme, hastalık yoğunluğu, leş döngüsü ve 200 yıllık çok tohumlu ekosistem dengesi hâlâ final doğrulama bekliyor; Faz C kabul edilmedi.

## 2026-10-07 Faz D iklim ve tarım regresyon kaynakları

- [x] `ClimateAgricultureTests` eklendi: 12 aylık mevsim tekrarı/kış etkileri, sıcak-kuru koşulda kuraklık ve nem geri kazanımı, yağmur nem sınırı, düşük nemde sulama telafisi.
- [ ] Unity derlemesi/testleri ve 6 tohumlu iklim-tarım soaku kullanıcı talimatıyla sona bırakıldı. Overlay, kayıt turu, iklim-kıtlık nedenselliği ve nüfus zinciri kanıtı olmadan Faz D kabul edilmedi.

## 2026-10-07 Faz E savaş yıkım hedefi yürütümü

- [x] Tamamlanan kuşatma sonrası `WarGoal.Destruction` artık savunucu tarafındaki geçerli hedef şehrini harabeye çeviriyor ve savaşı saldıran taraf zaferiyle kapatıyor. Belirlenmiş hedef şehirle eşleşme ve `Sieging` ordu durumu doğrulanıyor.
- [x] Hedefin yıkılması/savaşın kapanması ve üçüncü krallığın şehrinin korunması için `AllianceTests` regresyonları eklendi.
- [ ] Unity derlemesi/testi ve Faz E çok krallıklı soak kullanıcı talimatıyla çalıştırılmadı. Faz E kabul edilmedi.
## 2026-10-07 çayır zemini v5 · daha doğal geniş kümeler

- [x] Referanstaki arazi malzemesi hedefi korundu; ada geometrisi ve harita üretimi değiştirilmedi. Yeni v5 çayır dokusu eklendi, v4 ve önceki kaynaklar geri dönüş için tutuldu.
- [x] `MapRenderer` v5 kaynağına geçirildi. Shader'daki yüksek frekanslı parlak ot işaretleri sakinleştirildi, doku ölçeği büyütüldü ve toprak renk lekeleri seyrekleştirildi.
- [x] `terrain-materials.html` önizlemesi v5'i gösteriyor; import ayarları Point/Repeat ve mipmap kapalı olarak v4'ten aktarıldı.
- [ ] Unity derlemesi ve oyun içi görünüm kullanıcı testleri son aşamaya bırakıldığı için çalıştırılmadı.

## 2026-10-07 Faz G1 · kıtlık→kaynak savaşı neden zinciri

- [x] `FamineEvent` şehir/krallık UID'si, önem derecesi ve mevcut iklim nedeni ile tarihçeye alındı; TR/EN kronik şablonları ve tarih satırından şehre git eylemi eklendi.
- [x] Açıkça `ResourceShortage` nedeni taşıyan savaş, taraf krallığına ait en yakın ve üç oyun yılından yeni kıtlık olayına bağlanıyor. Alakasız taraf ve savaş nedenleri bağlantı üretmiyor.
- [x] Enum'a olay en sona eklendi; eski enum indeksleri, `HistoryEntry` ham düzeni ve HST1 save sürümü korunuyor. Regresyon test kaynağı yazıldı.
- [ ] Unity derlemesi, EditMode ve 200 yıllık tarih soaku kullanıcı testlerini son aşamaya bırakma talimatıyla çalıştırılmadı. Afet sonrası tahliye davranışı ve tarih bağı kaynakta tamamlandı; oyun içi sonuç kanıtı bekliyor. Faz G kabul edilmedi.

## 2026-10-07 Faz G1 · kıtlık→göç→savaş zinciri

- [x] `KingdomSystem` kıtlığın ilk kayıtlandığı ayda gerçekten taşınan sakinleri sayıp tek `FamineMigrationEvent` yayımlıyor; memnuniyetsizlik kaynaklı olağan göçler ayrı tarih olayı üretmiyor.
- [x] Göç kaydı şehir UID'lerini, krallığı ve kişi sayısını taşır; kaynak şehir kıtlığına neden bağı kurar. `ResourceShortage` savaşı uygun göç varsa ona, yoksa katılımcının yakın tarihli kıtlığına bağlanır.
- [x] TR/EN çevrimdışı kronikler, içerik tanımları ve kaynak şehir odaklama eklendi. Enum'lar sona eklendi; eski kayıt indeksleri/şeması korundu. Regresyon test kaynağı kıtlık→göç→savaş zincirini kapsayacak şekilde güncellendi.
- [ ] Unity derlemesi, EditMode ve tarih soaku kullanıcı testlerini sona bırakma talimatıyla çalıştırılmadı. Afet→göç ve ordu ölçeği kaynakta tamamlandı; uzun tarih soaku ve oyun içi kabul bekliyor. Faz G kabul edilmedi.

## 2026-10-07 çayır zemini v6 · seyrek ot, mat toprak geçişleri

- [x] V5'in sık parlak tutamlarını azaltan yeni zemin varyantı üretildi; geniş düzensiz yeşil alanlar ve mat toprak açıklıkları öne çıkarıldı.
- [x] MapRenderer ve görsel test kaynağı v6'ya geçirildi; önceki doku sürümleri geri dönüş için korundu. Tarayıcı önizlemesi 256 piksel tekrar ölçeğinde güncellendi.
- [ ] Unity derleme/test ve gerçek oyun görünümü kullanıcı testlerinin son aşamasına bırakıldı; shader'ın gerçek kamera ölçeğindeki etkisi henüz görülmedi.
## 2026-10-07 Faz G1 · izlenen kişi ve hükümdar ölümleri

- [x] Biyografisi izlenen önemli kişinin ölümü düşük önem (5), hükümdar ölümü yüksek önem (70) ile tarihçeye ekleniyor; sıradan ve izlenmeyen ölümler atlanıyor.
- [x] Ölüm kuyruğunda kişi/krallık/hükümdar durumu slot serbest bırakılmadan yakalanıyor. Veraset, aynı krallığın yakın tarihli hükümdar ölümüyle neden bağı kuruyor; kronik adı kalıcı biyografiden çözüyor.
- [x] Önem/veraset regresyon test kaynakları eklendi. Kullanıcının talimatıyla Unity testleri çalıştırılmadı. G1 kaynak işleri tamam; doğrulama bekliyor.

## 2026-10-07 Faz G1 · savaş kroniğinde ordu ölçeği

- [x] Savaş ilanında iki taraf ve müttefiklerindeki etkin askerler sayılıyor; logaritmik `%0–20` asker katkısı krallık ölçeğiyle birleştirilip toplam çarpan `%20` ile sınırlandırılıyor.
- [x] Asker ölçeğinin artışı/sınırı ve önem puanlarının birleşimi için regresyon kaynağı eklendi. Unity derleme/testleri kullanıcı isteğiyle çalıştırılmadı.
- [x] Afet sonrası göç davranışı ve tarihçe neden bağı kaynakta tamamlandı; Unity testleri ve oyun içi kabul bekliyor.

## 2026-10-07 çayır zemini v7 · sakin ve doğal doku

- [x] Örnek görselden ada/kıyı biçimi alınmadan, geniş yeşil alanlar ve seyrek toprak açıklıkları olan 1024×1024 tekrarlanabilir piksel dokusu eklendi; üretim betiği `Tools/generate_terrain_v7.py`.
- [x] `MapRenderer`, görsel regresyon kaynağı ve `terrain-materials.html` v7'yi kullanıyor. V1–v6 dosyaları korundu; dünya üretimi ve harita şekli değiştirilmedi.
- [ ] Unity derlemesi, testler ve oyun içi görünüm kullanıcı talimatıyla son test aşamasına bırakıldı.

## 2026-10-07 Faz G1 · afet sonrası tahliye ve tarih bağı

- [x] Afet isteği etki fırçasının 8 karo sınırındaki yakın şehri kayda alıyor. Sonraki yıllık göç turunda etkilenen şehir sakinlerinin aynı krallıktaki afet görmemiş, boş konutlu şehre tahliyesi deneniyor.
- [x] Gerçek taşınan sayısı afet işaretli metadata ile `PopulationMigrated` olarak kaydediliyor; `CauseEventId` kaynak şehrin yakın tarihli afetine bağlanıyor. Kronik nedenini Türkçe/İngilizce gösteriyor.
- [x] Afet şehri eşlemesi, bir yıllık tetik penceresi, neden bağı ve metadata bayrağı için EditMode test kaynakları eklendi. Unity test/derlemesi ve oyun içi kontrol kullanıcı talimatıyla çalıştırılmadı.

## 2026-10-07 Faz G5b · ortak önem eşiği ve belgesel kamera

- [x] Yeni Meta tarih kayıtları `EventId` üzerinden taranıyor; açılışta mevcut geçmişin tekrar bildirilmesi engelleniyor. Eski tür tabanlı bildirimler kaldırılarak çift toast önlendi.
- [x] Profil ayarına 60 varsayılanlı, 20–100 aralığında önem eşiği eklendi; bildirimler ve tarih penceresi filtresi aynı politikayı kullanıyor. Mevcut kategori susturmaları korunuyor; konum bağlantıları şehre/krallığa/savaşa gider, diğer olaylar tarih penceresini açar.
- [x] İsteğe bağlı belgesel kamera, bir taramada en yeni konumlu önemli olaya odaklanır. TR/EN ayar metinleri ve eşik regresyon testi kaynağı eklendi. `DECISIONS.md` #164.
- [ ] Unity derlemesi, test ve oyun içi bildirim sıklığı kullanıcı talimatıyla final aşamasına bırakıldı; G5 ve Faz G kabul edilmedi.

## 2026-10-07 Faz H1 senaryo ve hedef kaynak dilimi

- [x] `Data/_generator/p8_scenarios.py`, `Data/scenarios.json` ve `Data/_schemas/scenarios.schema.json` ile sekiz senaryo tanımı ve içerik kataloğu eklendi. Senaryolar sabit dünya ayarları, başlangıç nüfusu, hedefler, başarısızlık koşulları ve isteğe bağlı güç kısıtlarını taşıyor.
- [x] Yeni dünya ekranında senaryo seçimi; senaryolu dünyada deterministik başlangıç nüfusu; salt okunur `ScenarioRules`/ölçüler; otomatik açılan hedef penceresi ve HUD güç kısıtları bağlandı.
- [x] Kayıt bölümü 18 senaryo kimliği, başlangıç yılı ve terminal kazanma/kaybetme sonucunu saklıyor. Önceki kayıtlar ve sandbox varsayılan senaryosuz başlıyor.
- [x] Sandbox, ölçüm salt-okunurluğu, kataloğun sekiz girdi içermesi, güç kısıtları ve zaman aşımı kaybı için `ScenarioTests` kaynakları yazıldı; Unity Test Runner çalıştırılmadı.
- [x] Hedef penceresinde kazanma hedeflerine ek olarak kayıp eşikleri ve anlık değerleri gösteriliyor; tetiklenen kayıp koşulu uyarı rengiyle ayrılıyor. Türkçe/İngilizce metin katalogları yenilendi.
- [ ] Kullanıcı isteğiyle Unity derleme/test ve gerçek oyun kabulü en son aşamaya bırakıldı. Sekiz senaryonun oynanabilirliği ve otomatik kazanma/kaybetme dengesi kanıtlanmadı; H1/Faz H kabul edilmedi. H2–H8 sürüyor.

## 2026-10-07 Faz H2 rehber cihaz yönergeleri

- [x] Sekiz mevcut öğretici adımı için fare ve dokunmatik etkileşim açıklamaları ayrıldı; çalışan adım koşulları ve atla/yeniden başlat akışı korundu.
- [x] Türkçe/İngilizce kaynak metinleri içerik üretecine eklendi.
- [ ] Duruma dayalı "ne yapabilirim?" önerileri, ilk 10 dakika kullanılabilirlik gözlemi ve mobil/masaüstü gerçek kullanım kabulü açık. Unity testi kullanıcı isteğiyle son aşamaya bırakıldı; H2/Faz H kabul edilmedi.

## 2026-10-07 Faz H3 güç bulma kaynak dilimi

- [x] HUD güç araması yerelleştirilmiş ad, açıklama, sekme ve okunabilir güç kimliğinde arama yapıyor; son kullanılan en fazla altı simülasyon gücü profil ayarı olarak tutuluyor.
- [x] Güç ipucu etki parametrelerini içeriyor; kilitli güçlerde başarıma dair mevcut ipucu korunuyor.
- [ ] Durum tabanlı öneriler, keşfedilmemiş içerik akışının tamamı, dokunmatik arama kullanılabilirliği ve test/oyun kabulü açık. Testler kullanıcı isteğiyle sona bırakıldı; H3/Faz H kabul edilmedi.

## 2026-10-07 Faz J1/J2 · yayın hazırlığı kaynak dilimleri

- [x] `Tools/loc_audit.py` çeviri katalog kimliği, boş TR/EN, biçim yer tutucu eşitliği, katalog sayısı ve literal üretim `Loc.T`/`Loc.F` anahtarlarını denetliyor; kalite kapısı derlemeden önce çağırıyor. Dinamik anahtarlar ve olası kullanılmayan içerik anahtarları ayrıca notlanıyor.
- [x] Kayıt yenilemesinde `.bak` yedeği alınıyor; okunamayan ana save için başlık listeleme/yükleme yedek dosyaya geri düşüyor ve kurtarma loglanıyor. Slot silme yedeği de kaldırıyor. Otomatik afet isteği, efekt kuyruğa girmeden önce otomatik snapshot tetikliyor; bozuk ana kayıt/yedek geri dönüş regresyon kaynağı eklendi.
- [ ] Kullanıcı talimatıyla Unity derleme/test ve araçların çalıştırılması sona bırakıldı. Çeviri taraması, bozuk/yedekten kurtarma ve kesinti senaryoları çalıştırılmadı; J1/J2 ve Faz J kabul edilmedi.

## 2026-10-07 Kullanıcı soak gözlemi · küçük kurucu topluluk

- [x] Yıl 118 ekranında dünya toplamı 312 olsa da 208 papağan, 3 elf, 0 insan, 3 şehir sakini ve bir yıkılmış krallık görüldü; medeniyet nüfusu sağlıklı büyümemiş.
- [x] Kaynakta şehir kuruluşunu, aynı alt türden en az altı birimin 20 karo içinde olması ve yetişkin/sapient bir kurucunun tetiklemesi sağlıyor. Eşleşme için karşı cins, erişkinlik, doygunluk/mutluluk ve aile/tehdit koşulları geçerli; üreme yasası da açık olmalı. Tek görüntü kesin başlangıç nedenini göstermiyor.
- [ ] Final kullanıcı testinde aynı seed/save ile insan ve elf sayısını zaman serisi olarak izleme; kuruluş ve doğum/ölüm/kıtlık geçmişini karşılaştırma. Unity testleri ve soak kullanıcı talimatıyla henüz çalıştırılmadı.

## 2026-10-08 Kullanıcı gözlemi · 171. yılda medeniyet türleri görünmüyor

- [x] Yeni ekran görüntüsünde yıl 171'de toplam 170 canlı ve yalnız 2 tür (148 papağan, 22 maymun) görünüyor; önceki yıl 118 ekranında yalnız 3 elf kalmış, insan sayısı sıfır ve bir krallık yıkılmıştı. Görüntüler ölüm nedenini tek başına kanıtlamıyor.
- [x] Kaynak incelemesinde üreme aylık kontrolündeki 12 bireylik yerel kalabalık sınırının `IsCiv` ayrımı olmadan tüm türlere uygulandığı doğrulandı. Şehirde aynı 24×24 alan içindeki medeniyetler kümelenince doğum tümüyle durabiliyordu.
- [x] Yerel sert sınır yabani türlerle sınırlandı; medeniyetlerin üremesi mevcut şehir konut/yiyecek katsayıları ve genel nüfus tavanıyla dengeleniyor. İnsan ve yaban türü davranışları için regresyon test kaynakları eklendi; #182.
- [ ] Unity derlemesi/EditMode ve aynı kayıtta 100+ yıllık insan/elf nüfus, doğum-ölüm/kıtlık geçmişi kullanıcı final test aşamasında; bu kaynak düzeltmesinin canlı save üzerindeki kesin etkisi henüz doğrulanmadı.

## 2026-10-07 H4 · tür koruma bölgesi kaynak dilimi

- [x] Tile Details seçili birimin türünü bulunduğu 8×8 bölgeye ayırıyor veya korumayı kaldırıyor; Protection katmanı korunan tür alanlarını renkle gösteriyor.
- [x] Yaban avcısı seçimi, korunan türü koruma alanının içindeyken av olarak dışlıyor. Kayıt verisi isteğe bağlı bölüm kuyruğuna eklendi ve eski kayıtlar için mevcut biçim korunuyor.
- [x] Komut davranışı ve save/load kalıcılığı için EditMode regresyon kaynakları eklendi.
- [ ] Unity derlemesi/testi ve oyun içi koruma/overlay kontrolü kullanıcının final test aşamasına bırakıldı; geri alma maliyet ölçümü ve H4/Faz H kabulü açık.

## 2026-10-07 Faz H5/H6 · gözlem kaynak doğrulaması ve erişilebilirlik

- [x] H5 kamera belgesel modu, birim takip/kontrol ve hız önayarları/kısayolları kaynakta doğrulandı; kullanıcı testi ve kabul açık.
- [x] H6 profilinde renk körlüğüne uygun overlay kimlik paleti ve yüksek kontrast seçeneği eklendi; yüksek kontrast UI renk tokenları ile bölge katmanlarını etkiliyor. UI ölçeği ve azaltılmış hareket için ekran sarsıntısı ayarıyla aynı profilde çalışıyor.
- [x] İlk sekiz renk kimliğinin ayrışmasını ve palet döngüsünün kararlılığını kapsayan EditMode kaynak testi eklendi.
- [ ] Unity derlemesi/testi, renk körlüğü simülasyonu, kontrast oranı ölçümü ve gerçek mobil/masaüstü görsel kabulü kullanıcı testlerinin sonuna bırakıldı. H5/H6 ve Faz H kabul edilmedi.

## 2026-10-07 Faz H7 · başarım dışa aktarma

- [x] Başarım penceresinden tüm listeyi Markdown olarak dışa aktarma eklendi; kazanılan/kilitli durumu ve ilerleme sayısı aktarılıyor.
- [x] WebGL indirme köprüsü ve masaüstü `persistentDataPath` kaydı kullanılıyor; kaydetme/başlatma ve hata mesajları mevcut yerelleştirilmiş anahtarları kullanıyor. Yeni düğme tr/en kataloglarına eklendi.
- [x] Başarım işaretleri, içerik adları/koşulları ve ilerleme sayısını doğrulayan EditMode regresyon test kaynağı eklendi.
- [ ] Unity derleme/testi ve gerçek WebGL/masaüstü dışa aktarma kullanıcı talimatıyla son aşamaya bırakıldı; H7/Faz H kabulü açık. Testleri çalıştırmadım.

## 2026-10-07 Faz H8 · ayar arama ve performans tercihleri

- [x] Ayarlar genel, erişilebilirlik, performans/görüntü, ses, tarihçe, bildirim ve AI gruplarına ayrıldı; grup/etiket araması ve eşleşme yok durumu eklendi.
- [x] Dokunmatik cihazlarda dil, yazı ölçeği, performans, karşıtlık, kamera ve gece/gündüz için ayrı kısa görünüm var; arama veya düğmeyle tüm gruplar açılıyor. Varsayılana dönme ayarları sıfırlayıp başarımları, keşifleri, favorileri ve izlenen krallıkları koruyor.
- [x] Düşük/Dengeli/Yüksek önayarları 30/60/120 FPS sınırı ve hava/gölge/duman çizim bütçesiyle bağlandı; FPS sınırı ayrıca elle ayarlanabiliyor. Profil eski sürümde önayar alanı yoksa dengeliye normalize ediliyor.
- [x] Tercih sıfırlama ve ilerlemenin korunması için EditMode regresyon testi kaynağı eklendi.
- [ ] Unity derleme/testi, telefon/WebGL görünümü ve kullanılabilirlik kabulü kullanıcı talimatıyla final aşamaya bırakıldı; ayrı mobil ayar düzeni ve H8/Faz H kabulü açık.

## 2026-10-07 Faz I1 · platform bütçe profilleri ve ölçüm kökeni

- [x] Kalite eşiği yapılandırmasına editör batch, masaüstü Player, WebGL Player ve Android Player için ayrı birim/FPS/tick profilleri eklendi. Kaynak hedefler 5.000/60 FPS/8 ms, 2.500/30 FPS/10 ms ve 1.200/30 FPS/12 ms; donanım ölçümüyle doğrulanana dek plan hedefidir.
- [x] `PerfBench` birim hedefi ve bütçeyi çevre değişkeninden alıp Unity/platform/OS/cihaz/CPU/GPU backend bilgisini raporluyor. Gate yalnızca editör batch profilini koşturur ve sonucu Player kabulünden ayırır.
- [x] Sayısal parametre ayrıştırma fallback ve invariant kültürünü kapsayan `PerfBenchTests` kaynağı eklendi.
- [ ] Kullanıcı talimatı nedeniyle derleme, Unity testi, gate ve benchmark çalıştırılmadı. Gerçek Player ölçümü, WebGL/Android hedefleri ve telefon ısınma/bellek kabulü açık; I1/Faz I tamamlanmadı.

## 2026-10-07 Faz I2 · deterministik simülasyon dilimleri

- [x] Dünya Kanunları'na 1–4 dilim ayarı eklendi; varsayılan 1 mevcut davranışı koruyor ve kayıtlı dünya kanunlarıyla saklanıyor.
- [x] Birim AI kararları UID/tick üzerinden sabit dilimlere dağıtılıyor; sonuç kamera veya platforma göre değişmiyor. 2–4 dilim kararları birkaç tick geciktirebilir.
- [x] Dilim periyodu/dağılımı, ayar sınırları, save/load ve aynı tohum+ayar determinizmi için `SimulationDetailLodTests` kaynakları eklendi.
- [ ] Unity derlemesi, testleri, gerçek kayıt turu ve performans ölçümü kullanıcı talimatıyla sona bırakıldı; I2 ve Faz I kabul edilmedi. Sıradaki iş I3 sıcak nokta profili/optimizasyonu; ölçüm tabanı olmadan kör optimizasyon yapılmayacak.

## 2026-10-07 Kullanıcı gözlemi · haydut nüfusu

- [x] Otomatik `dis.bandits` olayı 20. dünya yılı ve toplam 200 birimden sonra devreye girebilir; temel yıllık şans %3, bekleme 15 yıl ve Kan Çağı çarpanı ×2. Olay en büyük şehrin yakınına `pw.spawn_bandit` ile tek haydut yerleştiriyor.
- [x] `sp.bandit` canlı doğumla ürüyor; üreme kanunu açıksa yetişkin haydutlar da nüfusu artırabilir. Ekrandaki 31 sayısı yalnızca her olayın tek seferde çıkardığı grup büyüklüğü değil, birikmiş/doğmuş haydutlar olabilir.
- [ ] Kesin kaynak ayrımı için oyun testinde afet tarihçesi ve haydut doğumlarını zaman içinde izlemek açık; Unity testi/soak çalıştırılmadı.

## 2026-10-07 Faz I3 · birim AI yinelenen yakın araması

- [x] Var olan `Logs/fin3p-Perf.log` eski Editör profilinde 5.091→4.393 birim ve 10,601 ms/tick kaydetmiş; en pahalı sistem `UnitThinkSystem` (4,174 ms), sonra `UnitActSystem` (3,106 ms), `UnitLifeSystem` (1,758 ms). Güncel kaynak değişikliklerinden önce olduğu için yalnızca öncelik seçimine yarar.
- [x] Birim AI eş arama dalı 40 karo içindeki türdaş yokluğunu zaten ölçtüğünde sonraki sürü davranışı kontrolünde daha küçük 8 karo ve aynı 40 karo yakın aramasını tekrarlamıyor. Dalın önceki 40 karo araması boşsa atlanan kontroller aynı sonucu veriyor.
- [ ] Güncel önce/sonra PerfBench, Unity derleme/testi ve WebGL/Android Player karşılaştırması kullanıcı test aşamasına bırakıldı. Kazanç kanıtlanana dek optimizasyon kabul edilmedi; I3/Faz I açık.

## 2026-10-07 Faz I4 · yükleme ve birim sanat atlasları

- [x] 96 yüksek çözünürlüklü birim PNG'si korunarak 160×240 runtime atlasları üretildi: atlas toplamı 2.145.542 bayt, kaynak PNG'ler 196.136.016 bayt (187 MiB). Bu yalnızca birim görsellerinin paket içindeki PNG boyutudur; toplam indirme/RAM ölçümü değildir.
- [x] `DetailedUnitSheet` önceden küçültülmüş atlası yüklüyor ve yoksa eski kaynak dönüşümüne geri düşüyor. `DataSync` yalnızca runtime atlas/metaverisini kopyalayacak ve eski yüksek çözünürlüklü StreamingAssets kopyalarını bir sonraki editör Play/Build senkronizasyonunda temizleyecek.
- [x] UI sesleri dışındaki efektler açılışta topluca indirilmek yerine ilk kullanımında yükleniyor. Asenkron kayıt/yükleme loglarına bayt ve süre ayrımı, kayıt penceresine işlem durumu eklendi.
- [ ] Statik PNG/metaveri kontrolü yapıldı; Unity derleme/testi, Editor DataSync, gerçek yükleme süresi/bellek ve masaüstü/WebGL/Android Player doğrulaması kullanıcı final test aşamasına bırakıldı. I4 ve Faz I kabul edilmedi.

## 2026-10-07 Faz I5 · WebGL DPI ve mobil ölçek

- [x] Oyun sayfası Unity `devicePixelRatio` yapılandırmasıyla WebGL çizim çözünürlüğünü sınırlıyor: düşük kapasiteli mobilde 0,75, diğer mobilde 1,0, masaüstünde en fazla 2,0. `?renderScale=` kontrollü manuel ayar sağlar; canvas CSS alanıyla eşleşmeye devam eder.
- [x] Kaynak denetiminde Brotli + `.htaccess`, açılış yüzdesi, safe-area, landscape, mobil arka plan duraklatması, 30 FPS ve düşük doğa efektleri mevcut doğrulandı.
- [ ] Tarayıcı ve gerçek cihaz kabulü (keskinlik, oran/orientation, ısı/pil ve oyun süresi) kullanıcı final testine bırakıldı. I5 ve Faz I tamamlanmadı.

## 2026-10-07 Faz I6 · kalite kapısı ve GC tahsisi

- [x] Mevcut gate sırası EditMode, Graphics, diagnostics, PlaySmoke, çok tohumlu 200 yıllık soak ve PerfBench'i doğruluyordu; eksik CrowdProbe adımı eklendi ve 0/2/8/20/40 yıl raporlarını arıyor.
- [x] PerfBench her simülasyon tick'inde `GC.GetAllocatedBytesForCurrentThread` farkını ölçüp toplam ve en kötü tick baytını logluyor. Editor-batch eşiği tick başına 0 B; metrik eksikse gate başarısız olur. Sıfır eşiğinin gerçek koşuda geçip geçmediği bilinmiyor.
- [x] Tahsis eşiğinin 0, pozitif ve geçersiz girdilerini kapsayan EditMode test kaynağı eklendi.
- [ ] Gate, Unity testleri, soak ve benchmark kullanıcı talimatıyla çalıştırılmadı. Gerçek GC/performans sonucu ve I6/Faz I kabulü açık.

## 2026-10-07 Faz J3 · yerel hata kaydı ve tanı paketi

- [x] Oyun açılışında Unity Error/Assert/Exception, yakalanmamış .NET istisnaları ve gözlenmemiş Task istisnaları kayda alınır; tampon son 80 girdiyi, yerel `diagnostics.log` en fazla 512 KiB'yi tutar. WebGL'de Unity hata konsolu callback'i korunur.
- [x] Ayarlar > Tanılama paketinde sürüm, platform, tohum/yıl, nüfus ve sınırlı profil ayarları var; AI URL/model/anahtarı ve dünya adı dahil edilmez. Yaygın API/Bearer anahtarı, e-posta ve kullanıcı klasörü desenleri temizlenir. Telemetri veya otomatik gönderim eklenmedi.
- [x] Maskeleme için `DiagnosticsBundleTests` regresyon kaynağı eklendi; `p0_ui.py` çalıştırılarak TR/EN metin kataloğu üretildi (634 kayıt).
- [ ] Unity derleme/testi ve WebGL/masaüstü clipboard + yerel dosya doğrulaması kullanıcı testlerinin sonuna bırakıldı. Maskeleme yalnızca bilinen desenleri kapsar; J3 ve Faz J kabul edilmedi.

## 2026-10-07 Faz J4 · sanat/ses envanteri ve özellik atlası

- [x] Statik içerik eşlemesi: 92 türün tümünde runtime detay atlası var (toplam 96); 24/24 ağaç Final renklendirilmiş; başlangıçta bitkiler 3/9, maden/kaynak parçaları 1/10 ve ekinler 0/1 Final ayrı görsele sahipti.
- [x] Önceden placeholder kullanan 16 bitki/ekin/kaynak için tek özgün 4×4 saydam renk atlası `Art/Final/features/feature_resources_atlas.png` ve hücre eşlemesi `feature_resources_atlas.json` eklendi. `SpriteLibrary` mevcut Final `_color.png` görsellerine öncelik verip eksikleri atlas hücrelerinden küçük tam renkli sprite olarak yükler; `DataSync` JSON eşlemesini StreamingAssets'e kopyalar.
- [x] Atlas yüklemesi, 16 içerik kimliği, sprite boyutu ve mevcut `tree_oak` görselinin korunması için `FeatureArtAtlasTests` regresyon kaynağı eklendi. Testteki kaynak-temizleme kullanımı test çalıştırılmadan düzeltildi.
- [x] Ses kataloğu 61 girdi ve Audio klasörü 61 WAV içeriyor. Bu yalnızca dosya sayımıdır; tetik kapsamı, miks kalitesi, loop dinleme ve mobil hoparlör test edilmedi.
- [x] Kaynak tetik eşlemesinde katalogdaki `hit.ranged` ve `hit.magic` seslerinin çağrılmadığı bulundu. Simülasyon vuruş olayı konum ve darbe türünü taşır; yakın dövüş, AI menzilli saldırıları, ateş/toprak temelli büyülü darbeler AudioManager'a bağlandı. Ölüm sesi korunup her hasarda yakın dövüş sesi çalma kaldırıldı.
- [x] Darbe türü/konumu olay sözleşmesini kapsayan `CombatImpactAudioTests` regresyon kaynağı eklendi; test çalıştırılmadı.
- [x] `Tools/visual_compare.py` tek PNG veya eşleşen klasörleri ölçer: kanal fark eşiği, değişen piksel yüzdesi, ortalama/azami fark; isteğe bağlı eşik ve JSON raporu sunar. Eşik verilmezse rapor-only çalışır. Eski ekran görüntüleri otomatik onaylı baseline sayılmaz.
- [ ] Unity derlemesi/testi, gerçek oyun/telefon görünümü, ses tetik yollarının runtime kabulü, miks/loop dinleme, mobil hoparlör ve kullanıcı tarafından onaylı görsel baseline/karşılaştırma açık. Kullanıcı talimatıyla testler final aşamasına bırakıldı; J4/Faz J kabul edilmedi.

## 2026-10-07 Faz J5 · katalog kapsamlı yayın QA listesi

- [x] `Tools/generate_qa_checklist.py` Python AST sözdizimi kontrolünden geçti ve çalıştırıldı; `13_QA_Beta_Kontrol_Listesi.md` güncel katalogdan 247 güç, 8 senaryo ve 19 pencere için tek tek satır üretti.
- [x] Kontrol listesi WebGL masaüstü/Android dikey-yatay, Android Player ve Windows matrisi; kayıt/yedek/mahremiyet; güç ve senaryo doğrulaması; 1000 yıllık çok tohumlu soak; 8 saatlik oturum; beta geri bildirim formu ve P0–P3 sınıflandırması içeriyor. Ayrıntılı test stratejisi ve Faz J belgesine bağlandı.
- [ ] Kullanıcı Unity/oyun testini en sona bıraktığı için testler, soak, cihaz/Player kabulü ve dış beta yapılmadı. Katılımcılara mesaj/link gönderilmedi. J5 ve Faz J kabul edilmedi.

## 2026-10-07 Faz J6 · dağıtım kaynağı ve AAB hazırlığı

- [x] Unity Android geliştirme APK yolundan ayrı, imzalı Release AAB yolu eklendi. Yayın paketi kimliği ve keystore bilgileri `PG_ANDROID_*` ortam değişkenlerinden alınır; derleme tamamlanınca parolalar temizlenir. Android dağıtım yönergesinde API 25 minSdk / Auto targetSdk, kalıcı paket kimliği ve imza sorumlulukları kaynakla eşleştirildi.
- [x] Android ADB dağıtım aracı `PG_ANDROID_APP_ID` ile uygulamayı başlatabilir. Web oyun sayfasına SEO/Open Graph metaverisi eklendi; Apache/LiteSpeed ayarlarında Brotli `Vary`, sabit isimli Unity/CSS/JS kaynakları için yeniden doğrulama ve temel güvenlik başlıkları tanımlandı. Mağaza metni ve gerçek ekran görüntüsü çekim planı `14_Magaza_Sayfasi_Taslagi.md` içinde.
- [ ] Unity AAB/WebGL derleme, AAB imza doğrulaması, kurulum/cihaz duman testi, web paket doğrulaması ve güncel mağaza SDK koşulu kullanıcı final testine bırakıldı. Kalıcı uygulama kimliği henüz kullanıcı/dağıtım sahibi tarafından seçilmedi; gerçek ekran görüntüsü ve yayına hazır paket yok. J6/Faz J açık.

## 2026-10-07 Faz J7 · lisans, veri akışı ve mağaza beyanı taslağı

- [x] `CREDITS.md` kaynaklarda kullanılan Json.NET, Firebase JS SDK ve CDN fontlarını/lisanslarını kapsıyor; son paket ve transitif bildirimlerin yeniden taranması not edildi.
- [x] Kaynak kodu taraması Firebase e-posta/Google/misafir Auth, yerel kayıt ve tanı, kullanıcı tarafından seçilen AI endpoint'i ile Google Fonts/Firebase CDN ağ akışını ayırdı. `15_Gizlilik_ve_Kullanim_Taslagi.md` KVKK aydınlatma alanları, kullanım koşulları başlıkları ve yaş anketi hazırlığını içeriyor.
- [ ] Hukuk kimliği/iletişim ve saklama/aktarım süreleri kullanıcı/dağıtım sahibinden gelmedi; Firebase hesap silme akışı yok; nihai privacy/terms ve yaş derecesi beyanı yapılmadı. Gerçek build/SDK ve özgünlük insan denetimi de açık. J7/Faz J kabul edilmedi.

## 2026-10-07 Faz J8 · veri modu şema sürümleme kaynağı

- [x] Mevcut `ContentDB.LoadAll` harici mod klasörlerinin Data/Localization yamalarını yükleyip çözümleme/doğrulama zincirinden geçiriyor. `ModManifest.contentSchemaVersion` v1 alanı eklendi; alansız eski manifest v1 sayılır, bilinmeyen sürüm yükleme uyarısıyla atlanır.
- [x] Modlama belgesi mevcut davranışa göre düzeltildi: yalnız veri/localization modları; kod/DLL modları desteklenmiyor. JSON schema katalog yolu, geriye uyumlu alan politikası, QA/mağaza/hukuk taslakları ve `Gelistirici_Rehberi` yol haritası bağlantısı işaret edildi.
- [x] `Data/_schemas/mod_manifest.schema.json` manifest v1 biçimini tanımlar. `DECISIONS.md` #170 önceki #82'deki uygulanmamış `Audio/` mod desteği iddiasını açıkça geçersiz kılar.
- [x] Geri uyumlu manifest ve desteklenmeyen schema davranışı için `ModManifestTests` kaynağı yazıldı.
- [ ] Unity testleri, gerçek örnek mod yükleme, JSON Schema doğrulama entegrasyonu ve üçüncü taraf mod uyumluluk kabulü kullanıcı final test aşamasında. J8 ve Faz J kabul edilmedi.

## 2026-10-07 Faz H2 · duruma bağlı “Ne yapabilirim?” önerileri

- [x] HUD araç çubuğuna her zaman erişilebilir öneri penceresi eklendi. Mevcut şehir yiyecek/kıtlık ve konut ihtiyacını, aktif savaşı ve medeniyet varlığını okur; en fazla üç öneriyi önceliklendirir.
- [x] Öneriler ilgili şehir/savaş ayrıntısına götürür; dünyada medeniyet yoksa senaryo kısıtını denetleyerek insan bırakma gücünü seçer; acil durum yoksa tarihçeyi açar. Hiçbir öneri simülasyon verisini doğrudan değiştirmez. TR/EN metinleri kaynak üretecine ve kataloğa eklendi.
- [x] Öncelik/fallback davranışları için `StateSuggestionsTests` regresyon kaynakları yazıldı.
- [ ] Unity derleme/testi, ilk 10 dakika kullanılabilirlik gözlemi ve dokunmatik/masaüstü UI kabulü kullanıcı test aşamasına bırakıldı; testleri çalıştırmadım. H2/Faz H kabul edilmedi.

## 2026-10-07 Faz H3 · bağlama göre güç önerisi ve keşif

- [x] “Ne yapabilirim?” kartları öneri durumuna göre gerçek bir araca/güce geçiyor: yiyecek baskısında Bereket, konut baskısında İncele, savaşta Dünya Tarihi, medeniyet yoksa İnsan ve olağan durumda İstatistikler. Güç seçimi mevcut senaryo izin/kilit kontrollerini kullanıyor.
- [x] Başarımla açılan güçler `Profile.Discovered` içinde izleniyor; yeni güç bildirimi tıklanınca aracı seçiyor. Eski profiller kazanılmış başarımları açılışta uzlaştırıyor; keşfedilmemiş kilitli araç ipucu açılma başarımını ve koşulunu belirtiyor. Eşleşmeyen güç araması açık boş durum ve tek dokunuşla temizleme sunuyor; 44 px hedefler mevcut. Öneri kimliği ve güç keşif davranışı için `StateSuggestionsTests`/`PowerDiscoveryTests` kaynakları eklendi.
- [ ] Unity derlemesi/testi, güçlerin gerçek kilit açma bildirimi, dokunmatik arama ve cihaz kullanılabilirlik kabulü kullanıcı test aşamasına bırakıldı. H3/Faz H kabul edilmedi.

## 2026-10-08 Kullanıcı derleme hataları · koruma ve atlas testleri

- [x] `FeatureArtAtlasTests` ve `SpeciesProtectionTests` için editör `DataSync` tipi `PG.EditorTools` ad alanından görünür yapıldı; korunan tür indeksini `SetProtectedSpecies` metodunun istediği `ushort` türüne dönüştürdüm.
- [ ] Unity derlemesi ve EditMode testleri kullanıcı isteğiyle çalıştırılmadı; ad alanı ve metod imzası kaynakta karşılaştırıldı, derleme sonucu bekliyor.

## 2026-10-08 Faz F2c · taşkın ovası biyokütlesi

- [x] `FeatureGrowth` nehir karesinde doğal büyümeyi durduruyor; nehirin sekiz komşu karosundaki yiyecek kaynağı yenilenmesi ×1,35 oluyor. Biyomun yerdeki rezervi ve yiyecek bitkileri aynı yerel katsayıyı kullanıyor.
- [x] Kıyı/iç bölge katsayısı regresyon kaynağı `EcosystemTests` içine eklendi; karar `DECISIONS.md` #171 ve Faz F günlüğüyle kaydedildi.
- [ ] Unity derlemesi/EditMode ve uzun dönem biyokütle dengesi kullanıcı final test aşamasında. Geniş nehir/köprü ve otomatik sel yerleşimi sonraki F2d/F2e dilimlerinde uygulandı; F2/Faz F kabulü açık.

## 2026-10-08 Kullanıcı gözlemi · 171. yılda fauna baskınlığı ve uygarlık çöküşü

- [x] Ekran görüntüsünde toplam 170 birim: 148 papağan, 22 maymun; insan, elf, şehir ve yaşayan krallık görünmüyor. Önceki 118. yıl ekranında 3 elf vardı; aynı kayıt olduğu doğrulanmadığı için iki görüntü kesin zaman serisi sayılmıyor.
- [x] Kaynak denetimi: yıllık `AnimalSpawnSystem` yeni doğal grupları tür başına 30'da durduruyor, ancak `UnitLifeSystems.CapFactor` üremeyi bu sınıra bağlamıyor. `law.population_cap` kapalıyken tür başına üreme yavaşlatma eşiği 5.000 oluyor; papağanın 148'e çıkması yumurtayla üreme üzerinden mümkün. Papağan yumurtlamada 1–3 yumurta bırakıyor.
- [ ] Ekran görüntüsü eski oyun kaydından olduğu için insan/elflerin geçmiş ölüm nedeni geriye dönük bulunamıyor. Yeni soak raporuna yıllık doğum/ölüm nedeni ve tür demografisi eklendi; aynı kaydı temsil etmediğinden fauna dengesi ve medeniyet çöküşünün kabulü hâlâ açık.
- [x] Yumurta türü dişilerine başarılı yumurtlamadan sonra 10 aylık `st.breeding_cooldown` eklendi; kuluçka süresince `UnitCourtship` yeni eşleşmeyi reddediyor. Süre mevcut status save alanlarında tutuluyor; `st.pregnant` doğum işleyicisi kullanılmadı. Yeni statü status listesinin sonuna yazıldı; önceki 31 statünün kimlik sırasının korunduğu statik karşılaştırmayla doğrulandı.
- [x] `EcosystemTests` parrot dişi/erkek eşleşmesinin cooldown-öncesi, cooldown-sırası ve status kalktıktan sonraki durumlarını kapsıyor. Karar #172 ile yazıldı; `p2_units.py` JSON çıktısı yenilendi ve JSON sözdizimi doğrulandı.
- [ ] Unity derleme/testi, aynı save'de yüzyıllık fauna ve uygarlık soaku kullanıcı final testine bırakıldı; C fazı kabul edilmedi.

## 2026-10-08 Faz C0 · yıllık tür demografisi raporu

- [x] `SoakMetrics.Population` mevcut anlamıyla medeniyet nüfusu kalır; vahşi toplam için `WildPopulation` eklendi. `PopulationBySpecies` artık tüm yaşayan türleri içerir ve her türde medeniyet işareti, dişi/erkek/diğer cinsiyet, yetişkin/yavru dağılımı, yıllık doğum sayısı ve ölüm nedeni tutulur.
- [x] Medeniyet ve vahşi türlerin ayrı ayrı nüfus patlamasını okumak için `MaxWildSpeciesPopulation` eklendi. Soak karşılaştırma araçlarının mevcut nüfus alanı değişmedi; JSON regresyon test kaynağı bu yeni alanları kapsıyor.
- [x] `CivSoak` birim doğum/ölüm olaylarını metrik toplayıcısına bağlıyor; olaylar her yıllık örnekten sonra sıfırlanıyor ve o yıl doğup ölen türler nüfusu sıfır olsa da raporda kalıyor.
- [x] `NatureSoak` için yalnız ekosistem dalını çalıştıran menü komutu eklendi. `PG_WILD_YEARS` ve `PG_WILD_SEED` ile 200 yıllık ölçüm ve seed seçilebilir; her yılın raporu `Logs/metrics-wild-<seed>-<years>y.json` biçiminde yazılır. Varsayılan 40 yıl bırakarak mevcut `NatureSoak.Run` süresini büyütmedim.
- [x] Çok krallıklı kalite kapısı, `PopulationBySpecies` listesinin genişlemesinden sonra medeni tür sayısını `IsCiv` alanıyla filtreliyor; vahşi tür sayısı kabul koşulunu yanlışlıkla karşılayamaz.
- [x] `DECISIONS.md` #173, Faz C günlüğü ve changelog güncellendi.
- [ ] Unity derlemesi/testi ve 200 yıllık vahşi dünya çok tohumlu raporu kullanıcı final test aşamasında. Yeni metrikler henüz gerçek soak çıktısıyla doğrulanmadı; C0/C fazı açık.

## 2026-10-08 Faz D4 · iklim bağlantılı kıtlık ölçümü

- [x] `CivSoak`, toplam `FamineEvent` sayısının yanında `Cause` değeri `Drought` veya `Cold` olan olayları da topluyor. `ClimateLinkedFamineEvents` yıllık örneklerde ve özet metrikte yer alıyor; mevcut `FamineEvents` alanı korunuyor.
- [x] `SoakMetricsTests` yeni JSON alanını kapsıyor; karar #174, Faz D günlüğü ve changelog güncellendi. Hedef oran rapordan hesaplanabilir; işaretli olay sayısı tek başına iklim nedenselliği kanıtı değildir.
- [ ] Unity derlemesi/testi ve altı tohumlu 100 yıllık iklim/kıtlık kabulü kullanıcı final test aşamasında; D4 ve Faz D açık.

## 2026-10-08 Faz E1c–E1e · elçiler, barış şartları ve antlaşma ihlali

- [x] Krallık ilişkileri sekmesine, oyuncu kontrolündeki krallık için hedefe göre barış, ittifak, ticaret, saldırmazlık, ambargo ve vasallık teklifleri eklendi. Gelen teklifler aynı sekmeden vali olmadan kabul/ret edilebilir. Var olan ilişki/antlaşma veya aynı yöndeki bekleyen teklif düğme tekrarını engelliyor.
- [x] Teklifler `MetaCommand` kuyruğunu kullanıyor; hedefin kabulü mevcut diplomasi uygulamasını yürütüyor. `p0_ui.py` üreticisinden TR/EN metinler yenilendi; elçi ve barış pencerelerinin 16 anahtarı JSON'da doğrulandı.
- [x] Savaşta barış teklif düğmesi şart penceresi açıyor: altın, şehir devri, teklif verenin vasallığı ve saldırmazlık süresi seçiliyor. Teklif komut yolunda yinelenen barış teklifleri önleniyor.
- [x] AI barış kararı uygulanabilir altın/şehir/vasallık/saldırmazlık taviz puanını kullanıyor; mevcut savaş yorgunluğu ve yüksek görüş kabul eşikleri korunuyor.
- [x] `AllianceTests` manuel barış şartlarının MetaCommand üzerinden kayda taşınması ve vasallık taviziyle erken AI kabulü için regresyon kaynakları içeriyor; UI etkileşim testi eklenmedi.
- [x] E1e: Geçerli savaş komutu saldırmazlık antlaşmasını bozabilir; antlaşma kaldırılır, savunucu görüşü 30 düşer ve savaş sebebi `TreatyBreach` olur. UI eylem adı ihlal maliyetini belirtir; AI'ın otomatik savaş seçimi aktif saldırmazlığı ihlal etmez.
- [x] `AllianceTests` geçersiz savaş türünde antlaşma/görüşün korunmasını ve geçerli ihlalde antlaşma kaldırma, −30 görüş ve savaş sebebini kapsıyor.
- [x] Gönderilen yıl 171 ekranı önceki fauna bulgusuyla eşleşiyor: 170 canlı birimin 148'i papağan ve 22'si maymun; yaşayan insan/elf/krallık yok. Güncel kaynakta `SpawnCivs=true`, yeni dünya penceresinde ayarlanabilir ve `GameBootstrap` sandbox başlatırken `CivStartSpawner`'ı çağırıyor. Dolayısıyla bu görüntü mevcut yeni-dünya varsayılanından önceki/eski bir kayda ait olabilir; aynı save doğrulanmadığından medeniyetlerin önceki ölüm nedeni görselden belirlenemez.
- [ ] Unity derlemesi/EditMode, gerçek oyun arayüzü, aynı kayıtta nüfus seyri ve çok krallıklı soak kullanıcı final test aşamasına bırakıldı. E1 ve Faz E kabul edilmedi.

## 2026-10-08 Faz E6a · vali diplomasi sözleşmesi

- [x] Kaynak denetiminde dört antlaşma eyleminin (`ProposeTrade`, `ProposeNonAggression`, `ProposeVassalage`, `ProposeTradeEmbargo`) JSON parser → validator → `MetaCommand` eşlemesi mevcut bulundu; tamamlanmayan bölüm test kapsamı ve model yönergesiydi.
- [x] `GovernorTests` dört eylemi tek senaryoda parse edip doğru komutlara eşleyecek regresyon kaynağı içeriyor. Test kullanıcı talimatıyla çalıştırılmadı.
- [x] Vali promptu aktif saldırmazlık statüsünün savaşla bilinçli bozulduğunu, antlaşmanın kalktığını ve −30 görüş maliyetini açıklıyor. `Docs/10_Krallik_Valisi.md` ambargo eylemi ile vasal savaş yasağını da kapsıyor.
- [ ] Unity derlemesi/EditMode ve gerçek AI sağlayıcı yanıtı final test aşamasında; E6 ve Faz E kabul edilmedi.

## 2026-10-08 Faz G7a · kayıtlı dünya üretim tarifi

- [x] Kayıt biçimi v1'e isteğe bağlı `SectionWorldRecipe=19` eklendi; format sürüm numarası sabit kaldı. Worldgen codec'i ayarları v1 JSON zarfında saklıyor, aralıkları normalize ediyor ve özel yerel görsel şablonlarını reddediyor.
- [x] `GameBootstrap` kayıt ve undo snapshot'larında üretim tarifini kaydediyor; yüklemede çözüp güncelliyor. Eski kayıtta tarif alanı yoksa mevcut tarif durumunu temizliyor; bozuk veri uyarı verip paylaşımı devre dışı bırakıyor.
- [x] `DeterminismTests` içine normal tarif round-trip, eksik eski bölüm, bozuk JSON ve özel görsel reddi için regresyon kaynakları eklendi. Karar #179, Faz G günlüğü ve changelog güncellendi.
- [ ] Unity derleme/EditMode, WebGL ve masaüstü gerçek save/share akışı kullanıcı final test aşamasında; çalıştırılmadı. G7 ve Faz G kabul edilmedi.

## 2026-10-08 Faz H1a · isteğe bağlı başlangıç uygarlıkları

- [x] Önceden tanımlı ama kullanılmayan `SpawnCivs` ayarı yeni dünya penceresine bağlandı. Varsayılan açık; sandbox üretiminde her `IsCiv` türünden sekiz yetişkin (4 dişi, 4 erkek) deterministik seed-derive akışıyla uygun kara kümesine yerleşiyor.
- [x] Senaryo seçimi uygarlık seçeneğini devre dışı bırakıyor; aktif senaryo yalnız kendi tanımlı başlangıç nüfusunu üretir. TR/EN UI anahtarı `p0_ui.py` üzerinden `ui_strings.json` kataloğuna üretildi.
- [x] `ScenarioTests` dengeli yetişkin çiftleri ve her uygarlık türünün sayısını doğrulayan regresyon kaynağı içeriyor. Karar #180, Faz H ve changelog kaydedildi.
- [ ] Unity derleme/EditMode ve aynı sandbox kaydında 100+ yıl nüfus/kıta yerleşimi kabulü kullanıcı final aşamasında; çalıştırılmadı. H1 ve Faz H kabul edilmedi.

## 2026-10-08 Faz H1b · senaryo koşulu regresyon kapsamı

- [x] `ScenarioTests` hedef eşiği öncesini, kazanma anını, kayıp eşiğini, kazanma/kaybetme koşul metriklerini ve terminal sonucun daha sonraki ölçüm değişimlerinde korunmasını kapsıyor.
- [x] Katalogdaki tüm senaryoların goal/failure koşulları için desteklenen metrik, geçerli `atLeast`/`atMost` operatörü ve negatif olmayan hedef doğrulaması eklendi.
- [ ] Test/Unity çalıştırması, sekiz senaryonun gerçek oyun oynanabilirliği ve otomatik kazanma/kaybetme soak kanıtı kullanıcı final aşamasında; H1/Faz H açık.

## 2026-10-08 Faz H1c · katalog senaryoları terminal değerlendirmesi

- [x] Sekiz senaryonun her birinde hedef metrikleri fixture üzerinden karşılanınca değerlendiricinin `Won` verdiğini; her kayıp koşulu ayrı fixture'da sağlanınca `Lost` verdiğini doğrulayan kaynak testleri eklendi.
- [x] Offline derleme 12 üretim + EditMode assembly için 0 hata verdi. Unity Test Runner ve gerçek simülasyonda doğal hedef erişimi/oynanabilirlik çalıştırılmadı.
- [ ] Senaryo denge, doğal kazanma-kaybetme ve kullanıcı oyun kabulü H1/Faz H için açık.

## 2026-10-08 Faz H4a · undo maliyet telemetrisi kaynak denetimi

- [x] Undo snapshot'ının kullandığı `SaveSystem.SaveFileAsync` loglarının dosya adıyla `undo_` kaynağını ayırt ettiği; snapshot ms, sıkıştırma+disk IO ms, toplam ms, byte, harita boyutu ve birim sayısını yazdığı doğrulandı.
- [x] H4 belgesindeki kaynak açığı düzeltildi: telemetri kodda mevcut; ek ölçüm implementasyonu gerekmiyor.
- [ ] Gerçek performans örnekleri ve masaüstü/WebGL/Android maliyet kabulü kullanıcı final testinde; H4/Faz H açık.

## 2026-10-08 Faz H2a · öğreticide eylem tabanlı nüfus ilerlemesi

- [x] Yaratık/uygarlık öğretici adımları mevcut dünya nüfusuna göre otomatik atlanmıyor; adım başında alınan ilgili tür nüfus tabanının üstüne çıkmayı ve doğru güç sekmesini gerektiriyor.
- [x] Yeni dünya veya yüklenen SimWorld değişince yarım kalmış rehber baştan kuruluyor. Ayarlar sıfırlanınca `TutorialDone` yeniden false olduğunda rehber de 0. adımdan yeniden başlıyor.
- [x] Kare başına çalışan kategori sayımı tahsis yapan iki `Any` lambda'sından düz döngüye geçirildi. `TutorialProgressTests` sekme uyumsuzluğunu, artış yokluğunu ve başarılı iki sekme durumunu kapsıyor.
- [x] Şehir inceleme ve kanun penceresi sayaçları da adım başlangıcında yakalanıyor; daha önceki oturum/rehber eylemleri bu adımları otomatik tamamlamıyor. Test kaynağı sayaç artışı sınırlarını kapsıyor.
- [x] Doğa/Yıkım adımı yalnızca ilgili sekmeden adım açıldıktan sonra güç uygulandığında ilerliyor; eski son kullanılan sekme değeri yeterli değil. Güç uygulama sayacı, mevcut UI güç girdisi yolundan tutuluyor ve regresyon kaynağıyla kapsandı.
- [ ] Unity/EditMode, dokunmatik/masaüstü gerçek akış ve ilk 10 dakika kullanılabilirlik gözlemi kullanıcı final test aşamasında; H2/Faz H açık.

## 2026-10-08 Ekran görüntüsü incelemesi · vahşi tür dağılımı

- [x] Yıl 171 görüntüsünde 170 toplam birimin 148 papağan ve 22 maymun olduğu doğrulandı; ekranda insan/elf ya da krallık görünmüyor. Görüntü tek başına bu türlerin hiç başlatılmadığını mı yoksa sonradan yok olduğunu mu kanıtlamıyor.
- [x] Kaynakta doğal hayvan grup sınırı 30 yalnızca yıllık dışarıdan spawn'ı durduruyor; üreme sınırı değil. Üreme yasası açıkken yumurtlayan türler her başarılı yumurtlamadan sonra 10 ay bekliyor. Tür başı yumuşak fren `max(200, aktif_nüfus_tavanı × 0.25)`; nüfus tavanı yasası kapalıyken 20.000 birimlik güvenlik sınırından türeyen eşik 5.000, dolayısıyla 148 papağan bu frene yaklaşmıyor. Görüntüde oran yüksek görünse de bunun patlama veya taşıma kapasitesi ihlali olduğu kanıtlanmış değil.
- [ ] Aynı kayıt/tohumla yıllık doğum, ölüm, tür ve besin raporları alınmadan papağan payının nedeni veya insan/elf yokluğunun nedeni kesinleştirilmeyecek. Çok tohumlu ekosistem kabulü ve tür taşıma kapasitesi ayarı kullanıcı final doğrulamasına açık; C fazı kabul edilmedi.

## 2026-10-08 Ekran görüntüsü takibi · 118. yıldan 171. yıla olası nüfus düşüşü

- [x] Yeni karede yıl 171, toplam nüfus 170 ve iki tür görünüyor: papağan 148, maymun 22. Önceki yıl 118 ekranında 312 nüfus, sekiz tür ve üç elf vardı. Yıl farkı bir ilerleme izlenimi veriyor; save/tohum kimliği ekranlardan doğrulanmadığı için bunları kesin aynı zaman serisi saymıyorum.
- [ ] Eğer aynı kayıtsa 53 yılda toplam nüfusun 312'den 170'e, tür sayısının 8'den 2'ye düşmesi beklenen kararlı çeşitlilik değil; ancak görüntüler nedenini veya medeniyetin hiç kurulup kurulmadığını göstermez. Aynı save'den yıllık doğum/ölüm nedeni raporu olmadan ölüm nedeni çıkarılmayacak. Önceki civ üreme sınırı düzeltmesi bu eski save'i geriye dönük canlandırmaz; uygarlık başlangıç seçeneği yeni dünyalar içindir.

## 2026-10-08 Faz J7 · Firebase hesap silme akışı

- [x] Web hesap alanında oturum sahibi kullanıcı için geri alınamaz Firebase Auth silme düğmesi, açık onay, işlem durumu ve hata açıklaması eklendi. Firebase `requires-recent-login` hatası yeniden giriş yönlendirmesine çevriliyor.
- [x] Web/oyun kaynaklarında Firebase Firestore/Realtime Database kullanımını bulmadım; oyun oturum açma için Auth kullanıyor, yerel save'i silme işlemi etkilemiyor. UI bu sınırı işlemin öncesinde ve sonrasında belirtiyor.
- [x] `auth-actions.test.cjs` silme, etkin kullanıcı yokluğu ve Firebase yeniden giriş hatası için yazıldı. Kullanıcı testleri sona bırakma talimatıyla çalıştırılmadı.
- [ ] Gerçek Firebase yapılandırmasıyla hesap silme ve yeniden giriş hata akışı, yasal metin ve mağaza beyanı kabulü final aşamasında; J7/Faz J açık.

## 2026-10-08 Faz J8 · mod manifesti runtime şema doğrulaması

- [x] `DataSync` yalnız üst seviye içerik JSON'larını değil `_schemas/*.json` dosyalarını da StreamingAssets'e aynalıyor; WebGL dosya manifesti bunları kapsıyor.
- [x] `ContentDB` manifest şemasını `ContentFiles` ile okuyup `ModManifest.Order` hattına veriyor. Desteklenen `type`, `required`, `properties`, `additionalProperties`, `pattern`, `minLength`, `const`, `items`, `uniqueItems` kuralları mod okunmadan önce uygulanıyor.
- [x] Geçersiz mod uyarıyla atlanıyor; bozuk/eksik/desteklenmeyen şema manifestli modları güvenli biçimde kapatıyor ama temel oyun yüklemesini sürdürür. Manifesti olmayan eski klasör tabanlı mod davranışı korunuyor.
- [x] `Examples/Mods/example.world_notes`, ana menü etiketi üzerinden kurulumu görünür kılan, çalıştırılabilir biçimde paketlenmemiş localization-only mod örneği sunuyor. Klasör kopyalama/kaldırma talimatı ve test kaynağı eklendi.
- [x] `ModManifestTests` için gerçek depo şemasına bağlı güncel/bozuk manifest, bilinmeyen alan, hatalı kimlik, yinelenen bağımlılık, yanlış tür ve örnek mod manifesti regresyon kaynakları eklendi. Karar #181, mod belgesi ve changelog güncellendi.
- [ ] Unity derleme/EditMode, WebGL/Android StreamingAssets ve gerçek örnek mod uyumluluğu kullanıcı final test aşamasında; bu kontroller çalıştırılmadı. J8/Faz J açık.

## 2026-10-08 Yol haritası devamı · güncel çevrimdışı derleme

- [x] Güncel çalışma ağacında `python Tools/compile_check.py` yeniden çalıştırıldı: 12 üretim assembly'si ve `PG.Tests.EditMode` assembly'si 0 derleme hatasıyla derlendi.
- [ ] Bu araç Unity Test Runner'ı çalıştırmaz; EditMode/PlayMode testleri, soak, grafik ve cihaz kabulü kullanıcının son doğrulama aşamasına bırakıldı. A–J fazları bu nedenle kabul edilmiş sayılmıyor.

## 2026-10-08 Faz I0 · Player kare ölçüm kaplaması

- [x] F3 geliştirici kaplamasına sabit boyutlu son 120 kare örnek penceresi eklendi; ortalama kare süresi/FPS, en yavaş kare, hedef FPS, Player/Editor/build durumu, platform, cihaz, grafik API'si ve çözünürlük görünür.
- [x] `RollingFrameWindowTests` ilk pencere hesabı, hatalı örneklerin atılması ve 120 örnek sonrası en eski karenin çıkarılması için regresyon kaynağı içeriyor. Test çalıştırılmadı.
- [x] Offline `python Tools/compile_check.py`: 12 üretim assembly'si ve EditMode assembly'si 0 derleme hatası verdi; bu Unity Test Runner sonucu değildir.
- [ ] WebGL/Android/masaüstü Player'da F3 görünümü ve 10 dakikalık FPS/ısı/bellek ölçümü, kullanıcı final doğrulamasında. Bu kaynak değişikliği gerçek Player benchmarkı veya Faz I kabulü değildir.

## 2026-10-08 Faz B · küçük insan/elf kurucu topluluğu tanısı

- [x] `CivSoak` için `founders` senaryosu eklendi: ayrı konumlarda 7 insan ve 7 elf, her türde 4 dişi/3 erkek, yetişkin ve kur yapabilecek yakın kümeler. 100 yıl log'unda iki türün nüfusu, zirvesi, doğumu ve son sayısı görünür.
- [x] `Tools/founder_soak.ps1` altı seed için başlangıç cinsiyet dağılımı, her iki türde en az bir doğum ve 100. yılda yaşayan nüfus kabulini kontrol edecek. Henüz çalıştırılmadı.
- [x] Offline `python Tools/compile_check.py`: 12 üretim assembly'si + EditMode assembly 0 hata; `civ_ab.ps1` ve `founder_soak.ps1` PowerShell AST ayrıştırması temiz. Hiçbir test/soak çalıştırılmadı.
- [ ] Unity Test Runner ve altı tohum × 100 yıl founder soak kullanıcı final testine bırakılıyor. Faz B kabulü açık.

## 2026-10-08 Faz J4 · güncel sanat ve ses envanteri denetimi

- [x] Salt okunur runtime sanat denetimi 738/738 bina anahtarını altı stilin tamamında final/atlas görseliyle, 44/44 dünya özelliğini final/atlas görseliyle çözdü; placeholder/fallback ve çözümsüz anahtar yok.
- [x] Canlı sanat denetimi 96 türün 2.304 pozunu, yürüme karelerini, palet aralıklarını ve atlas oluklarını geçti. Ses denetimi 61 WAV'ın bağlarını, PCM biçimini, tepe payını, uçlarını, loop birleşimlerini ve 10 müzik çiftini geçti.
- [ ] Bunlar statik kaynak denetimleridir; Unity runtime, oyun içi boyut/örtüşme, gerçek ses miks/dinleme ve cihaz kabulü yerine geçmez. J4/Faz J açık.

## 2026-10-08 Faz F1b ve nüfus ekranı kontrolü

- [x] `WorldGenTemplateValidationTests` regresyon kaynağı eklendi; warp genliği/frekansı için mod override sınırlarını kapsıyor. Unity testi kullanıcı final aşamasına bırakıldı.
- [x] `python Tools/compile_check.py` üretim ve EditMode assembly'lerini 0 derleme hatasıyla derledi. Bu EditMode testlerini çalıştırmaz.
- [x] Kullanıcının yıl 171 ekranı kontrol edildi: toplam 170 canlı, 148 papağan ve 22 maymun; insan/elf/krallık yok. Önceki yıl 118 görüntüsü aynı kayıtsa elflerin de arada yok olduğu görülür; ekranlardan ölüm sebebi çıkarılamaz.
- [ ] Nüfus dengesinin nedeni için aynı save'deki tarih/ölüm kayıtları ve türlerin başlangıç dağılımı gerekir. Mevcut kare tek başına üreme/yiyecek sorununun hangisi olduğunu kanıtlamaz; C fazı kabulü kullanıcı final doğrulamasına açık.

## 2026-10-08 C5 ekran bulgusu kaynak kontrolü

- [x] Tür tükenme takibinin alt tür kayıtlarına dayanması şüpheli göründü; birim üretim yolu incelendi. `UnitWorld.Spawn`, doğal spawn dahil eksik alt türü `ResolveSubspecies` ile kök kayda bağladığından `AnimalSpawnSystem.TrackSpeciesRisk` geçmiş tür varlığını görebiliyor. Bu yol için kod değişikliği gerekmedi.
- [ ] Ekrandaki insan/elf yokluğunun sebebi hâlâ yalnız save tarihçesi ve ölüm nedenleriyle doğrulanabilir; Unity/soak çalıştırılmadı.

## 2026-10-08 Yol haritası · faz kabul listesi hazırlığı

- [x] `Tools/generate_qa_checklist.py`, A–J fazları için ayrı kabul satırları ve yol haritasındaki ölçü/tohum/hedefleri üretir: EditMode/determinizm, ekonomi, vahşi ekosistem, iklim, çok krallıklı dünya, worldgen, tarih, oyuncu deneyimi, Player performansı ve uzun soak.
- [x] `Docs/Yol_Haritasi/13_QA_Beta_Kontrol_Listesi.md` katalogdan yeniden üretildi: 247 güç, 8 senaryo, 20 pencere ve yeni faz ölçüm tablosu.
- [ ] QA maddeleri yalnızca son testte gerçek rapor/cihaz/oyun kanıtıyla işaretlenebilir. Unity testi, soak, build veya cihaz testi bu hazırlık sırasında çalıştırılmadı.

## 2026-10-08 Ekran takibi ve vahşi yaşam tanı komutu

- [x] Yeni ekran, önceki 171. yıl görüntüsüyle aynı sayıları gösteriyor: 170 canlı; 148 papağan, 22 maymun; görünür insan/elf/krallık yok. Ekran tek başına aynı kayıt/tohum olduğunu veya ölüm nedenini kanıtlamıyor.
- [x] Üreme akışı incelendi: papağan yumurtlayan otçul; doğurganlığı yerel biyokütle ve yakın tür yoğunluğu sınırlar. Toplam birim sayısı tek başına taşıma kapasitesi ihlali göstermez. Eski save'de civ hiç başlatılmadıysa `SpawnCivs` seçeneği onu geriye dönük oluşturmaz.
- [x] `run_unity.ps1 -Mode WildEcosystemSoak` ve 6 tohum/200 yıl JSON doğrulaması yapan `Tools/wild_soak.ps1` eklendi. Ölçüm hâlen koyun-kurt tanı çiftini başlatır; tam C kabulü (başlangıç türlerinin ≥%85'i, patlama ve bitki örtüsü) için tek başına yeterli değildir.
- [ ] Kullanıcı isteğiyle Unity/test/soak çalıştırılmadı. Ekrandaki kayıt için gerçek doğum/ölüm ve save kimliği alınmadığından kök sebep açık; C fazı da kabul edilmedi.

## 2026-10-08 Faz C0 · başlangıç tür nüfusu ölçümü

- [x] `SoakMetricsReport.InitialPopulationBySpecies`, simülasyon başlamadan önceki yaşayan tür ve cinsiyet sayımlarını saklıyor. `CivSoak` ve `NatureSoak.Ecosystem` bunu ilk tick'ten önce kaydediyor; yıllık örneklerle karışmıyor.
- [x] `Tools/wild_soak.ps1`, ilk tür sayımına göre son yıl tür hayatta kalma oranını hesaplayıp raporluyor. Ölçüm hâlâ yalnızca koyun-kurt çiftidir; tam toplulukta tüm tür yüzdesi ve bitki örtüsü kriterlerini henüz karşılamaz.
- [x] `SoakMetricsTests` pre-tick tür sayımını JSON'a yazma davranışını kapsayan regresyon kaynağı içeriyor.
- [x] PowerShell iki çalıştırıcısının AST sözdizimi kontrolü geçti; `git diff --check` temiz. İlk offline derleme `EntityId` ad çakışmasını buldu, açık namespace alias'ıyla giderildi; tekrar derlemede 12 üretim + EditMode assembly 0 hata verdi.
- [ ] Unity Test Runner, 6×200 yıllık soak ve tam vahşi tür/bitki örtüsü kabulü kullanıcı final testine bırakıldı. C fazı açık.

## 2026-10-08 Faz C0 · doğal vahşi topluluk ve bitki örtüsü ölçümü

- [x] `Wild Ecosystem Soak` artık el ile ekilmiş koyun-kurt tanı çiftine değil, `SpawnInitialAnimals()` ile biyomların doğal vahşi topluluğuna başlıyor; orta ölçekli harita, seed ve yıl sayısı seçilebilir. Genel `Nature Soak` ve `Unit Soak` içindeki ikili tanı yolu korundu.
- [x] JSON raporu başlangıç ve yıllık bitki/ağaç örtüsü tile sayısı ile yüzde kapsamasını yazıyor; ölü ağaç kapsam dışı. `CivSoak` da başlangıç çevre ölçümünü alıyor.
- [x] `wild_soak.ps1` altı seed için topluluk raporu dosyasını doğrulayacak ve tür hayatta kalması (%85), başlangıç payına göre 3× tür yoğunluğunun 50 yıl sürmesi ve bitki örtüsü değişimi (±%25) eşiklerini hesaplayacak şekilde güncellendi.
- [x] Kullanıcının gönderdiği `DataSync` ve `ushort` hataları mevcut kaynakta tam nitelikli çağrı/açık dönüşümle giderilmiş durumda. Offline `compile_check.py` üretim ve EditMode assembly'lerinde 0 hata verdi; çalıştırıcıların PowerShell AST kontrolü temiz.
- [ ] Unity Test Runner ve 6×200 yıllık topluluk soak'u kullanıcı final testine bırakıldı. Avcı-av salınımının sönümlenmesi ve 5.000 birim performans kriteri bu çalıştırıcıda henüz otomatik kabul edilmiyor; C fazı açık.

## 2026-10-08 Faz C0 · av-avcı eğrisi ölçümü

- [x] Yıllık vahşi metrikler `species.json` içindeki çözümlenmiş `prey` ilişkilerinden avcı ve av rollerini belirleyip nüfus toplamlarını JSON'a yazıyor; tür satırları da rollerini taşıyor.
- [x] Altı tohumlu rapor ilk ve son 50 yılın avcı/av nüfus aralıklarını normalize edip sönümlenme göstergesi olarak yazdırıyor. Bu yalnızca tanı göstergesidir; gerçek dalga çıktısını görmeden otomatik kabul/ret vermiyor.
- [x] Yeni regresyon kaynakları kurt-tavşan içerik ilişkisinden avcı/av rollerinin ve yıllık toplamların JSON'a yazılmasını kapsıyor. Offline derleme: 12 üretim + EditMode assembly, 0 hata; PowerShell AST temiz.
- [ ] Kullanıcı Unity Test Runner ve 6×200 yıllık soak'u son aşamada çalıştıracak. 5.000 birim performans eşiği ile çöküş-yeniden çıkış davranışı ayrıca gerçek soak çıktısıyla değerlendirilmeden C kabul edilemez.

## 2026-10-08 Faz C · 5.000 birim performans kapısı

- [x] `UnitPerf` artık 200 tick ısınma + 600 ölçüm tick'i için hedef/mevcut nüfus aralığını, birim sistemleri ortalama/en kötü tick süresini ve işlemciyi `Logs/metrics-unit-perf-5000.json` dosyasına yazıyor; ortalama 0,5 ms/tick'i veya nüfus %95 alt sınırını aşarsa başarısız dönüyor.
- [x] `Tools/run_unity.ps1 -Mode UnitPerf` ve altı seed'li `wild_soak.ps1` entegrasyonu eklendi; çok tohumlu çalıştırma fauna ölçümünden sonra ayrı performans koşusunu yapıp raporun güncelliğini ve hedef nüfus/tick sayısını doğrulayacak.
- [x] Rapor serileştirme testi kaynağı eklendi. Offline derleme (12 üretim + EditMode assembly) ve üç PowerShell AST kontrolü 0 hata verdi.
- [ ] Kullanıcı talimatıyla Unity çalıştırılmadı; ölçüm sonucu yok. Editör makinesindeki gerçek performans sonucu ve 50 yıllık çöküş-yeniden çıkış analizi alınmadan Faz C kapanamaz.

## 2026-10-08 Faz C · 50 yıllık çöküş/toparlanma tanısı

- [x] Altı tohumluk betik önceki 20 yılın tepe vahşi nüfusunun %25'ine düşen yılları çöküş olarak sayıyor; aynı tepenin %75'ine 20 yıl içinde çıkanları toparlanma, çıkmayanları toparlanmamış bölüm olarak raporluyor.
- [x] Bir seed'de 50 veya daha fazla ardışık düşük nüfus yılı otomatik başarısızlık sayılıyor. Tanım ve sınırlar Faz C günlüğünde açıklandı; gerçek soak koşusu yapılmadı.
- [ ] Gerçek 6×200 yıllık eğriler, tür sönümlenmesi ve 5.000 birim performansı kullanıcı final aşamasında ölçülecek; sonuçlar gelmeden C tamamlanmış değil.

## 2026-10-08 Faz D · eşleşmiş iklim kontrolü ve maliyet ölçümü

- [x] `CivSoak` aynı seed'le değişken iklim veya test amaçlı sabit iklim kontrolü çalıştırabiliyor. Sabit kontrol başlangıç sıcaklık/nem/kuraklık değerlerini her ay, iklim fazından sonra geri kuruyor; mevsim, bulut ve otomatik afetler kapalı.
- [x] Özet JSON iklim sistemlerinin tick ve ay başı maliyetini ayrı yazıyor; sabit kontrol sistemi ölçümden hariç. Ay hesabı `TicksPerMonth=60` ve 12 ay/yıl üzerinden.
- [x] `Tools/climate_ab.ps1` aynı altı tohumu 100 yıl iki koşulda çalıştırıp iklime bağlı kıtlık payı ≥%50, değişken iklimde kıtlık sayısı seed sapması, son uygarlık nüfusu ≥200 ve iklim maliyeti ≤0,3 ms/ay koşullarını değerlendirecek. ClimateAgriculture/SoakMetrics test kaynakları genişletildi.
- [x] Offline derleme 12 üretim + EditMode assembly'de 0 hata verdi; üç Unity PowerShell çalıştırıcısı AST ile ayrıştırıldı. Unity Test Runner ve soak çalıştırılmadı.
- [ ] İklim bağlantısı artık kuraklık/soğuk ile doğrulanmış sel kaynaklı ürün kaybını sayıyor. Gerçek 6×100 yıl sonuçları ve Unity kabul testleri alınmadan D fazı açık.

## 2026-10-08 Faz D · selden kıtlığa tarımsal iz

- [x] `WorldObjects.Submerge`, yalnız gerçek şehir tarlası veya mahsul feature'ı suya çevrilirken şehir için 24 aylık ürün kaybı hafızası oluşturuyor. Tarımsal kayıp olmayan su baskını kıtlık iklim nedeni sayılmıyor.
- [x] `FamineCause.Flood`, şehir ekonomisinin üç aylık kıtlık olayına ekleniyor. Hafıza, ham `CityData` düzenini değiştirmeden Civ kayıt bölümüne isteğe bağlı `FLD1` uzantısıyla yazılıyor; uzantısız eski kayıtlar boş varsayılanla okunuyor.
- [x] `ClimateAgricultureTests` gerçek tarla seli/neden, ekilmemiş arazide neden yokluğu, sürenin dolması, kayıt turu ve eski uzantısız save geriye uyumluluğu için regresyon kaynakları içeriyor. İklim A/B kapısının neden mesajı sel kaynaklı ürün kaybını da kapsıyor.
- [x] `python Tools/compile_check.py` üretim ve EditMode assembly'lerini 0 hatayla derledi; `climate_ab.ps1` PowerShell AST kontrolü ve değişen dosyaların `git diff --check` denetimi temiz.
- [ ] Unity EditMode, oyun içi kıtlık akışı ve 6×100 yıllık iklim A/B kullanıcı son test aşamasında. D fazı kabul edilmedi.
## 2026-10-08 Faz E0b · diplomasi kabul telemetrisi

- [x] Çok krallıklı soaku gerçek trade delivery event'lerini (kervan/deniz), kabul edilen antlaşmaları, her krallığın savaşa katılımını, vasallık/isyanı, krallık nüfus payı/çöküşünü ve Meta fazı maliyetini raporluyor.
- [x] `Tools/kingdom_soak.ps1` altı tohum, 300 yıl, eşzamanlı dört krallık, diplomasi olayları, 50 yıl savaş üst sınırı, çalışan ticaret, 80% nüfus hâkimiyetinde kayıtlı çöküş ve 0,3 ms/tick eşiklerini denetliyor.
- [x] `SoakMetricsTests` yeni JSON metrik alanlarının kaynak regresyonlarını içeriyor; `python Tools/compile_check.py` 12 üretim + EditMode derlemesini 0 hatayla bitirdi, kabul betiği PowerShell AST kontrolünü geçti.
- [ ] Unity EditMode/PlayMode, altı tohumlu 300 yıl soaku, kıtlık ithalatı eşleştirmesi ve hakimiyet sonrası tarihçe/çöküş kabulü kullanıcı final aşamasında; Faz E açık.

## 2026-10-08 Faz E4 · ticaret ithalatı ve kıtlık eşleştirmesi

- [x] CivWorld tanı anahtarı varsayılan açık; şehir tüccarları ve deniz ticareti kapalı koşulda rota açmıyor, aktif yükü kaynağa geri döndürüyor. Anahtar kayıt formatına eklenmedi.
- [x] `Tools/trade_famine_ab.ps1` aynı en az altı seed'i ticaret kapalı/açık çok krallıklı koşuda çalıştırıp nüfus-yılı başına kıtlık olaylarını ve teslimatları karşılaştıracak; en az üç teslimatlı eşleşme ve daha düşük açık ticaret kıtlık oranı PASS koşulu.
- [ ] Betik ve Unity Test Runner kullanıcı final testine bırakıldı. Offline derleme/sözdizimi kontrolü tamamlanmadan E4 kabul edilmez.

## 2026-10-08 · Kullanıcı gözlemi: medeniyetlerin uzun vadede yok olması

- [x] Kur yapma akışındaki neden bulundu: medeniyet eşleşmesi iki tarafın üreme isteği bitini eşzamanlı şart koşuyor; karşılıklı görev verilen partnerin kendi isteği yoksa UnitAct görevi iptal ediyordu. Eşlerden biri başlatabiliyor ve kur süresince karşılıklı göreve atanmış partner devam edebiliyor.
- [x] `UnitCourtshipTests` tek taraflı başlangıç isteğiyle kurun tamamlanması regresyonunu kapsayacak şekilde güncellendi.
- [ ] Unity EditMode ve kullanıcının mevcut kaydında/aynı seed'de nüfus eğrisi, ölüm nedenleri ve şehir/krallık kuruluşu kontrol edilecek. Kod düzeltmesi, ekrandaki tüm yok oluş nedenlerinin çözüldüğünü tek başına kanıtlamaz.

## 2026-10-08 Faz B/E · uygarlık çöküşü kabul ölçümü

- [x] `kingdom_soak.ps1` yıllık medeniyet nüfusunu izleyerek son yılda sıfır uygarlık birimini ve en az 20 kişilik tepe sonrası %30 altındaki 20 yılı geçen ardışık çöküşü seed başarısızlığı sayıyor; seed başına tepe, son nüfus ve çöküş serisini yazdırıyor.
- [ ] Gerçek çok tohumlu soak kullanıcı final testinde çalıştırılacak; eşik gerçek denge üzerinde ölçülmeden Faz B/E kabul edilmez.

## 2026-10-08 · Kullanıcı ekran görüntüsü: 171. yılda uygarlık nüfusu yok

- [x] Görüntüde toplam 170 canlı, 0 yaşayan krallık, 1 yıkılmış krallık ve yalnızca 22 maymun + 148 papağan var. `StatsWindow` sayımları yaşayan birim deposundan yaptığı için bu gerçek uygarlık yok oluşu; toplam nüfusun vahşi türlerden gelmesi sayaç hatası değil.
- [x] Mevcut kaynakta istatistik penceresi toplamın altında ayrıca medeniyet ve yabanıl nüfusu gösteriyor; ekran görüntüsünde bu iki satır yok. Çalışan Unity derlemesi kaynakla aynı sürümde olmayabilir.
- [ ] Görüntü tek başına medeniyetlerin neden öldüğünü göstermez. Tarihçe türün yok oluşunu kaydedebilir, fakat genel ölüm nedeni dökümü ve geçmiş nüfus grafiği bu pencereye bağlı değil. Kurucu-soak ve kullanıcının son oyun testinde nüfus/ölüm eğrisi kontrol edilmeden B/E kabulü kapanmaz.

## 2026-10-08 · Kurucu kabulüne şehir/krallık ve nüfus ayrımı eklendi

- [x] QA listesi 20 UI penceresi sayısını ve Dünya İstatistikleri'nde medeniyet/vahşi yaşam ayrımının kontrolünü içeriyor; görüntüde vahşi nüfus toplamı medeniyetlerin tamamen yok olmasını gizliyordu.
- [x] `founder_soak.ps1`, 6×100 yıllık mevcut kabul kontrolünde tür başına doğum ve son nüfusa ek olarak son yıl medeniyet nüfusu, şehir ve yaşayan krallık bulunmasını da arıyor. Bu screenshot bulgusunu otomatik hata olarak raporlayacak.
- [x] Python checklist üreticisi 247 güç, 8 senaryo ve 20 pencere satırı üretti; PowerShell betik AST sözdizimi kontrolü geçti. `python Tools/compile_check.py` 13/13 assembly ve 0 hata verdi; Unity Test Runner/oyun/soak çalıştırılmadı.
- [ ] Kullanıcı aynı kayıt/seed veya yeni kurucu senaryosunda son test yapacak; ölüm nedeni ve nüfus eğrisiyle kök neden ayrıştırılacak.

## 2026-10-08 · Faz J6 güvenli web paketleme

- [x] WebGL build hedefi `PG_WEB_BUILD_PATH` ile proje içindeki adaya yönlendirilebiliyor. Dağıtım scripti eski çıktıyı build öncesinde silmiyor; staging bundle ve ZIP'i doğrulamadan mevcut site/player klasörlerini değiştirmiyor, başarısız terfide geri alıyor ve eski klasörleri tarihli yedekliyor.
- [x] ZIP üretimi gizli `.htaccess` dosyasını ve web kökü dosya yapısını koruyor; doğrulayıcı güvenli yolları, CRC'yi, tam dosya listesi/içerik eşitliğini, OG görselini ve kritik StreamingAssets'i kontrol ediyor. Yerel `WebSite/tests/` yayımlanan pakete alınmıyor.
- [x] PowerShell AST, Python parse ve 13/13 kaynak assembly derlemesi geçti. Eski ZIP/site/player güncel build öncesi korundu.
- [ ] WebGL build `Logs/release-20261008-20261008-044121-158-WebBuild.log` içinde lisans IPC ve `com.unity.editor.headless` hatasıyla build aşamasına gelemedi. Güncel ZIP, WebGL/hosting doğrulaması, Android imza/build ve cihaz-hosting kabulü bekliyor.

## 2026-10-08 · Faz J8 mod sıralama döngüsü

- [x] Kaynakta mod bağımlılıkları ile `loadAfter` kenarlarını birlikte çözen DFS'in döngüyü loglayıp yine de üyeleri sonuç listesine eklediği doğrulandı.
- [x] Döngü üyeleri ve onlara zorunlu bağımlılığı bulunan modlar artık reddediliyor; bağımsız modlar sıraya alınıp temel oyun içeriği devam ediyor. `ModManifestTests` bu davranış için regresyon içeriyor.
- [x] `python Tools/compile_check.py` üretim ve EditMode kaynaklarını 13/13 assembly, sıfır hata ile derledi.
- [ ] Unity EditMode, WebGL/Android paketli mod örneği, bozuk StreamingAssets ve gerçek mod birlikte çalışabilirliği kullanıcı final testinde; J8 açık.

## 2026-10-08 · Faz J7 herkese açık hesap silme yolu

- [x] `WebSite/account-deletion.html` public sayfası ve footer bağlantısı eklendi; hesap silme kapsamı, yerel kayıtların kalması ve AI sağlayıcısı saklamasının ayrı yönetimi açıklanıyor.
- [x] Web akışı `accountAction=delete` parametresini koruyarak giriş sonrası oyuna yönlendirmiyor, onay düğmesini görünür/fokuslu hale getiriyor. Silme için kullanıcı ayrıca düğmeye basıp onay vermeli.
- [x] Gizlilik taslağı ve Faz J7 kayıtları güncellendi; gerçek Firebase yapılandırması, hosting URL'si, mağaza kaydı ve kullanıcıya ait hukuki/iletişim/saklama alanları açık bırakıldı.
- [ ] Tarayıcı/Firebase hesap silme, gerçek yayın adresi ve Google Play Console bağlantısı son kullanıcı kabulünde doğrulanacak; J7 açık.

## 2026-10-08 · Yol haritası güncel derleme önkontrolü

- [x] Güncel çalışma ağacında `python -B Tools/compile_check.py` üretim kaynaklarının 12 assembly'sini ve PG.Tests.EditMode assembly'sini derledi: 13/13, 0 hata.
- [ ] Bu offline assembly kontrolü Unity Test Runner, oyun içi davranış, uzun soak, gerçek Player performansı veya hosting build'ini doğrulamaz. Kullanıcının son test aşaması ve WebGL lisans engeli bekleniyor; yol haritası fazları kabul edilmiş değil.

## 2026-10-08 · Kaynak ve görsel envanter denetimleri

- [x] `Data/_generator/validate.py`: tüm katalog referansları geçerli, 0 hata. Üretilen `Docs/EkA_Icerik_Katalogu.md` eski sayımlardan kalan 76 içeriği ve eksik tarihçe/senaryo bölümlerini alacak şekilde yenilendi; güncel toplam 1608.
- [x] `Tools/loc_audit.py`: 677 katalog girdisi, TR/EN değerleri, biçim yer tutucuları, 244 literal kaynak anahtarı ve 8 iki dilli senaryo tutarlı. 280 doğrudan eşleşmeyen anahtar dinamik/içerik araması olabileceği için manuel inceleme notu olarak kaldı.
- [x] Runtime sanat denetimi 738/738 bina anahtarını ve 44/44 dünya özelliğini final/atlas sanatıyla çözüyor; 2304 birim pozu ve 61 WAV kaynak/loop/PCM kontrolünden geçti.
- [ ] Bunlar statik kaynak denetimleridir; Unity renderer/audio/UI, mobil/Player veya görsel kullanıcı kabulini kanıtlamaz. J1/J4 ve Faz J bu nedenle açık kalıyor.

## 2026-10-08 · Faz J1 dinamik yerelleştirme anahtarları

- [x] `Tools/loc_audit.py`, doğrudan `Loc.T/F` çağrılarının yanında `Small`/`Heading` gibi arayüz yardımcıları ve anahtar özelliklerinden kurulan dar `ui.*.` ailelerini de tarıyor. Böylece önceki 75 eşleşmeyen adayın 56'sı gerçek dinamik kaynak kullanımıyla eşleşti.
- [x] Güncel denetim: 679 katalog girdisi; 246 literal Loc anahtarı, 434 diğer C# literal anahtarı, 43 JSON içerik referansı, 16 kurulan önek ve 8 iki dilli senaryo tutarlı. 19 anahtar hâlâ kaynak/içerik referansı olmadan raporlanıyor; silinmediler.
- [ ] `python -B Tools/loc_audit.py --show-unreferenced` kaynak denetimi geçti ve 19 aday notu verdi. Unity arayüzü, dar ekran kabulü ve bu 19 anahtarın tutulma/silinme kararı kullanıcı final aşamasında; J1/Faz J açık.

### Faz J1 devamı — kaynakta tüketicisi olmayan anahtarların temizliği

- [x] C# üretim kaynakları, tüm oyun JSON'ları ve WebSite içinde 19 adayın çağrısı bulunmadı. Bunların 15'i eski tarihçe/olay metni, kalan 4'ü eski generic, menü, tanılama veya senaryo metni; tarihçe olayları `chronicle_templates.json` tarafından üretiliyor.
- [x] `Data/_generator/p0_ui.py` içinden kaldırılıp 660 girdilik `Data/ui_strings.json` yeniden üretildi; `Assets/StreamingAssets/Data/ui_strings.json` aynası SHA-256 ile eşitlendi. Karar #193 kaydedildi.
- [ ] Yerelleştirme kaynak denetimi ve içerik doğrulaması kullanıcı Unity/UI kabulinin yerine geçmez; dar ekran testi ve J1/Faz J kapanışı kullanıcı final testinde.

## 2026-10-08 · Kurucu nüfus kabul eşiği

- [x] `CivSoak` ve `Tools/founder_soak.ps1` artık 6×100 yıl sonunda hem insan hem elf türünün ayrı ayrı başlangıç kohortu olan 7 kişiyi korumasını bekliyor; 3 kişiye kadar düşen tür artık başarı sayılmıyor.
- [x] QA beta listesi yeniden üretildi: 247 güç, 8 senaryo, 20 pencere. `founder_soak.ps1` PowerShell AST'si ve checklist üreticisi Python AST'si geçti; `python -B Tools/compile_check.py` üretim + EditMode kaynaklarını 13/13 assembly, 0 hatayla derledi.
- [ ] Unity EditMode, 6 seed × 100 yıl soak ve gerçek nüfus/ölüm nedenleri kullanıcı final aşamasında çalıştırılacak. Bu kapı sonucu olmadan B/C/Faz J kabulü yok.

## 2026-10-08 · Faz A4 kapısına kurucu nüfus soaku eklendi

- [x] `gate_thresholds.json` altı kurucu tohumu ve 100 yıllık koşu eşiğini tanımlıyor; `gate.ps1` çok krallıklı soaktan sonra `founder_soak.ps1` çalıştırıp sonucu başarı koşuluna katıyor.
- [x] Son kapı özetinde çok krallık ve kurucu seed adetleri ayrı raporlanıyor. Unity gate çalıştırılmadı; bu ek son kullanıcı testinde çalıştırılacak ve A4/A açık kalacak.

## 2026-10-08 · Faz A4 kaynak test keşif alt sınırı

- [x] `gate.ps1` tarihsel 204 EditMode + 3 Graphics tabanına ek olarak `Tools/verify_test_inventory.py` ile mevcut C# NUnit test özniteliklerinin alt sınırını iki NUnit XML sonucuyla karşılaştırıyor. Yeni kaynak testleri artık eski sabit eşik nedeniyle sessizce atlanamıyor.
- [x] Kaynakta 364 test özniteliği bulundu: 361 EditMode ve 3 açık Graphics vakası. Eski `Logs/fin3-EditMode.xml` 207 vaka gösterdiğinden yeni kaynak envanterini karşılamıyor; bu tarihsel XML mevcut koşu sonucu sayılmadı.
- [x] Yerel sentetik NUnit XML'inde 361+3 sonuç kabul edildi, 360+3 sonucu reddedildi. Python sözdizimi ve `gate.ps1` PowerShell AST kontrolleri geçti; Unity Test Runner çalıştırılmadı.
- [ ] Gerçek Unity Test Runner XML'leri, betiğin geçiş yolu ve A4 kalite kapısı kullanıcı final test aşamasında doğrulanacak; A4/A açık.

## 2026-10-08 Faz G4 · save ile kalıcı medeniyet/yabanıl nüfus grafiği

- [x] Yüzyıl ve çağ özetleri medeniyet ve yabanıl canlı sayısını ayrı kaydediyor; yıllık grafik serileri seçilebilir ve kaydedilmiş özetlerden yeniden yükleniyor.
- [x] HST1 v4 paralel demografi listesi kullanıyor; v3 ve daha eski özetler bozulmadan okunuyor ve eski yıllar için bilinmeyen ayrım grafikte sıfır olarak uydurulmuyor.
- [x] `WorldSummaryTests` save round-trip, v3 migration, grafik serisi ve eski özette bilinmeyen ayrımın sıfır sayılmamasını kapsayacak şekilde genişletildi. Offline derleme tüm 12 üretim ve EditMode assembly'sinde 0 hata verdi; testler çalıştırılmadı.
- [ ] Unity save/load grafiği, gerçek simülasyon nüfusu ve eski kayıt doğrulaması kullanıcı final testinde; G4 ve Faz G açık.

## 2026-10-08 · Faz B ekonomi kabul denetimi ve nüfus ekranı incelemesi

- [x] `Tools/civ_economy_gate.py` altı `single` soak raporunu 100 yıllık hedeflere göre değerlendiriyor: ortalama kurulan şehir, azami salon seviyesi, insan tepe nüfusu, kıtlık sayısı, yıl sonu eşya üretiminin artması, 30. yıldan sonraki yetişkin işsizlik oranı ve tepe nüfusun %30 altındaki ardışık yılları.
- [x] `gate.ps1` A4 sırasına 6 seed × 100 yıl tek krallık soaku ve yeni doğrulayıcıyı ekliyor. Eşikler `gate_thresholds.json` içinde merkezi olarak tanımlı.
- [x] Sentetik kabul/red araç testleri eklendi: uygun veriyi kabul ediyor; eksik tohum, düşük şehir/salon/insan, yüksek kıtlık/işsizlik, üretimsizlik, yıllık üretim düşüşü, uzun nüfus çöküşü ve en az bir seed'de salon seviyesi 3 koşulunu doğruluyor. Altın birikim oranı, eksik akış ve bakiye uzlaşması için de regresyonlar eklendi. 8/8 test geçti.
- [x] Kullanıcının yıl 171 ekranında dünya nüfusu 170 ve görünen türler 22 maymun + 148 papağan; medeniyet/şehir satırı yok. Bu, görüntüde medeniyet kalmadığını gösteriyor; kayıt/seed veya ölüm dökümü olmadan nedeni belirlenemez. Faz B notuna eklendi.
- [x] Altın akışı vergi/ticaret gelirleri, alım giderleri, krallık/yağma transferleri ve rota ücretleri olarak raporlanıyor; her yıl defter neti toplam şehir altın bakiyesi farkıyla uzlaştırılıyor. 30. yıl sonrası net/bürüt birikim oranı ≤%10 hedefi yapılandırıldı; eşik gerçek çok seed verisiyle kalibre edilmeli.
- [ ] Unity Test Runner ve 6×100 yıllık soaku kullanıcı final aşamasında. Faz B kabulü, özellikle işsizlik/altın eşiklerinin gerçek sonuçları ve oyun içi denge doğrulanana kadar açık.

## 2026-10-08 · Faz B7 veri güdümlü bina bakımı

- [x] 41 bina tanımına `goldUpkeepPerYear` eklendi; generator, JSON schema, content model, katalog ve StreamingAssets kopyası güncellendi. Generator haritası bina kimlikleriyle birebir eşleşmeyi zorunlu tutuyor; içerik doğrulaması 0 hata.
- [x] Tamamlanmış/ayakta şehir binaları yılın 12. ayının başında bakım öder. Tahsilat kasadaki altınla sınırlı; kalan bakım ücreti borç yaratmadan yıllık metrikte `MaintenanceUnpaid` olarak tutuluyor. Bu muhasebe `GoldFlow.Expenses` ve bakiye uzlaşmasına dahil.
- [x] EditMode regresyon kaynakları maliyet tanımları, tamamlanmış bina toplamı, kasadan fazla ödeme yapılmaması ve yıllık tahsilat zamanını kapsıyor. `python -B Tools/compile_check.py`: 13/13 assembly, 0 hata. Python kabul kapısı 10/10 test; veri üreticisi doğrulaması 0 hata ve katalog toplamı 1608.
- [ ] Unity Test Runner ve 6×100 yıl ekonomi soaku kullanıcı final aşamasında; yıllık bakım tutarları/denge oranı gerçek simülasyon sonucu ile kalibre edilmeli. Faz B açık.

## 2026-10-08 · Yıl 171 nüfus ekranı doğrulama düzeltmesi

- [x] İstatistik satırını yeniden okuyunca önceki notta eksik kalan `Krallıklar: 0 yaşıyor, 1 yıkıldı` bilgisi doğrulandı. Bu kayıt hiç medeniyet başlamamış bir dünya değil; en az bir krallık kurulduktan sonra çökmüş.
- [x] Kaynak akışıyla karşılaştırıldı: `GameBootstrap.StartNewWorld` başlangıç nüfusunu `CivStartSpawner` ile yalnız yeni dünya açılırken kuruyor; sürekli otomatik medeniyet yeniden başlatıcısı yok. Bu, çöküşten sonra geri dönüş olmamasını açıklar; krallığın hangi nedenle ve hangi yılda düştüğünü açıklamaz.
- [ ] Ekran görüntüsü ölüm nedeni/tarihçe dökümünü taşımıyor. Aynı save olmadan açlık, savaş, yaşlanma veya üreme katkısını ayırmak ve B/C/E nüfus kabulünü kapatmak mümkün değil; Unity/soak kullanıcı final testine bırakıldı.

## 2026-10-08 · Faz G1 krallık çöküşü nedeni ve yeni görüntü

- [x] Yıl 171 görüntüsü toplam 170 varlığı (148 papağan, 22 maymun) ve 0 yaşayan/1 yıkılmış krallığı gösteriyor. Bu, hayvan nüfusunun medeniyet nüfusu yerine geçtiğini; ancak tek ekranın krallığın hangi olayla yıkıldığını kanıtlamadığını doğruluyor.
- [x] `CityRuined` geçmiş kaydı sahip krallık UID'sini taşıyor. Aynı krallığa ait son üç oyun yılı içindeki şehir yıkımı, fetih veya isyan kaynaklı kayıp `KingdomFell.CauseEventId` ile bağlanıyor; `HistoryChronicle` bu bağlantıyı Türkçe/İngilizce neden satırı olarak gösteriyor. Eşleşme bulunmazsa neden yazılmıyor.
- [x] `HistoryImportanceTests` ve `ChronicleTests` kaynak regresyonları genişletildi; UI ve Unity testleri kullanıcı talimatıyla çalıştırılmadı.
- [x] `python -B Tools/compile_check.py`: 13/13 assembly, 0 hata. `python -B Data/_generator/validate.py`: 0 hata. `python -B Tools/loc_audit.py`: 678 anahtarın TR/EN değeri ve yer tutucuları tutarlı. `git diff --check` temiz; yalnızca mevcut dosya satır sonu uyarıları var.
- [ ] Unity Test Runner, gerçek aynı-kayıt görüntü kontrolü ve 100 yıllık medeniyet/nüfus soaku kullanıcı final aşamasında; G1/Faz G kabul edilmedi.

### Faz G1 devamı — çöküş nedenine tıklama (2026-10-08)

- [x] Tarihçe listesi artık satır indeksini neden bağlantısına iletiyor. Nedeni bulunan `KingdomFell` satırı, eski krallık başkentine gitmek yerine bağlantılı şehir kaybı olayının mevcut harita odağı/şehir penceresi yolunu açıyor; nedeni bulunamazsa eski krallık ayrıntısı davranışı kalıyor.
- [x] `ChronicleTests` neden olayının doğru indeksini bulmayı kapsıyor; `python -B Tools/compile_check.py` üretim + EditMode kaynaklarını 13/13 assembly, 0 hatayla derledi. Unity Test Runner ve görsel oyun kontrolü çalıştırılmadı.
- [ ] Aynı kayıtta gerçek tıklama ve odak kontrolü kullanıcı final testinde; G1/Faz G açık.

## 2026-10-08 · Faz H8 nüfus ekranında medeniyet yok oluşu uyarısı

- [x] Yıl 171 ekranındaki 170 yabanıl canlının yaşayan medeniyeti gizleyebildiği doğrulandı. İstatistikler zaten medeniyet/yabanıl toplamını gösteriyor; yeni uyarı yalnız yaşayan medeniyet nüfusu sıfır, yaşayan krallık sıfır ve en az bir yıkılmış krallık olduğunda çıkıyor.
- [x] Sandbox'ta medeniyet hiç kurulmadıysa ve yaşayan krallık varsa uyarı verilmemesi regresyonla kapsandı. Uyarı metni TR/EN üretildi, StreamingAssets'e kopyalandı; normal/yüksek kontrast tema stilleri eklendi.
- [x] `python -B Tools/compile_check.py`: 13/13 assembly, 0 hata; `Data/_generator/validate.py`: 0 hata; `Tools/loc_audit.py`: 679 TR/EN metin girdisi tutarlı; kaynak ve StreamingAssets `ui_strings.json` SHA-256 eşleşiyor; `git diff --check` temiz.
- [ ] EditMode testinin Unity'de çalıştırılması ve gerçek ekran görüntüsü kullanıcı final testinde; H8/Faz H açık.

### Faz J5 devamı — çöküş uyarısı kabul maddesi (2026-10-08)

- [x] `Tools/generate_qa_checklist.py` ve yeniden üretilen `Docs/Yol_Haritasi/13_QA_Beta_Kontrol_Listesi.md`, nüfus/krallık üçlü koşulunu ve boş sandbox/yaşayan krallıkta yanlış alarm olmadığını açıkça kontrol ediyor.
- [x] QA çıktısı 247 güç, 8 senaryo ve 20 pencere içeriyor; yeni kontrol maddesi eklendi. Kullanıcı final UI testine kadar J5 ve Faz J açık.

## 2026-10-08 · Faz A3 kurucu soaku ölüm nedeni tanısı

- [x] `Tools/founder_soak.ps1` seed başına insan ve elf nüfusunun yıllık minimumunu/yılını ve birikimli en sık üç ölüm nedenini yazdırıyor. Kurucu nüfus tabanını kaybeden koşular, son toplamın yanında zaman ve neden bakımından da incelenebilecek.
- [x] Faz A ve CHANGELOG kayıtları güncellendi. PowerShell AST, offline C# derleme (13 assembly) ve içerik doğrulaması kontrol edildi; Unity Test Runner ve soak kullanıcı final aşamasına bırakıldı.
- [ ] Gerçek çok tohumlu raporlarda ölüm nedeni özetinin okunabilirliği ve kabul ölçütleri kullanıcı final testinde değerlendirilecek; A3/A4/A açık.

## 2026-10-08 · Faz B0 salon yükseltme tıkanma ölçümü

- [x] `SoakMetrics` şehir başına sonraki salon kademesini, maliyetin karşılanıp karşılanmadığını, uygun yer bulunup bulunmadığını ve eksik kaynak miktarlarını rapora ekliyor. Yer araması simülasyon RNG'sini değiştirmeyen yerel deterministik RNG kullanıyor.
- [x] JSON round-trip regresyon kaynakları bu yeni alanları kapsıyor. `python -B Tools/compile_check.py`: 13/13 assembly, 0 hata; `git diff --check`: çıkış kodu 0 (yalnızca depo genelindeki satır sonu uyarıları).
- [ ] Unity Test Runner ve çok tohumlu soak çalıştırılmadı; kullanıcı testleri en sona bıraktı. B0/B açık.

## 2026-10-08 · Faz A3 karşılaştırma çıktısı genişletildi

- [x] `metrics_compare.py`, tohum eşleştirmeli mevcut özet karşılaştırmasına final yıl medeniyet/yabanıl nüfusu, krallık/şehir durumu, medeniyetin olmadığı yıl sayısı ve salon yükseltme engellerini ekliyor. Eski raporlarda bulunmayan salon alanları `n/a` kalıyor.
- [x] `test_metrics_compare.py` kaynak regresyonları eklendi ve A4 gate içine alındı. Python AST, PowerShell AST ve `git diff --check` başarılı.
- [ ] Araç testleri, gerçek before/after raporları ve çok seed soak kullanıcı final testinde çalıştırılacak. A3/A4/A açık.

## 2026-10-08 · Faz E4 yıllık fiyat sapması ölçümü

- [x] Soak örneği pazar başına kaynak fiyatının denge fiyatı 1.0'dan ortalama sapmasını ve örnek sayısını raporluyor. `metrics_compare.py` son yıl değerini tohum bazında karşılaştırıyor; sıfır örneği olan rapor `n/a` gösteriyor.
- [x] JSON alanı için EditMode round-trip, fiyat sayısının karşılaştırıcı çıktısı için Python regresyon kaynağı eklendi; `gate.ps1` bu Python regresyonlarını çalıştıracak şekilde yapılandırıldı.
- [x] Offline C# derleme 13/13 assembly 0 hata; Python AST, PowerShell AST ve ilgili `git diff --check` temiz.
- [ ] Unity testleri, deniz/kara rotalarının gerçek teslimatı ve çok tohumlu fiyat yakınsama soaku kullanıcı finalinde çalıştırılacak. Bu sapma yalnız ölçüm proxy'sidir; E4/Faz E kabul edilmedi.
