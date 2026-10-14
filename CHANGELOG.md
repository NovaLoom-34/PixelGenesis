## [Unreleased] — Yerelleştirme kataloğunu tüketicilerle eşleştir

- `Tools/loc_audit.py`, `Small`/`Heading` gibi UI yardımcılarına ve anahtar özelliklerine kurulan `ui.*.` ailelerini de tanıyor. Kaynakta tüketicisi kalmayan 19 eski/generic çeviri üreteçten kaldırıldı; TR/EN metinleri ve tarihçe şablonları korunuyor. Katalog 660 anahtara indi.

## [Unreleased] — QA listesine medeniyet çöküşü uyarısı

- Üretilen son kullanıcı kontrol listesi, Dünya İstatistikleri çöküş uyarısının tetik koşullarını ve boş sandbox/yaşayan krallık yanlış alarm kontrollerini içeriyor. QA üreteci 247 güç, 8 senaryo ve 20 pencereyi koruyarak yeniden üretildi.

## [Unreleased] — İstatistiklerde medeniyet yok oluşu uyarısı

- Yaşayan medeniyet nüfusu sıfıra inmiş ve en az bir krallık yıkılmışsa Dünya İstatistikleri'nde uyarı gösterilir; hiç medeniyet başlamayan sandbox ve yaşayan krallıklar yanlış alarm üretmez. Yüksek kontrast tema da desteklenir.
- Koşul için EditMode regresyon kaynağı eklendi, metin `Data/_generator/p0_ui.py` üzerinden üretildi ve StreamingAssets kopyası güncellendi. Unity Test Runner çalıştırılmadı.

## [Unreleased] — Krallık çöküşünde şehir kaybı nedeni

- Şehir yıkımı artık sahip krallığın UID'sini tarihçeye taşır; aynı krallığın yakın tarihli şehir kaybı, çöküş olayının nedeni olarak bağlanır ve kronikte gösterilir. Bu satıra tıklamak ilgili şehre odaklanıp şehir penceresini açar; eşleşme yoksa mevcut krallık ayrıntısı korunur.
- Şehir kaybı/krallık nedenselliği, kronik metni ve neden olayının bulunması için EditMode regresyon kaynakları eklendi. Offline derleme 13/13 assembly geçti; Unity Test Runner çalıştırılmadı, kullanıcı final testinde.

## [Unreleased] — Faz B ekonomi sonuçlarını kalite kapısında denetle

- `gate.ps1` altı seed × 100 yıl tek krallık ekonomi soaku çalıştırıp şehir, salon, insan nüfusu, kıtlık, üretim, işsizlik, altın birikim dengesi, bakım tahsilatı ve uzun nüfus çöküşü eşiklerini kontrol ediyor. Bina tanımları yıllık bakım bedelini veriden belirliyor; tam ve ayakta yapıların tahsilatı yıllık gold-flow/bakiye defterinde yer alıyor.
- Python sentetik geçiş/red testleri 10/10 geçti; veri generator/schema doğrulandı; offline derleme 13/13 assembly, sıfır hatayla tamamlandı. Unity Test Runner ve gerçek soaku çalıştırılmadı; kullanıcı final testinde.

## [Unreleased] — Kurucu nüfus düşüşünü kabul kapısında yakala

- 6 seed × 100 yıl insan/elf kurucu soaku artık yalnızca tek hayatta kalanı değil, her türde en az başlangıçtaki 7 kişiyi bekliyor. 3 kişiye düşen bir tür kabulü geçemez.
- Unity soak ve EditMode çalıştırılmadı; kullanıcı final testinde doğrulanacak.

## [Unreleased] — Kurucu nüfus soaku ölüm nedeni tanısı

- `founder_soak.ps1`, her seed için insan ve elf nüfusunun yıllık minimumunu/yılını ve birikimli en sık üç ölüm nedenini raporlar. Başarısız kurucu tohumlarının nedenini incelemek için metrik çıktısı artık doğrudan özetlenir.
- PowerShell AST sözdizimi kontrolü geçti; Unity soak çalıştırılmadı.

## [Unreleased] — Kalite kapısına kurucu nüfus soaku eklendi

- `Tools/gate.ps1` artık çok krallıklı soaktan sonra altı tohumda 100 yıllık insan/elf kurucu deneyi çalıştırıyor ve her türün başlangıç nüfus tabanını korumasını şart koşuyor.
- Kapı ve soaku çalıştırılmadı; kullanıcı final testi için yapılandırıldı.

## [Unreleased] — Kalite kapısında test keşif sayımı

- `Tools/gate.ps1` artık EditMode ve Graphics NUnit sonuçlarını mevcut C# test özniteliklerinden hesaplanan alt sınırla karşılaştırıyor. Böylece yeni testler geldikçe sabit eski 204/3 eşikleri eksik keşfi gizleyemiyor.
- Kaynakta 361 EditMode ve 3 Graphics vakası için alt sınır sayıldı. Sentetik XML geçişi ve eksik vaka reddi doğrulandı; eski 207 vakalık XML yeni envanteri karşılamıyor. Unity Test Runner tekrar çalıştırılmadı.

## [Unreleased] — J7 herkese açık hesap silme yolu

- PixelGenesis ana sayfasından ulaşılabilen `account-deletion.html` hesabın silinme kapsamını anlatıyor ve kullanıcıyı giriş ile ayrı kalıcı silme onayına yönlendiriyor. Yerel oyun kayıtlarının silinmediği açıklanıyor.
- Silme bağlantısından gelen oturum açma, kullanıcıyı oyun sayfasına göndermek yerine hesap silme düğmesine getiriyor. Canlı Firebase/hosting ve mağaza URL doğrulaması bekliyor.

## [Unreleased] — J8 döngülü modları güvenle atla

- Mod sıralama döngüleri artık üyelerini ve onları gerektiren modları yüklemeden atlıyor; bağımsız modlarla temel içerik yüklenebiliyor. EditMode regresyon kaynağı eklendi ve offline derlendi; Unity Test Runner çalıştırılmadı.

## [Unreleased] — J6 güvenli web build ve hosting arşivi

- WebGL player benzersiz staging yoluna yazılabilir; site ve hosting ZIP'i kaynak/player/StreamingAssets/OG/CRC/içerik kontrolünden sonra terfi eder. Önceki player ve hosting klasörleri tarihli yedeklenir; hata halinde geri alınır. 8 Ekim batch build'i Unity headless lisans hatasında durduğu için yeni yayın paketi üretilemedi.

## [Unreleased] — Kurucu nüfus çöküşü kabul koşulları

- QA listesi insan/elf kurucu testinde 100 yıl sonunda yaşayan türlerin yanında şehir ve krallık varlığını da ister; istatistik penceresinde medeniyet/vahşi nüfus ayrımı ayrıca kontrol edilir. `founder_soak.ps1` bu değerleri metrikten reddeder. Unity soak/test çalıştırılmadı.

## [Unreleased] — Faz J4 ses tetik ve sanat kapsam denetimi

- Ses denetimi runtime tetiklerini, ambiyansı, çağ müziğini ve güç fallback yollarını kataloğa bağlıyor. Güncel kaynak kapsamı bina/özelliklerde 738/738 ve 44/44; 2.304 karakter pozu ve 61 WAV statik doğrulamadan geçti. Unity ve mobil ses kabulü bekliyor.

## [Unreleased] — Faz J3 tanı paketi anahtar maskeleme

- Yerel hata raporu API key, client secret ve token alanlarının boşluklu/JSON yazımlarını da maskeliyor; regresyon kaynağı 13 assembly'lik offline derlemeden geçti.

## [Unreleased] — Faz J2 kayıt yedeği hata güvenliği

- Ana kayıt değiştirme başarısız olursa mevcut `.bak` korunuyor; yedek yalnızca yeni ana save güvenli biçimde yazıldıktan sonra döndürülüyor. Regresyon kaynağı 13 assembly'lik offline derlemeden geçti; Unity testi çalıştırılmadı.

## [Unreleased] — Faz H4 undo maliyet ölçüm kaynağı

- Undo kayıtları mevcut save telemetrisinde snapshot, sıkıştırma+disk yazma ve toplam süreyi; dosya boyutu, harita ve nüfusu `undo_` dosya adıyla raporluyor. Ek ölçüm kodu gerekmediği belgelendi; gerçek platform örnekleri kullanıcı final aşamasında.

## [Unreleased] — Faz H1 senaryo terminal kapsamı

- Sekiz katalog senaryosunun hedef ölçütleri sağlandığında kazanma, her ayrı kayıp koşulu sağlandığında kaybetme sonucunu değerlendiren EditMode regresyon kaynakları eklendi. Test assembly offline derlendi; Unity testleri ve doğal oynanış/denge soaku çalıştırılmadı.

## [Unreleased] — Medeniyet çöküşünü soak kapısında yakalama

- Çok krallıklı soak betiği, yıllık medeniyet nüfusunda 20 yılı aşan derin çöküşü ve son yılda sıfır uygarlık kalmasını raporlayıp reddeder. Gerçek soak çalıştırılmadı.

## [Unreleased] — Oyun içi performans tanısı

- F3 tanı kaplaması son 120 karenin ortalama süresini/FPS değerini ve en yavaş kareyi, ayrıca Player/Editor, platform, cihaz, grafik API'si, çözünürlük ve hedef FPS bilgisini gösterir. Sabit boyutlu pencere her karede heap ayırmaz.
- Kayan pencere için EditMode regresyon kaynağı eklendi; Unity testi ve gerçek Player/WebGL/Android ölçümü çalıştırılmadı.

## [Unreleased] — Küçük insan/elf kurucu grubu soaku

- `CivSoak -Scenario founders` yedi insan ve yedi elfi dengeli cinsiyetlerle ayrı kümelerde başlatıp yıllık iki tür nüfusunu raporlar. `Tools/founder_soak.ps1` altı tohumda her iki tür için başlangıç sayısı, doğum ve son nüfus ölçütlerini kontrol eder. Unity soaku çalıştırılmadı.

## [Unreleased] — Medeniyet kur ve doğum akışı düzeltmesi

- Medeniyetlerde eşlerden birinin üreme isteği kur yapmayı başlatabilir; kur sırasında karşılıklı eş hedefi alan partnerin de ayrıca aynı anda istek üretmesi gerekmiyor. Bu, küçük başlangıç topluluklarında doğumların gereksiz iptal olmasını önler. EditMode regresyon kaynağı güncellendi; Unity Test Runner çalıştırılmadı.

## [Unreleased] — Faz E4 ticaret ve kıtlık A/B aracı

- CivSoak için kayıt dışı, varsayılan açık ticaret kontrolü ve aynı seedlerde ticaret kapalı/açık raporları üreten `Tools/trade_famine_ab.ps1` eklendi. Betik kıtlık olaylarını uygarlık nüfus-yılına normalize eder ve açık ticaret koşulunda gerçek teslimat ister. Unity karşılaştırması çalıştırılmadı.

## [Unreleased] — Vahşi ekosistem tanı çalıştırıcısı

- `run_unity.ps1 -Mode WildEcosystemSoak` ve `Tools/wild_soak.ps1` altı tohumu/200 yılı çalıştırıp yıllık tür, nüfus, doğum ve ölüm JSON raporlarının güncel olduğunu doğrulayacak şekilde eklendi. Rapor ilk tick öncesi tür sayımını ve başlangıç türlerinin son yıl hayatta kalma oranını da içerir. Tanı yalnız koyun-kurt senaryosunu kapsar; tam C fazı kabulü değildir. Unity çalıştırılmadı.

## [Unreleased] — Faz A5 tam durum karması

- Determinism ve save-load-devam kontrolleri artık tüm kayıtlanabilir simülasyon bölümlerini ve RNG/saat durumunu karşılaştırıyor; dünya hash'i bütün iklim ve ekoloji zone alanlarını kapsıyor. Test kaynakları derlendi, Unity testleri çalıştırılmadı.

## [Unreleased] — Faz C0 doğal topluluk soak hazırlığı

- Vahşi ekosistem soak menüsü artık biyomların doğal fauna topluluğunu üretir; ikili koyun-kurt tanı senaryosu diğer tanı menülerinde korunur. Başlangıç ve yıllık bitki örtüsü kapsamı JSON'a eklenir.
- Altı tohumluk çalıştırıcı tür devamı, uzun süreli tür baskınlığı ve bitki örtüsü değişimi eşiklerini raporlar. Offline üretim ve EditMode assembly derlemesi 0 hatayla geçti; Unity Test Runner ve soak kullanıcı final testine bırakıldı.
- Yıllık JSON'a çözümlenmiş besin ağından avcı/av toplamları eklendi; soak betiği ilk ve son 50 yılın normalize nüfus aralıklarını sönümlenme tanısı olarak karşılaştırıyor.
- 5.000 birimde birim sistemlerinin 0,5 ms/tick hedefini raporlayan JSON performans koşusu eklendi; altı tohumlu vahşi ekosistem betiği bu koşuyu tamamlayıp sonucu da doğrular. Unity ölçümü çalıştırılmadı.
- Soak raporu, vahşi nüfusun önceki 20 yıl tepesinin %25'ine inmesini ve sonraki 20 yılda %75'e toparlanmasını sayar; 50 ardışık düşük yıl başarısızlık verir. Bu tanı gerçek tohum sonuçlarıyla henüz çalıştırılmadı.

## [Unreleased] — Faz D iklim A/B ölçümü

- `CivSoak` eşleşmiş seed'lerle sabit iklim kontrolü ve değişken iklim koşusu alabiliyor; özet ölçütlerine iklim fazı ms/tick ve ms/ay eklendi.
- `Tools/climate_ab.ps1` altı seed/100 yıllık koşuların iklime bağlı kıtlık oranı, seedler arası kıtlık değişkenliği, nüfus ve aylık maliyetini karşılaştırır. Çalıştırma/testler kullanıcı final aşamasında.

## [Unreleased] — Faz D sel ve tarımsal kıtlık bağı

- Şehir tarlasını veya mahsul feature'ını gerçekten su altında bırakan sel, şehir için 24 ay hatırlanan ürün kaybı izi oluşturur; bu aralıktaki kıtlık `Flood` nedenini taşır. Ekili olmayan yerdeki sel bu nedeni üretmez.
- Sel izi şehir kayıt bölümüne isteğe bağlı `FLD1` ekiyle yazılır; eski kayıtlar varsayılan boş iz ile yüklenmeye devam eder. İlişkilendirme, süre ve kayıt uyumu için EditMode regresyon kaynakları eklendi; Unity testleri ve iklim soaku çalıştırılmadı.

## [Unreleased] — Faz A test derleme önkontrolü

- Offline derleme aracı artık Unity testlerini çalıştırmadan güncel EditMode test assembly'sini de derliyor. Üç test kaynağı API/ad alanı uyumsuzluğu giderildi; üretim ve test assembly derlemesi 0 hatayla geçti.

## [Unreleased] — Faz A4 kalite kapısı düzeltmesi

- Çok tohumlu medeniyet soak çağrısında PowerShell dizi parametresi artık metin olarak değil `int[]` olarak aktarılıyor. Gate ve Unity soak doğrulaması kullanıcı final aşamasında.

## [Unreleased] — Faz J4 tam bina atlas kapsamı

- Altı kültürün sur/kapıları için iki sütunlu; yuva, kovan ve harabeleri için üç sütunlu atlaslar eklendi. Yükleyici, atlas metadata'sında aileye göre sütun sayısını kullanıyor; eski üç sütunlu düzen korunuyor.
- Statik tarama 738/738 bina anahtarını çözüyor (9 tekil renkli + 729 atlas), placeholder/fallback yok; 44/44 dünya özelliği çözülüyor. Unity ve oyun içi kabul açık.

## [Unreleased] — Faz J4 anıt sanatı

- Altı kültüre tanrı heykeli, anıt ve cenaze ateşinin inşaat/tamamlanmış/harabe atlasları eklendi. Önizleme 216 yapı türünü ve 648 durumu gösteriyor.
- Statik kapsam 738 anahtarda 648 çözülmüş (9 tekil renkli + 639 atlas), 72 placeholder ve 18 fallback; 44 dünya özelliği çözülüyor. Unity ve oyun içi kabul açık.

## [Unreleased] — Faz J4 dini yapılar sanatı

- Altı kültüre büyük tapınak, şifacı evi ve mezarlık için inşaat/tamamlanmış/harabe atlası eklendi. Önizleme 198 yapı türünü ve 594 durumu gösteriyor.
- Statik kapsam 738 anahtarda 594 çözülmüş (9 tekil renkli + 585 atlas), 126 placeholder ve 18 fallback; dünya özellikleri 44/44 çözülüyor. Unity ve oyun içi kabul açık.

## [Unreleased] — Faz J4 kıyı yapıları sanatı

- Altı kültüre balıkçı kulübesi, iskele ve tersane görselleri inşaat/tamamlanmış/harabe durumlarıyla eklendi. Önizleme 27 bina türü ve 486 durumu gösteriyor.
- Statik runtime kapsamı 738 anahtarda 486 çözülmüş (9 tekil renkli + 477 atlas), 234 placeholder ve 18 fallback; 44 dünya özelliğinin tamamı çözülüyor. Unity ve oyun içi kabul son aşamada.

## [Unreleased] — Faz J4 ileri şehir yapıları sanatı

- Altı kültür için malikâne, ahır ve kışla inşaat/tamamlanmış/harabe görselleri eklendi. Önizleme 24 bina türü ve 432 görsel durumu gösteriyor.
- Statik runtime kapsamı 738 anahtarda 432 çözülmüş (9 tekil renkli + 423 atlas), 288 placeholder ve 18 fallback; 44 dünya özelliğinin tamamı çözülüyor. Unity ve oyun içi görsel kabul kullanıcı final aşamasında.

## [Unreleased] — Faz J4 kamusal yapı sanatı

- Altı kültürün kuyu, yel değirmeni ve han görselleri üç durumlu atlasla eklendi. Runtime seçici, önizleme ve test kaynağı 21 yapı türünü kapsıyor.
- Statik bina kapsamı 738 anahtarın 378 final/atlas, 342 placeholder ve 18 fallback olduğunu gösteriyor; 44 dünya özelliği çözülüyor. Unity ve oyun içi görsel kabul kullanıcı final aşamasında.

## [Unreleased] — Faz J4 kuruluş yapıları sanatı

- Altı kültürün kamp ateşi, çadır ve kulübe görselleri üç durumlu altıncı bina atlasına eklendi. İnsan kulübesi için mevcut tekil çizimlerin yükleme önceliği korundu. Yükleyici, önizleme, kaynak atlas denetimi ve test kaynağı 18 yapı türüne genişledi.
- Statik bina kapsamı 738 anahtarın 326 final/atlas, 394 placeholder ve 18 fallback olduğunu gösteriyor; 44 dünya özelliği çözümleniyor. Unity ve oyun içi görsel kabul kullanıcı final aşamasında.

## [Unreleased] — Faz J4 gıda ve tarım bina sanatı

- Altı kültür için fırın, ağıl ve arı kovanı atlasları eklendi; her biri inşaat, tamamlanmış ve harabe karelerine sahip. Yükleyici, metadata üreticisi, tarayıcı önizlemesi ve EditMode kaynak kapsamı 15 bina türüne genişledi.
- Statik bina kapsamı 738 anahtarın 275 final/atlas, 445 placeholder, 18 fallback olduğunu gösteriyor; 44 dünya özelliği çözümleniyor. Unity doğrulaması ve oyun içi görsel kabul kullanıcı final aşamasında.

# Changelog

## [Unreleased] — Faz E0 diplomasi soaku telemetrisi

- Çok krallıklı soak raporu artık gerçek kervan/gemi teslimlerini, kabul edilen antlaşmaları, her krallığın savaşa katılımını, vasallık/isyanı, krallık nüfus paylarını/çöküşlerini, aktif savaş sürelerini ve Meta fazı ms/tick değerini içeriyor.
- `Tools/kingdom_soak.ps1` altı tohum × 300 yıl kabul kapısını bu metriklerle doğrular. Unity soaku ve EditMode testleri kullanıcı final test aşamasına bırakıldı; Faz E kabul edilmedi.

## [Unreleased] — Faz J4 kültür yapıları sanatı

- Altı kültür için kütüphane, okul ve tapınak atlasları inşaat/tamamlanmış/harabe durumlarıyla runtime'a eklendi. Önizleme 180 yapı türü ve 540 durumu kapsıyor.
- Statik runtime kapsamı 738 anahtarda 540 çözülmüş (9 tekil renkli + 531 atlas), 180 placeholder ve 18 fallback gösteriyor; 44 dünya özelliğinin tümü çözülüyor. Unity ve oyun içi kabul açık.

## [Unreleased] — Faz J4 detaylı canlı atlası geri dönüşü

- Kaynak poz maskeleri ile Unity atlas geri dönüşünün ölçek yuvarlaması hizalandı; `water_elemental` ve `crystal_beetle` gibi yarım değerli boyutlarda maske genişliği artık üretimle aynı hesaplanıyor.
- İki yarım-piksel örnekleme durumu için EditMode regresyon kaynağı eklendi. Salt okunur canlı sanatı denetimi 2.304 pozu geçti; 192 runtime atlas/düzen dosyası ve iki kaynak öğe atlas dosyası StreamingAssets ile eşleşti. Unity doğrulaması kullanıcı final aşamasında.

## [Unreleased] — Faz J4 bina sanatı ve mimari kapsamı

- `Tools/audit_runtime_art_coverage.py`, 738 stil/bina/durum anahtarını ve 44 dünya özelliğini runtime yükleme sırasına göre sayar. Altı halkın on iki bina türü için 221 bina anahtarı final/atlas sanatı, 499 placeholder, 18 fallback kullanıyor; 44 özelliğin tamamı çözülüyor.
- Altı halkın 216 durum karesini içeren önizlemesi, manifesti, StreamingAssets eşlemesi ve EditMode test kapsamı güncellendi. Kalan 499 placeholder anahtar için sanat üretimi ve Unity oyun içi görsel kabulü açık.


## [Unreleased] — Dünya nüfusu ayrımı

- Dünya istatistikleri artık toplam nüfusun altında medeniyet nüfusunu ve yaban/diğer canlıları ayrı gösteriyor. Tür listesi ve toplam değer aynı kaldı; ayrım, hayvan yoğunluğunun medeniyet nüfusu sanılmasını önlüyor.
- Türkçe/İngilizce etiketler UI kaynağından üretildi. Unity arayüz testi kullanıcı final aşamasında.

## [Unreleased] — Eski kayıt uyumluluğu fikstürü

- Depodaki oyun 1.0 kayıt fikstürünü güncel yükleme yolu ve eksik opsiyonel senaryo bölümüyle açan `LegacySaveFixtureTests` kaynağı eklendi. `PolicySaveMigrationTests`, eski politika bölümü 1–8 alan düzenlerini ve vergi varsayılanını kapsıyor; `HistoryMetadataMigrationTests` tarihçe yan bloğunun v1 eski düzenini, `DeterminismTests` da yarım `.tmp` kalıntısının son kayıt yüklemesine etkisiz kalmasını sınayacak kaynak ekliyor. Unity derleme/testi kullanıcı final aşamasında.

## [Unreleased] — Tanı paketi gizlilik kapsamı

- Yerel tanı log rollover işlemi artık UTF-8 bayt bütçesini koruyor; çok baytlı hata metinleri 512 KiB sınırını aşmıyor.

- Günlük maskelemesi artık JSON biçimli API anahtarı ve parola değerlerini de gizliyor; mevcut DiagnosticsBundleTests için regresyon girdileri eklendi.

- Tanı paketi profilinden yalnızca allowlist ayarlarını dışa aktardığını ve keşifler, başarımlar, güç listeleri, krallık izlemeleri ile özel sayaçları dışarıda tuttuğunu denetleyen `DiagnosticsBundleTests` kaynağı eklendi. Unity doğrulaması kullanıcı final aşamasında.

## [Unreleased] — Faz F2 geniş nehir ve köprü

- Yüksek akışlı nehirler geçilmez su kanalına genişliyor; yol fırçasıyla nehir üstüne köprü kurup kaldırmak mümkün. Köprü hareket region'larını güncelliyor ve mevcut harita bayraklarıyla kaydoluyor.
- Save/load sırasında türetilmiş hareket bayrağı yeniden kurulup köprü geçişi korunuyor. Köprü/region ve save/load regresyon kaynakları eklendi. Unity testi, görsel kabul, çok tohum ve performans kontrolü kullanıcı final test aşamasına bırakıldı.

## [Unreleased] — Faz F2 otomatik taşkın yerleşimi

- Otomatik sel afeti artık nehir yakınındaki alçak kıyı ovasını hedefliyor; nehir olmayan haritalarda mevcut rastgele kara seçimine geri dönüyor. Deterministik yer seçimi regresyon kaynağı eklendi; Unity doğrulaması kullanıcı final aşamasında.

## [Unreleased] — Medeniyet nüfusu ve yerel üreme sınırı

- Yerel 12 bireylik taşıma kapasitesi artık yalnız yabani türlerin doğumunu durduruyor. İnsan, elf, cüce ve ork nüfusu şehir konutu/yiyecek katsayıları ve genel nüfus tavanıyla dengeleniyor; küçük yerleşimlerin kalabalık sayılıp doğumu tamamen kesmesi önlendi.
- İnsan nüfusunun yerel sınırdan etkilenmemesi ve yabani hayvan sınırının korunması için EditMode regresyon kaynakları eklendi. Unity test/derlemesi kullanıcı final aşamasında çalıştırılacak.

## [Unreleased] — Faz J8 mod manifesti şema doğrulaması

- `Data/_schemas/mod_manifest.schema.json`, masaüstü/Android/WebGL içerik yoluna kopyalanıp mod yükleme sırasında uygulanıyor. Hatalı manifest yalnızca ilgili modu uyarıyla atlıyor; temel oyun yüklemesi sürüyor. Şemanın desteklenmeyen bir doğrulama anahtarı eklemesi kapalı biçimde mod manifestlerini reddediyor.
- `Examples/Mods/example.world_notes` menü etiketini değiştirerek yüklemeyi görünür kılan localization-only örnek sunuyor. Manifest biçimi, zorunlu alanlar, türler, desen, sabit şema sürümü, bilinmeyen alanlar ve yinelenen liste girdileri için regresyon test kaynakları eklendi. Unity ve paketlenmiş platform kontrolleri kullanıcı final aşamasında.

## [Unreleased] — Sandbox uygarlık başlangıcı

- Yeni dünya ekranında “Uygarlıklarla başla” seçeneği var ve varsayılan açık. Sandbox dünyaları uygun kara kümelerine insan, elf, cüce ve ork türlerinden sekizer yetişkinle başlayabilir; senaryolar kendi belirlenmiş başlangıç topluluklarını kullanır. Seçenek dünya üretim tarifi ve kaydıyla taşınır.
- Başlangıç grubu ve dişi/erkek eşitliği için regresyon kaynağı eklendi; Unity doğrulaması kullanıcı final test aşamasında.

## [Unreleased] — Eyleme bağlı öğretici ilerlemesi

- Öğretici, dünyada önceden bulunan hayvan veya uygarlıkları oyuncunun yaptığı eylem saymıyor; ilgili sekmeden yeni birim eklenince ilerliyor. Şehir/kanun adımları oturum başındaki sayaçları, doğa/yıkım adımı da doğru sekmede gerçekten uygulanmış güç eylemini bekliyor. Dünya değiştirme/yeniden yükleme ve ayar sıfırlama akışları rehberi doğru başlangıca alıyor.
- Sekme, nüfus artışı, sayaç sınırları ve güç eylemi koşulları için `TutorialProgressTests` eklendi; Unity testi kullanıcı final aşamasında.

## [Unreleased] — Faz G7 kayıtlı dünya tarifi

- Yeni kayıtlar taşınabilir dünya üretim ayarlarını opsiyonel bölüm 19'da saklıyor; tarif yükleme ve undo geri yüklemesinde geri geliyor. Eski kayıtlar değişmeden açılıyor ve üretim tarifi içermediği için paylaşım düğmesi kapalı kalıyor. Özel yerel görsel şablonları paylaşılabilir tarif olarak saklanmıyor.
- Round-trip, eski kayıt ve bozuk tarif regresyon kaynakları eklendi. Unity derleme/testi kullanıcı final test aşamasında.

## [Unreleased] — Faz E1e saldırmazlık ihlali

- Oyuncu geçerli bir savaş emriyle saldırmazlık antlaşmasını bozabilir; savunucuyla ilişkisi 30 düşer ve savaş nedeni tarihçede “Antlaşma ihlali” olarak görünür. Geçersiz savaş türü antlaşmayı/ilişkiyi değiştirmez; AI otomatik savaş seçimi antlaşmaya uymaya devam eder.
- Unity derleme/testi kullanıcı final aşamasında; E1/Faz E açık.

## [Unreleased] — Faz E6 vali diplomasi kapsamı

- Vali komut talimatı, aktif saldırmazlığı bozarak savaş ilan etmenin iptal ve −30 görüş bedelini açıklar. Vali belgesine ticaret ambargosu teklifi ve vasal savaş sınırı eklendi; dört antlaşma teklifini JSON'dan komut kuyruğuna eşleyen regresyon kaynağı eklendi.
- Unity derleme/EditMode ve gerçek sağlayıcı cevabı kullanıcı final aşamasında; E6/Faz E açık.

## [Unreleased] — Faz E1d oyuncu barış şartları

- Savaş içindeki krallık barış teklifinde ödenecek altını, devredilecek şehri, teklif verenin vasallığını ve barış sonrası saldırmazlık süresini seçebilir. Komut kuyruğu şartları bekleyen teklife taşır; alıcı kabul etmeden uygulanmaz.
- AI uygulanabilir tavizleri erken barış değerlendirmesine katar; önceki savaş süresi/kayıp/görüş kabul eşikleri korunur. UI metinleri `p0_ui.py` ile üretildi; `AllianceTests` kaynakları komut kuyruğu ve erken kabul akışlarını kapsıyor. Unity derleme/testi kullanıcı final aşamasında; E1/Faz E açık.

## [Unreleased] — Faz E1c oyuncu elçi teklifleri

- Kontrolü oyuncuda olan krallıklar ilişkiler sekmesinden savaşta barış; barışta ittifak, ticaret, saldırmazlık, ambargo ve vasallık teklifi gönderebilir. Gelen teklifler vali etkin olmasa bile aynı sekmeden kabul veya reddedilebilir. Teklifler simülasyon komut kuyruğuna girer.
- Yinelenen bekleyen teklifler ve zaten yürürlükte olan antlaşmalar için yeni teklif düğmesi gösterilmez. TR/EN metinleri UI üreticisinden oluşturuldu.
- Unity derleme/testi ve oyun içi arayüz kabulü kullanıcı final test aşamasında; E1/Faz E açık.

## [Unreleased] — Faz D kıtlık ve iklim ölçümü

- `CivSoak` yıllık/özet metriğine `ClimateLinkedFamineEvents` ekledi. Kuraklık veya soğuk işaretli `FamineEvent`'ler toplam kıtlıklardan ayrı sayılır; ilişki ölçülür, tek başına neden kanıtı sayılmaz. Çok tohumlu soak ve hedef oran kullanıcı test aşamasında.

## [Unreleased] — Faz C yıllık tür demografisi

- `SoakMetrics` yaşayan tüm türleri, medeniyet/vahşi toplamlarını, tür başına dişi/erkek ve yetişkin/yavru dağılımını, ayrıca yıllık doğumları ve ölüm nedenlerini yazar. Mevcut `Population` alanı medeniyet toplamı olarak korunur; kıyaslama ve gate tüketicileri geriye dönük uyumlu kalır.
- Çok krallıklı gate, ilk yıl tür sayısını artık `IsCiv` ile süzer; yeni vahşi tür kayıtları medeni tür kabul eşiğini yapay biçimde geçiremez.
- `PixelGenesis/Wild Ecosystem Soak` ayrı menü komutu, `PG_WILD_YEARS`/`PG_WILD_SEED` seçenekleriyle yıllık vahşi tür metrik raporu üretir; varsayılan süre 40 yıldır. 200 yıllık kabul ve çok tohumlu çalışma henüz yapılmadı.
- JSON regresyon kaynağı güncellendi. Unity testi ve çok tohumlu ekosistem soaku kullanıcı final doğrulamasına bırakıldı.

## [Unreleased] — Faz C yumurtlayan türlerde kuluçka aralığı

- Yumurta bırakan dişi, yeni kuluçkadan sonra yumurtanın 10 aylık gelişme süresiyle eşleşen üreme arasına giriyor; aralık mevcut statü/save mekanizmasıyla saklanıyor. Ekosistem regresyon kaynağı eklendi, Unity testi ve nüfus soaku çalıştırılmadı.

## [Unreleased] — Faz F2c taşkın ovası biyokütlesi

- Nehir karolarında doğal bitki büyümesi durur; kıyıdaki sekiz komşu karoda yiyecek kaynağı yenilenmesi %35 artar. Kayıt şeması değişmez; Unity doğrulaması kullanıcı final test aşamasında.

## [Unreleased] — H2 duruma bağlı oyuncu önerileri

- HUD'daki “Ne yapabilirim?” penceresi şehir erzağı/konut baskısını, devam eden savaşı ve medeniyet yokluğunu okuyarak ilgili adımları önerir. Şehir veya savaş ayrıntısına götürür; senaryo kısıtını denetleyerek insan bırakma aracını seçebilir. Öneriler simülasyonu kendiliğinden değiştirmez.
- `StateSuggestionsTests` kaynakları eklendi ancak Unity testleri, mobil/masaüstü UI ve ilk 10 dakika kullanılabilirlik kabulü kullanıcı test aşamasına bırakıldı.

## [Unreleased] — H3 bağlama göre güçler ve keşif

- “Ne yapabilirim?” önerileri ilgili güç/araç seçimine geçiyor. Başarımla açılan kilitli güçler `Profile.Discovered` listesine ekleniyor; bildirime tıklamak gücü seçiyor ve eski profiller kazanılmış başarımlarla uzlaştırılıyor.
- Keşfedilmemiş kilitli güç tooltip'i açılma başarımını ve koşulunu gösteriyor. Eşleşmeyen arama boş durum ve temizleme düğmesi sunuyor. Regresyon test kaynakları eklendi; Unity ve dokunmatik/oyun içi kabul kullanıcı test aşamasında.

## [Unreleased] — Faz J6 Android yayın derlemesi hazırlığı

- `AndroidReleaseBuild` imzalı Release AAB derleme yolunu ekler; final paket kimliği ve keystore bilgilerini ortam değişkenlerinden ister. Keystore parolaları işlem sonunda temizlenir ve repoya yazılmaz.
- Android dağıtım yönergesi `minSdk 25` / `targetSdk Auto` kaynak ayarlarıyla eşleştirildi; mağaza sayfası metin ve gerçek ekran görüntüsü planı eklendi. Oyun sayfasına paylaşım meta verisi, web host kurallarına Brotli varyantı ve yeniden doğrulama başlıkları eklendi. Unity build, imza, cihaz kabulü ve mağaza gereklilikleri çalıştırılmadı; J6 açık.

## [Unreleased] — Faz J7 veri akışı ve lisans taslağı

- `CREDITS.md` mevcut Json.NET, Firebase JavaScript SDK ve Google Fonts/CDN yazıtiplerini lisanslarıyla listeliyor. KVKK ve Google Play resmi kaynaklarına bağlı, kaynak akışlarını açıklayan gizlilik/kullanım ve yaş derecelendirme taslağı eklendi.
- Metin yayınlanmaya hazır hukuki belge değildir: yayıncı kimliği, aktarım/saklama ayrıntıları, SDK taraması ve hukuk incelemesi gerekir; Firebase hesap silme akışı eksik.

## [Unreleased] — Faz J7 web hesap silme akışı

- Oturum açmış kullanıcı, onay diyaloğundan Firebase Auth hesabını kalıcı olarak silebilir. Son giriş tazeliği isteyen sağlayıcı hataları tekrar giriş yönergesi verir.
- Silme ekranı ve tamamlanma mesajı, cihazdaki yerel oyun kayıtlarının bu işlemle silinmediğini açıkça belirtir; web oyununda Auth dışında Firebase veri deposu entegrasyonu bulunmuyor.
- `auth-actions.test.cjs` silme sözleşmesi için eklendi; kullanıcı talimatıyla test çalıştırılmadı. Gerçek Firebase proje akışı ve hukuk/mağaza metinleri açık.

## [Unreleased] — Faz J8 içerik modu şema sürümü

- Veri modlarına `contentSchemaVersion` v1 eklendi. Eski manifestler geriye uyumlu olarak v1 kabul edilir; tanınmayan içerik şemasına sahip mod uyarıyla atlanır.
- Modlama belgeleri gerçek kapsamı yansıtıyor: Data/Localization yamaları var, DLL/kod modları yok. `ModManifestTests` kaynağı eklendi ancak kullanıcı test aşaması için çalıştırılmadı.

## [Unreleased] — Faz J4 özellik sanatı atlası

- Placeholder'a düşen 16 bitki, ekin ve kaynak öğesi için özgün saydam 4×4 renk atlası eklendi. `SpriteLibrary` hücreleri runtime sprite'larına böler; var olan Final renkli özellik görselleri önceliğini korur.
- Atlas eşlemesi StreamingAssets'e senkronize edilir. `FeatureArtAtlasTests` regresyon kaynağı eklendi, fakat kullanıcı talimatıyla Unity derleme/testi ve oyun içi görsel kabulü sona bırakıldı; J4 kabul edilmedi.

## [Unreleased] — Faz J4 savaş vuruşu ses bağlantıları

- Yakın dövüş vuruşu artık yalnızca hedefi öldürdüğünde değil, hasar verdiğinde ses üretir. Menzilli saldırılar ve büyü darbeleri ayrı `hit.ranged` / `hit.magic` tetiklerini hedef konumunda çalar; öldürme ve ölüm sesleri ayrı kalır.
- Darbe türü/konum olay sözleşmesi için `CombatImpactAudioTests` kaynağı eklendi. Unity derleme/testi ve gerçek dinleme kullanıcı final aşamasına bırakıldı.

## [Unreleased] — Faz J4 görsel karşılaştırma aracı

- `Tools/visual_compare.py` aynı çözünürlükteki PNG ekran görüntülerinin değişen piksel yüzdesi, ortalama ve azami kanal farkını raporlar; klasörleri eşleşen göreli adlarla karşılaştırabilir ve isteğe bağlı fark eşiği uygular.
- Onaylı görsel baseline henüz seçilmedi. Araç, Unity testleri ve oyun içi ekran görüntüsü kabulü final kullanıcı aşamasında.

## [Unreleased] — Faz J3 yerel tanılama

- Unity hata/istisnaları sınırlı yerel `diagnostics.log` dosyasına ve son 80 girdilik tampon belleğe kaydediliyor. Ayarlar penceresinden sürüm, seed/yıl, nüfus, platform ve izin verilen profil tercihlerini içeren tanı metni panoya alınabiliyor.
- Tanı metni yaygın API/Bearer anahtarı, e-posta ve kullanıcı klasörü biçimlerini maskeliyor; AI bağlantı ayarları dışarıda ve telemetri kapalı. Regresyon test kaynağı eklendi ancak Unity doğrulaması kullanıcı testlerinin son aşamasına bırakıldı; J3 kabul edilmedi.

## [Unreleased] — Faz I5 web çözünürlük ölçeği

- WebGL oyun canvas'ında cihaz piksel oranı sınırlandı: mobilde 0,75–1,0, masaüstünde en fazla 2,0. Mobil kaynaklar sınırlıysa başlangıçta daha düşük çizim çözünürlüğü seçilir; `?renderScale=` ile sınırlar dahilinde değiştirilebilir.
- Gerçek cihaz görüntü, pil/ısı ve performans doğrulaması kullanıcı test aşamasında; I5 kabul edilmedi.

## [Unreleased] — Faz I6 kalite kapısı ölçümleri

- Kalite kapısı PlaySmoke sonrasında CrowdProbe çalıştırıp beş sabit yıl raporunun hepsini arıyor. PerfBench, her simülasyon tick'indeki tahsisi ölçüp en kötü tick sonucunu yazıyor; editor-batch kapısı 0 B/tick eşiği kullanıyor.
- Eşik ayrıştırma testi kaynakta eklendi. Gate ve Unity doğrulaması çalıştırılmadı; I6 kabul edilmedi.

## [Unreleased] — Faz I4 birim görseli ve yükleme maliyeti

- Karakter sanatının yüksek çözünürlüklü kaynakları korundu; 96 birim atlası önceden 160×240 piksele küçültülerek üretiliyor. Çalışma zamanı küçük atlası doğrudan yükleyebiliyor; kaynak PNG'leri paketlemeden eski kaynak yoluna uyumluluk yedeği bırakılıyor.
- Editör senkronizasyonu kaynak çizim/maske dosyalarını StreamingAssets'e kopyalamayı bırakıp runtime atlaslarını alıyor. UI dışı tek seferlik sesler ilk kullanımda yükleniyor; kayıt ve yükleme boyut/süre logları ile pencere durum göstergesi eklendi.
- Statik atlas doğrulaması geçti; Unity test/build ve çalışma zamanı ölçümleri kullanıcı tarafından sona bırakıldı, I4 kabul edilmedi.

## [Unreleased] — Faz I2 simülasyon ayrıntı dilimleri

- Dünya Kanunları'na kaydedilen 1–4 simülasyon dilimi ayarı eklendi. Birim AI kararları UID ve tick ile deterministik biçimde zamanlanıyor; varsayılan bir dilim mevcut davranışı koruyor.
- Dilim dağılımı, ayar sınırları, kayıt turu ve determinism regresyon kaynakları eklendi. Unity derleme/testi ve performans ölçümü kullanıcı talimatıyla çalıştırılmadı; I2 kabulü açık.

## [Unreleased] — Faz I3 birim AI sorgu sadeleştirmesi

- Ölçümde en pahalı sistem görünen `UnitThinkSystem` içindeki sürü yakınlığı kontrolü, aynı karar akışında 40 karo sorgusu zaten boş döndüyse tekrarlı 8/40 karo yakın sorgularını atlıyor. Sonuç davranışını değiştirmeden birim başına gereksiz uzamsal taramayı kaldırıyor.
- Önce/sonra performans ölçümü ve Unity testleri kullanıcı test aşamasına bırakıldı; kazanç doğrulanmadı, I3 kabul edilmedi.

## [Unreleased] — Faz I1 platform performans profilleri

- Gate eşiklerine editör batch, masaüstü, WebGL ve Android profilleri eklendi. PerfBench artık hedef birim/bütçeyi alabiliyor ve raporun editör mü Player mı olduğunu, Unity/platform/cihaz/CPU/GPU bağlamıyla belirtiyor.
- Gate sadece editör batch eşiğini doğrular; Player profilleri hedef olarak tanımlıdır ve gerçek build ölçümleri bekler. Ayrıştırıcı regresyon testleri eklendi; kullanıcı talimatıyla test, derleme ve benchmark çalıştırılmadı.

## [Unreleased] — H8 ayar deneyimi kaynak dilimi

- Ayarlar araması, açılır gruplar ve dokunmatik cihazlarda kısa ayar görünümü eklendi. Arama “Tüm ayarlar” görünümünü açar; varsayılanları yükleme profil ilerlemesini korur.
- Düşük/Dengeli/Yüksek performans önayarları FPS sınırını ve doğa görsel efekt bütçesini değiştiriyor; profil sıfırlama regresyon testi kaynağı eklendi. Unity ve cihaz testleri final aşamaya bırakıldı; H8/Faz H kabul edilmedi.

## [Unreleased] — H7 başarım listesi paylaşımı

- Başarım penceresine Markdown listesini dışa aktarma eklendi; kazanılan/kilitli durumları ve toplam ilerlemeyi gösteriyor. WebGL'de indirme, masaüstünde `persistentDataPath` içine kaydetme mevcut dışa aktarma akışıyla uyumlu.
- Formatter regresyon testi kaynakta eklendi. Unity testleri ve gerçek WebGL indirme kullanıcı isteğiyle final test aşamasına bırakıldı; H7/Faz H kabul edilmedi.

## [Unreleased] — Faz J1 yerelleştirme denetimi

- `Tools/loc_audit.py` katalogdaki kimlik, boş çeviri, `{0}` yer tutucu ve literal `Loc.T`/`Loc.F` anahtarlarını denetler; kalite kapısına eklendi.
- Denetim kullanıcı testleri sona bırakıldığı için çalıştırılmadı; dinamik anahtarlar ve dar ekran metin taşmaları ayrıca doğrulanmalı.

## [Unreleased] — Faz J2 kayıt yedeği

- Kayıtlar yenilenirken önceki dosyanın `.bak` kopyası korunuyor; bozuk/okunamayan ana kayıt varsa yükleme yedeği deneyip kurtarmayı günlüğe bildiriyor. Otomatik afet komutu kuyruğa eklenmeden önce snapshot alınıyor.
- Slot silme `.bak` dosyasını da kaldırıyor; ana kayıt bozulması/yedek kurtarma regresyonu için test kaynağı eklendi. Unity kayıt-yükleme ve kesinti testleri kullanıcı son aşama talimatıyla çalıştırılmadı.

## [Unreleased] — H4 krallık izleme

- Krallık penceresinden izlemeyi açıp kapat; profil en fazla 20 dünya tohumu/harita boyutu/krallık kimliği anahtarı saklar.
- İzlenen krallığı içeren kronik olayları önem eşiğinden bağımsız bildirme ve izleme regresyon kaynağı eklendi. Unity derlemesi/testi kullanıcı son aşama talimatı nedeniyle çalıştırılmadı; H4 tür koruma alanı ve maliyet ölçümü bekliyor.

## [Unreleased] — H4 geri alma kaynak dilimi

### Eklendi
- Büyük güç fırçası ve tanımlı afet müdahalelerinden önce kısa ömürlü save snapshot'ı; HUD düğmesi ve `Ctrl+Z` ile son müdahaleyi geri yükleme.
- Undo snapshot'ları için en fazla beş kayıt ve beş dakikalık saklama; mevcut kayıt biçimi ve ayrı geçici klasör kullanılıyor.
- Dünya karmasının geri yüklenmesini ve snapshot tüketimini kapsayan `UndoTests` kaynağı.

### Doğrulama durumu
- Unity derlemesi/testleri kullanıcı tarafından son aşamada çalıştırılacak. Geri alma performans/maliyet kabulü, tür koruma alanı ve krallık izleme tamamlanmadı; H4 kabul edilmedi.

## [Unreleased] — H4 tür koruma alanı kaynak dilimi

- Tile Details içinden seçili birimin türünü bulunduğu 8×8 bölge için korumaya al veya kaldır; yeni Protection harita katmanı korunan alanları gösterir.
- Yaban avcılarının hedef seçimi korunan türleri aynı bölgenin içinde av dışı bırakır. Koruma işareti kayıt dosyasının geriye uyumlu isteğe bağlı iklim bölümüne eklendi.
- Komut ve save/load regresyon test kaynakları eklendi; Unity derlemesi/testi ve oyun içi kabul kullanıcı talimatıyla çalıştırılmadı. H4 maliyet ölçümü ve Faz H kabulü açık.

## [Unreleased] — H6 erişilebilirlik paleti kaynak dilimi

- Profil ayarlarına renk körlüğüne uygun kimlik renkleri ve yüksek kontrast seçeneği eklendi; seçimler UI ve harita katmanlarına uygulanıyor. Mevcut yazı ölçeği ve ekran sarsıntısı tercihi korunuyor.
- Sekiz ayrı renk ve kararlı kimlik eşlemesi için EditMode regresyon kaynağı eklendi. Unity testi, kontrast ölçümü ve cihaz/görsel kabul kullanıcı testlerinin sonuna bırakıldı; H6 kabul edilmedi.

## [Unreleased] — H1 senaryo ve hedef dilimi

### Eklendi
- İçerik üretecinden gelen sekiz senaryo tanımı; dünya tohumu/şablonu, başlangıç insan nüfusu, hedef/kayıp koşulları ve izinli güçler.
- Yeni dünya penceresinde senaryo seçimi; senaryo başladığında hedef penceresi açılıyor, ölçüler yıl başında güncelleniyor ve sonuç bildiriliyor.
- Senaryo kimliği, başlangıç yılı ve terminal sonucu isteğe bağlı kayıt bölümünde saklanıyor; kazanma/kaybetme sonucu geri dönmüyor, sandbox ve eski kayıtlar varsayılan davranışını koruyor.
- Hedef penceresi artık kaybetme eşiklerini ve mevcut değerlerini de gösteriyor; tetiklenen koşul belirgin biçimde işaretleniyor.
- Senaryo içerik şeması, Türkçe/İngilizce arayüz metinleri ve `ScenarioTests` kaynakları eklendi.

### Doğrulama durumu
- Kullanıcı talimatıyla Unity derlemesi/testleri ve senaryo kazanma-kaybetme oyun doğrulaması son aşamaya bırakıldı; H1 ve Faz H kabul edilmedi.

## [Unreleased] — H2 cihaz uyumlu öğretici metinleri

- Sekiz adımlı rehber masaüstünde fare, dokunmatik cihazda parmak etkileşimine göre yönerge gösteriyor. Adım tetikleme davranışı ve rehberi geçme/yeniden başlatma akışı aynı kaldı.
- Türkçe ve İngilizce metinler UI içerik üretecinden güncellendi. Unity testi ve cihaz kullanılabilirlik gözlemi son doğrulama aşamasına bırakıldı; H2 kabul edilmedi.

## [Unreleased] — H3 güç bulma kolaylıkları

- Güç araması yerelleştirilmiş ad, açıklama, sekme ve İngilizce içerik kimliklerini tarıyor; en son kullanılan altı simülasyon gücü profile kaydediliyor.
- Güç araç ipuçları parametre etkilerini gösteriyor. Durum tabanlı öneriler ve oyun içi kabul henüz tamamlanmadı; H3 kabul edilmedi.

## [Unreleased] — G1 kıtlık ve kaynak savaşı neden zinciri

### Eklendi
- Kıtlık olayları şehir/krallık kimliği ve iklim nedeni ile kroniğe kaydediliyor. Eşik ayındaki gerçek nüfus göçü tek bir olay olarak yazılıyor ve tarih satırı kaynak şehre gidebiliyor.
- Kaynak yetersizliği olarak kodlanmış savaş, taraflarından birinin kıtlık kaynaklı göçüne; göç yoksa yakın tarihli kıtlık kaydına bağlanıyor. Enum sona eklendi; ham geçmiş biçimi ve yan kayıt sürümü değişmedi.
- Kıtlık→göç→kaynak savaşı ilişkisinin ve ilgisiz savaş nedenlerinin bağsız kalmasının regresyon kaynağı eklendi.

### Doğrulama durumu
- Unity derlemesi ve testleri kullanıcı isteğiyle son aşamaya bırakıldı.

## [Unreleased] — G5 önem eşiği ve belgesel kamera

### Eklendi
- Yeni tarih kayıtları ortak önem eşiğiyle bildirilir; eşik profil ayarından değişir ve tarih penceresi filtresiyle paylaşılır. Önceden açılmış geçmiş tekrar bildirim üretmez, mevcut kategori susturmaları çalışır.
- İsteğe bağlı belgesel kamera, gelen önemli konumlu olaylardan sonuncusuna odaklanır. Bildirim tıklaması ilgili harita hedefini açar; hedefi olmayan olaylar tarih penceresine gider.
- Türkçe/İngilizce ayarlar ve eşik regresyon kaynağı eklendi.

### Doğrulama durumu
- Unity derlemesi/testleri ve bildirim yoğunluğu kullanıcı isteğiyle final aşamasına bırakıldı.

## [Unreleased] — G1 savaş öneminde ordu ölçeği

- Savaş kroniğinin önem puanı, savaş ilanı anında iki taraftaki ve müttefiklerindeki savaşçı/birlik sayısını logaritmik %0–20 katkı olarak hesaba katıyor. Krallık nüfusuyla gelen ölçek toplamı yine %20 sınırında.
- Asker sayısı ölçek sınırı ve puan birleşimi için regresyon kaynağı eklendi; Unity testleri son aşamaya bırakıldı.

## [Unreleased] — G1 afet kaynaklı şehir göçü

- Afet isteği 8 karo yakınındaki şehri işaretliyor. Yıllık göç turu bir yıl içinde etkilenen şehirden uygun ve afet görmemiş krallık şehrine yetişkin sakin tahliyesi yapabiliyor.
- Tahliye olayı kişi sayısını ve afet nedenini taşıyor; tarih kaydı afet olayına bağlanıyor ve kronik bunu TR/EN gösteriyor. `HistoryEntry` ile HST1 kayıt biçimi değiştirilmedi.
- Şehir eşlemesi, tahliye penceresi, neden bağı, anlatım ve metadata bayrağı için regresyon kaynakları eklendi; Unity testleri sona bırakıldı.

## [Unreleased] — Daha doğal çayır zemini v5

### Eklendi
- Ada/harita biçimine dokunmadan geniş çimen kümeleri ve mat toprak açıklıkları taşıyan yeni tekrar döşenebilir çayır dokusu eklendi; önceki kaynaklar geri dönüş için korundu.
- Shader'daki parlak ot parıltısı ve ince gürültü azaltıldı; materyal daha geniş alana yayılıyor ve biyom rengi korunuyor.
- Zemin malzemeleri önizlemesi v5 dokusunu gösteriyor.

### Doğrulama durumu
- Kaynak bağlantıları ve import ayarları statik olarak incelendi. Unity derlemesi ve oyun içi görünüm, kullanıcının son test aşamasına bırakıldı.

## [Unreleased] — Daha okunaklı çayır zemini v7

### Eklendi
- Referanstaki ada biçimini kopyalamadan, geniş ve düşük kontrastlı çimen kümeleri, seyrek toprak açıklıkları ve az sayıda kısa ot işareti taşıyan 1024×1024 döşenebilir malzeme üretildi.
- Oyun kaynağı, görsel regresyon girdisi ve tarayıcı önizlemesi v7'ye geçirildi; eski doku sürümleri korundu.

### Doğrulama durumu
- Doku görsel olarak incelendi. Unity derlemesi ve oyun içi görünüm, kullanıcının testleri sona bırakma isteğiyle çalıştırılmadı.

## [Unreleased] — Faz E yıkım savaş hedefi

### Eklendi
- `WarGoal.Destruction`, tamamlanan kuşatmada geçerli savunucu hedef şehrini harabeye çevirip savaşı saldıran tarafın zaferiyle bitiriyor; üçüncü taraf şehirleri ve yanlış hedefler korunuyor.
- Hedef yürütümü ve üçüncü taraf koruması için iki regresyon senaryosu eklendi.

### Doğrulama durumu
- Unity derlemesi/testleri ve çok krallıklı soak kullanıcı testlerinin son aşamasına bırakıldı.

## [Unreleased] — Faz D iklim ve tarım regresyon kaynakları

### Eklendi
- `ClimateAgricultureTests`, mevsim döngüsü, kuraklık eşiği/toparlanması ve kuru tarla verimi ile sulama telafisi için regresyon senaryoları ekliyor.

### Doğrulama durumu
- Unity derlemesi, EditMode ve iklim-soak kullanıcı isteğiyle çalıştırılmadı; overlay, kayıt turu ve iklim-kıtlık zincirinin oyun içi kabulü açık.

## [Unreleased] — Faz C ekosistem regresyon kaynakları

### Eklendi
- `EcosystemTests`, besin ağı verisinin çözülmesini, tür izin listesine göre yerel av sayısını ve bölge biyokütlesinin kaynak tüketimi/harita yeniden kurulumunda tutarlılığını kapsıyor.

### Doğrulama durumu
- Kullanıcının testleri sona bırakma talimatıyla Unity derlemesi ve EditMode çalıştırılmadı; Faz C'nin göç, tükenme, hastalık, leş ve çok tohumlu denge kontrolleri açık.

## [Unreleased] — Faz B ekonomi ve üretim zinciri

### Eklendi
- Şehir büyümesi ve odun stokuna duyarlı meslek kotası için `CityGrowthTests` ve `JobQuotaTests`; demirhanede metal olmadan eşya üretimi ve ekipman verimi için `CraftChainTests` eklendi.
- Tırpan/kazma ekipman davranışları tarla ve taş/maden üretimine bağlandı; kalite ve malzeme çarpanları uygulanıyor. Aletler kullanımda aşınıp kırılıyor; dayanıklılık eski save uyumlu `DUR1` ek bloğunda saklanıyor.

### Doğrulama durumu
- Unity derlemesi, EditMode ve çok tohumlu soak kullanıcı testlerini sona bırakma talimatıyla çalıştırılmadı.

## [Unreleased] — G7 dünya paylaşımı ve dışa aktarma

### Eklendi
- Tarih penceresinden kronik Markdown olarak indirilebilir/kaydedilebilir; dünya özeti ekran görüntüsü ve istatistiklerle PNG'ye, yanında Markdown bilgi dosyasına aktarılır.
- Oturumdaki dünya üretim tarifi `PGW1` paylaşım koduna kopyalanabilir ve yeni dünya ekranında içe alınabilir. Kod sürümlü ve sağlama toplamlıdır; taşınabilir olmayan özel görsel şablonu paylaşım dışıdır.
- WebGL için panoya kopyalama ve dosya indirme köprüsü eklendi.

### Doğrulama durumu
- Unity derlemesi/testleri kullanıcı isteğiyle çalıştırılmadı. Mevcut eski save'lerde üretim tarifi bulunmadığından paylaşım kodu kullanılamaz; kayıt şemasına tarif ekleme açık iştir.

## [Unreleased] — Daha doğal çayır zemini

### Eklendi
- Ada/harita geometrisini değiştirmeden, küçük ot kümeleri ve seyrek toprak açıklıkları içeren özgün çayır dokusu v3 eklendi. Render materyali yeni kaynağı kullanıyor; biyom renkleri ve mevcut zoom davranışı korunuyor.
- Zemin malzemeleri önizlemesi v3 dokusunu gösteriyor.

### Doğrulama durumu
- Kaynak ve önizleme bağlantıları statik olarak kontrol edildi. Unity derlemesi ve gerçek oyun testi, kullanıcının talimatıyla son aşamaya bırakıldı.

## [Unreleased] — F7a bellek göstergesi ve G1–G6 tarih sistemleri

### Eklendi
- Dünya boyutu seçiminin yanında 64 byte/tile kaynak-tabanlı tahmini tile-array çalışma belleği ve 4× düşük çözünürlüklü önizleme tahmini gösterilir.
- Geçersiz harita boyutu ayarı `Medium` boyutuna sınırlandırılır; tahmin hesabı preview çözünürlüğünü de izler.
- Faz G1 kaynak dilimi: 25 tarih türüne JSON önem değerleri, krallık ölçeğine göre sınırlı çarpan, savaş-barış/şehir çatışması neden bağları ve geriye uyumlu Meta history sidecar kaydı eklendi.
- Faz G2 kaynak dilimi: 26 olay için çevrimdışı iki dilli kronik varyantları, gerçek simülasyon adlarıyla UI anlatımı ve sınırlı dünya başına cache eklendi. Buluş keşfi ayrı tarih türüne taşındı.
- Faz G3 kaynak dilimi: önemli kişilerin doğum/aile, hükümdarlık, savaş/kariyer ve ölüm kayıtları ile kişi başına altı olaylık halka ve kalıcı ölüm özeti Meta `HST1 v2` yan bloğuna eklendi; v1 ve eski yan bloksuz kayıtlar destekleniyor.
- Faz G4 kaynak dilimi: çağ geçişi ve yüzyıl özetleri Meta `HST1 v3` yan bloğunda saklanıyor; nüfus grafiği yüklenen dünya raporlarından devam ediyor, kültür/krallık/savaş/kişi ölçümleri rapora yazılıyor. V1 ve v2 yan blokları geriye uyumlu.
- Faz G5 kaynak dilimi: önemli olay bildirimleri tıklanınca ilgili şehre/başkente odaklanıyor ve ayrıntısını açıyor; yeni şehir bildirimi de bu akışa bağlandı.
- Faz G6a kaynak dilimi: tarih penceresine tüm/önemli olay filtresi, arama, 100'lük sayfalama ve şehir/krallık/savaş satırlarından nesneye gitme eklendi.
- Faz G6b kaynak dilimi: tarih penceresinde önemli kişiler için arama/biyografi ve isyan kayıtlarından krallık soyağacı eklendi; seçili biyografi yeni olaylarla yenilenir.
- Faz G6c kaynak dilimi: tarih hedeflerine kamera odağıyla birlikte, ekrandaki hedefe bağlı, süreli ve etiketi olan harita işaretçisi gösterilir.

### Doğrulama durumu
- Tahmin alanı/preview ölçeği regresyonu yazıldı. Kullanıcının son test isteğiyle test, içerik/Unity derleme ve gerçek device-memory profili çalıştırılmadı; bu bir cihaz benchmark'ı değildir.
- Tarih önem kapsamı, puan sınırları ve savaş-barış kayıt turu için regresyon kaynakları yazıldı; kullanıcı talimatıyla çalıştırılmadı.
- Kronik şablon kapsamı ve çeviri/cache davranışı için regresyon kaynağı yazıldı; kullanıcı talimatıyla çalıştırılmadı.
- Biyografi halka sınırı/sırası, ölüm özeti save turu ve eski HST1 v1 okuması için regresyon kaynakları yazıldı; kullanıcı talimatıyla çalıştırılmadı.
- Çağ/yüzyıl summary save round-trip'i ve aynı raporun yinelenmesini önleme için regresyon kaynağı yazıldı; kullanıcı talimatıyla çalıştırılmadı.
- Bildirim gezinmesi kaynak incelemesiyle bağlandı; kullanıcı talimatıyla UI/oyun testi çalıştırılmadı.
- Tarih penceresi filtresi ve gezinti kaynak değişikliği tamamlandı; kullanıcı talimatıyla UI/Unity testi çalıştırılmadı.
- G6b/G6c UI kaynakları eklendi; seçilen biyografi güncellenmesi ve marker çizimi için Unity UI testi/gerçek oyun kabulü kullanıcı testlerinin sonuna bırakıldı.
- Biyografi halka sınırı/sırası, ölüm özeti save turu ve eski HST1 v1 okuması için regresyon kaynakları yazıldı; kullanıcı talimatıyla çalıştırılmadı.

## [Unreleased] — F6b yeni dünya biçimleri

### Eklendi
- İç deniz, nehir havzaları ve dağlık takımadalar için üç ayrı arazi maskesi/noise/erozyon preset'i eklendi.
- Şablon seçimi, tanımlıysa dağ/tepe/nehir/nem ayar varsayılanlarını uygular; olmayan varsayılanlar oyuncunun mevcut ayarını korur.

### Doğrulama durumu
- Şablon-maskesi ve varsayılan regresyonu yazıldı. Kullanıcının son test talimatıyla içerik doğrulaması, Unity/önizleme çalıştırılmadı.

## [Unreleased] — F6a dünya kurulumunda nehir yoğunluğu

### Eklendi
- Dünya oluşturma ekranındaki yeni TR/EN nehir yoğunluğu kaydırıcısı, önizleme ve gerçek üretimde aynı `WorldGenSettings` değerini kullanır.
- Sıfır değer nehirleri kapatıp gölleri korur; yüksek değer akış eşiğini düşürerek daha sık nehir kanalı üretir.

### Doğrulama durumu
- Kullanıcının testleri son aşamaya bırakma talimatıyla test/derleme ve önizleme çalıştırılmadı.

## [Unreleased] — F5a jeolojik kaynak dağılımı

### Eklendi
- Maden seçimi yükselti profili ve seçili damarlar için biyom filtresi kullanıyor; nehir hücreleri başlangıç damarlarından korunuyor.
- Büyük kara kütlelerindeki eksik bakır, demir, taş ve odun kaynaklarını deterministik küçük kümelerle tamamlayan kontrol eklendi; ağaç seçiminde yerel biyom türleri önceliklendiriliyor.
- Kaynak grubu adaletini kara kütlesi başına doğrulayan EditMode regresyonu yazıldı.

### Doğrulama durumu
- Kullanıcının testleri son aşamaya bırakma talimatıyla test/derleme, çok tohumlu kaynak dağılımı ve üretim süresi çalıştırılmadı.

## [Unreleased] — F4a yükselti ve rüzgâr yağış gölgesi

### Eklendi
- Başlangıç yağış gölgesi, seed'e bağlı ve oyunun ilk `WindSystem` yönüyle aynı rüzgâr vektörünü izliyor; dağın rüzgâr alan yamacına hafif nem, rüzgâr altına kuruluk veriyor.
- Sürekli yükselti başlangıç sıcaklık kuşaklarını yumuşak biçimde soğutuyor; iklim alanından zone başlangıç nemi oluşturuluyor ve biyom tablosu aynı nem alanını kullanıyor.

### Doğrulama durumu
- Rüzgâr yönlü relief ve zone nemi için EditMode regresyon kaynağı yazıldı. Kullanıcının son test isteğiyle test/derleme/çok tohum dengesi çalıştırılmadı.

## [Unreleased] — F3a şablon kontrollü hafif arazi erozyonu

### Eklendi
- Yükseklik sınıflandırmasından önce akış birikimine göre dar kanalları hafifçe oyan ve düşük eğimli kanal uçlarında az miktarda birikim yapan Burst işi eklendi.
- Erozyon kuvveti dünya şablonuna göre ayarlanır; düz/boş/görsel haritalar değişmeden kalır, yüzdelik kara oranı sınıflandırması korunur.
- Kıyı sınırında yalnızca kıyı hücrelerine düşük kuvvetli yükseklik harmanlama eklendi; son histogram ve arazi eşikleri yeni yüksekliklerden yeniden hesaplanıyor.

### Doğrulama durumu
- Determinizm, akış kanalı ve yükseklik sınırı için EditMode testi yazıldı. Kullanıcı testleri en sona bırakma isteğiyle test, derleme, süre ölçümü ve galeri çalıştırılmadı.
- Kıyı su/kara geçişi ve kıyı dışı değişmezlik için de EditMode regresyon kaynağı eklendi; çalıştırılmadı.

## [Unreleased] — F2b nehir kıyısı nemi ve yüzey ayrımı

### Eklendi
- Nehirli veya kıyısındaki 8×8 zone'larda başlangıç nemi artar; nehir içeren zone aylık küçük nem yenilemesi alır. Mevcut tarla büyümesi ve hasat formülü bu nemi tüketir.
- Nehir kenarı toprağı ayrı, düşük kontrastlı yosun ve ıslak toprak dokusuyla gösterilir.
- Şehir yer seçimi nehir kıyısını su kıyısı gibi değerlendirir; bina ayak izleri nehir hücrelerini kullanmaz. Nehir temizlenince yakın yüzey chunk'ları da yenilenir.

### Doğrulama durumu
- Başlangıç/aylık nem karşılaştırma testi yazıldı. Kullanıcı isteğiyle test, derleme ve uzun dönem mahsul/kuraklık soaku çalıştırılmadı.

## [Unreleased] — F2a akış birikimi ve sığ nehirler

### Eklendi
- Sürekli yükseklik alanında deterministik priority-flood; kapalı çukurlarda sınırlı sığ göl ve suya ulaşan yürünebilir akış kanalları.
- Nehir yüzey tonu ve hafif animasyon; başlangıç bitki/maden yerleşimi kanallardan kaçınır, arazi düzenlemesi nehir işaretini kaldırır.

### Doğrulama durumu
- Akış/yürünebilirlik regresyon testleri eklendi. Kullanıcı isteğiyle test, derleme, çok tohumlu soak ve görsel kabul çalıştırılmadı; F2 kabul edilmedi.

## [Unreleased] — F1 arazi sırtları

### Eklendi
- Kıtalar, tek kıta, takımadalar, halka ve göl şablonlarına yüksek arazide belirginleşen, şablon başına ayarlanabilir dağ sırtı etkisi eklendi.
- Sırt etkisi mevcut noise örneğini yeniden kullanır; yeni octave eklemez ve hedef kara oranı yüzdelik sınıflamasıyla korunur.

### Doğrulama durumu
- Determinizm/yükseklik sınırı regresyonu eklendi. Unity testi, worldgen görsel galerisi ve üretim performans ölçümü kullanıcı son test aşamasına bırakıldı.

## [Unreleased] — E4d ticaret antlaşması, ambargo ve kıtlık ithalatı

### Eklendi
- Ticaret anlaşması alıcıya %10 fiyat indirimi, taşıma giderine %25 indirim verir; rota seçimi ve teslim aynı koşulları uygular.
- Yiyecek stoğu azalan şehirlerin gıda ithalatına rota seçiminde 1.25×–2.5× öncelik verilir.
- 10 yıllık karşılıklı ticaret ambargosu AI teklifi, kayıt/ilişki durumu ve vali eylemi eklendi; ambargo yeni rotaları ve teslimi durdurur, ticaret anlaşmasıyla çakışmaz.
- Policy save biçim 8 yeni ambargo türlerini tanır; biçim 1–7 geriye dönük okunur.

### Doğrulama durumu
- Ticaret etkileri ve ambargo kayıt turu için EditMode regresyonları eklendi. Kullanıcı talimatıyla Unity testi/derleme/soak çalıştırılmadı.

## [Unreleased] — E5a açlık huzursuzluğu ve veraset krizi

### Eklendi
- Uzayan kıtlık bağlılığı düzenli düşürür; isyan baskısı 20 puanda sınırlanır.
- Hükümdarın yetişkin rakip çocuklarından biri başka bir şehirdeyse, düşük bağlılıkta hak iddiası ayrılık ve `war.succession` iç savaşına dönüşebilir. Oyuncu seçimiyle atanmış varis bu otomatik krizi önler.

### Doğrulama durumu
- Kriz ihtimali ve kıtlık baskısı regresyonları yazıldı; Unity testi/derleme/soak çalıştırılmadı. Vergi politikası ve Faz E kabulü açık.

## [Unreleased] — E5b değişken vergi politikası

### Eklendi
- Vali `SetPolicy` eylemi 0–100 vergi düzeyi alıyor. 50 dengeli gelir ve nötr mutluluk verir; düşük vergi geliri azaltıp mutluluğu yükseltir, yüksek vergi geliri artırıp mutluluğu düşürür.
- Vergi oranı mevcut şehir mutluluğuna ve dolayısıyla bağlılık/isyan hesabına bağlandı. Policy save biçim 9'da korunur; biçim 1–8 dengeli vergiyle yüklenir.

### Doğrulama durumu
- Vergi sınırları ve save round-trip regresyonları yazıldı; Unity testi/derleme/soak çalıştırılmadı.

## [Unreleased] — E4c limanlar arası deniz ticareti

### Eklendi
- Kara pazar rotası bulunamadığında rıhtımlı şehirler arasında su yolu erişimi olan dost limanlara ticaret teknesi gönderiliyor.
- Tekne stok fazlasını ihtiyaç, depo, altın, fiyat ve mesafe giderine göre taşır; savaş/başarısız kârlılıkta yükü kaynağa geri getirip tekneyi limana döndürür.
- Mevcut `BoatData` raw kayıt yapısı ve `BoatType` enum değerleri korunuyor.

### Doğrulama durumu
- Tekne tesliminin altın, kapasite ve mesafe gideri sınır testi eklendi. Kullanıcı isteğiyle Unity test/derleme/soak çalıştırılmadı; E4c oyun içinde doğrulanmadı.

## [Unreleased] — E4b kara ticaret rotası ve taşıma maliyeti

### Eklendi
- Pazar, aynı kara adasındaki dost alıcıları ihtiyaç, fiyat, altın ve mesafeye göre değerlendirip en yüksek pozitif net gelirli rotaya kervan çıkarıyor.
- Miktar/mesafeyle artan taşıma gideri satıcı gelirinden düşüyor; teslimde gerçek depo kapasitesi ve savaş durumu kontrol edilerek artan yük kaynağa dönüyor.

### Doğrulama durumu
- Mesafe/miktar maliyet testi eklendi. Kullanıcı isteğiyle Unity testi/derleme/soak çalıştırılmadı; deniz ticareti açık.

## [Unreleased] — E4a pazar fiyatı ve teslim güvenliği

### Eklendi
- Pazarlar, alıcı şehrin stokuna göre 0.5×–1.5× aralığında arz-talep fiyatı uyguluyor; ticaret başlangıcında ve teslimde alıcının güncel stoğu/altını kontrol ediliyor.
- Kervan yoldayken savaş çıkarsa teslim etmiyor; kısmi satışta kalan yük kaynağına geri dönüyor.

### Doğrulama durumu
- Kıtlık/fazla stok fiyat testi yazıldı. Kullanıcı isteğiyle test/derleme/soak çalıştırılmadı; E4a henüz oyun içinde doğrulanmadı.

## [Unreleased] — E3c yağma hedefi

### Eklendi
- Başarılı kuşatma, yağma hedefli savaşta şehrin altınının %25'ini ve sivil rezerv üzerindeki yiyecekten ordunun boş taşıma kapasitesi kadarını alıyor; şehir bağlılığı 20 azalıyor ve şehir el değiştirmiyor.
- Yağma altını ana şehre aktarılıyor, erzak ordu yan kaydına ekleniyor; hedef tamamlanınca savaş saldıran tarafın zaferiyle kapanıyor.
- `CityPillaged` TR/EN dünya tarihçesi olayı eklendi; eski olay enum değerleri korundu.

### Doğrulama durumu
- Loot sınır regresyonu yazıldı. Kullanıcı isteğiyle testler/derleme/soak çalıştırılmadı; E3c oyun içinde doğrulanmadı.

## [Unreleased] — E3b ordu donanımı ve kuşatma kaynak dilimi

### Eklendi
- Şehir suru, gözetleme kulesi, kapı binası ve salon seviyesi fetih öncesi kuşatma süresini belirliyor. Savunma askerleri şehirdeyken ve ordu yaklaşmamışken süre ilerlemiyor.
- Silah/zırh/kalite/malzeme etkileri mevcut birim istatistiklerinden kuşatma gücüne aktarılıyor. Kuşatma ilerlemesi Policy biçim 7'de korunuyor ve savaş penceresinde gösteriliyor.

### Doğrulama durumu
- Savunma/donanım süre karşılaştırması ve kuşatma kayıt turu testleri yazıldı; talimat gereği çalıştırılmadı. Unity derlemesi ve savaş soaku son kabul aşamasında.

## [Unreleased] — E3a ordu ikmali ve moral kaynak dilimi

### Eklendi
- Ordu erzağı ana şehre 30 birim mesafede stoktan ikmal alıyor; uzakta şehir yiyeceği askerlere aktarılmıyor. Erzak doygunluğu yeniliyor, eksik ikmal moral kaybettiriyor ve düşük moral birliklerin daha erken geri dönmesine yol açıyor.
- Ordu erzak/moral durumu ham `ArmyData` kaydı değişmeden Policy biçim 6 yan listesinde saklanıyor. Savaş penceresi asker, moral ve erzak bilgisini gösteriyor.

### Doğrulama durumu
- Morale ve kayıt turu regresyonları yazıldı; kullanıcı talimatıyla çalıştırılmadı. Derleme, oyun testi ve denge soaku son kabul aşamasında.

## [Unreleased] — E2b barış şartları kaynak dilimi

### Eklendi
- Barış teklifleri sabit altın haraç, şehir devri, vasallık ve saldırmazlık süresi taşıyor; savaş hedefi haraç/vasallık için otomatik teklif şartları hazırlanıyor.
- Kabulde altın kaynak krallığın mevcut şehirlerinden alıcı başkentine gider; şehir yalnızca gerçek sahibinden ve son şehir değilse devredilir. Teklif özeti diplomasi penceresinde yerelleştirilmiş biçimde görünür.
- Policy kayıt bölümü biçim 5'e yükseltildi; eski biçim 1–4 okumaları korunuyor.

### Doğrulama durumu
- Altı regresyon testi yazıldı; kullanıcı talimatıyla çalıştırılmadı. Unity derlemesi ve oyun testi son kabul aşamasında.

## [Unreleased] — Faz E2a savaş nedenleri ve hedefleri

### Eklendi
- `war_types.json` neden/hedef izin listeleri üretici ve şemaya eklendi; her ilan bu içerik sınırları içinde casus belli ve stratejik hedef alıyor.
- Hedef şehir fethi tamamlanınca veya savaş yorgunluğu 80/100'e ulaşınca mevcut barış teklif akışı tetikleniyor. Savaş sonlanınca karşıt taraflar arasındaki eski barış teklifleri temizleniyor.
- Sebep/hedef, ham `WarData` düzenini değiştirmeden Policy bölüm 16 biçim 4'te ve savaş tarihçesinde saklanıyor; tarih penceresi bunları Türkçe/İngilizce gösteriyor. Biçim 1–3 okuyucuları korunuyor.

### Doğrulama durumu
- Hedef, kayıt gidiş-dönüşü ve biçim 3 göç testleri eklendi; kullanıcı talimatıyla çalıştırılmadı. Unity derlemesi, oyun testi ve soak son kabul aşamasında.

## [Unreleased] — Doğal çayır malzemesi v2

### Değişti
- Tekrarlı çim katmanı orta ölçekli ot kümeleri ve seyrek toprak izleri olan yeni el boyaması dokuya bağlandı. Shader, toprağa hafif sıcak renk verir; harita geometrisi, biyom taban renkleri ve simülasyon etkilenmez.
- `terrain-materials.html` önizlemesi ve görsel notları yeni dokuyu gösteriyor. Eski doku geri dönüş için korunuyor.

### Doğrulama durumu
- PNG boyutu ve kenar sürekliliği statik olarak incelendi. Unity/shader derlemesi ile oyun içi görünüm ve performans, kullanıcının talimatıyla son kabul aşamasına bırakıldı.

## [Unreleased] — Diplomasi E1a–E1b kaynak dilimleri

### Eklendi
- Süreli ticaret ve saldırmazlık antlaşmaları, tek yönlü vasallık ve haraç; `MetaWorld` diplomatik durum sorgusu, bölüm 16 kayıt biçimi 3 ve biçim 1/2 eski kayıt okumaları.
- Vali/AI ticaret, saldırmazlık ve vasallık teklifleri; kabul/ret ekranı ve bildirimlerinde teklif türü gösterimi.
- Ticaret antlaşması ticaret rotasını açar; saldırmazlık antlaşması hem AI hem komut düzeyinde savaş ilanını engeller.
- Tek yönlü vasallık teklifi, kayıt biçim 3, yıllık %5 altın haraç aktarımı, dünya geçmişi olayı ve ilişki/vali görünümü.

### Doğrulama durumu
- Kaynak incelemesi, JSON anahtar denetimi ve diff boşluk kontrolü yapıldı. Unity/oyun testleri, kayıt turu ve çok krallıklı soak kullanıcı isteğiyle son test aşamasına bırakıldı. Barış şartları/manual elçi UI'sı ve E1 kabulü tamamlanmadı.

## [Unreleased] — Medeniyet ekonomisi B fazı dilimi

### Eklendi
- Şehir başına ekonomi tanı JSON'u, yiyecek akış sayaçları ve yıllık büyüme darboğazları; boş bina alanı için kapsamlı yer arama ve daha geniş salon yükseltme alanı.
- Konut/yiyecek durumuna bağlı doğurganlık, kaynak açığına göre oduncu/taşçı kotaları, odun/kemik ile erken demirhane, işsizlik ve yiyecek fazlası ile göç tetikleri.
- Pazar ticareti alıcının kaynak ihtiyacı ve altın ödeme gücünü denetliyor; mercantile özelliği vergi gelirini etkiliyor. Araştırma nüfus ve bilgin emeğinden puan alıyor.

### Doğrulama durumu
- Unity derlemesi, EditMode, soak ve performans koşuları kullanıcı testleri son aşamaya bırakıldığı için yapılmadı; B fazı kabul edilmedi.

## [Unreleased] — Ekosistem C1 besin ağı verisi

### Eklendi
- 92 tür için üretilebilir beslenme ağı listeleri, trofik seviye, vücut boyutu ve normalize besin gereksinimi; içerik şeması ve runtime av seçimleri bu veriyi kullanıyor.

### Doğrulama durumu
- Unity içerik yüklemesi/derlemesi, ekoloji testleri ve 200 yıllık wild soak çalıştırılmadı; C fazı kabul edilmedi.

## [Unreleased] — Yol haritası A fazı altyapısı

### Düzeltildi
- Ortak düşman hesabı, iki ayrı savaş kaydında aynı saldırgana karşı savaşan krallıkları da tanıyor. Vali ayarlarında oyuncunun devraldığı krallık, kullanılan beyin türünden bağımsız olarak istek kapsamından çıkarılıyor ve bu tercih geri açıldığında kayıtlı durumdan temizleniyor.
- Vali HTTP testleri türetilmiş hata türlerini kabul ediyor; yerel HTTP denemesi artık geçerli 64×64 harita boyutunu kullanıyor. İcat kayıt testi yükleme hedefinde kaynak krallıkla aynı kimlikte krallık kuruyor. GPU gerektiren renk testi ayrı `Graphics` kategorisine taşındı.

### Eklendi
- `CivSoak` için yıl sonu nüfus/tür/şehir/savaş/kıtlık/üretim metrikleri JSON raporuna yazılıyor. `Tools/civ_ab.ps1 -Scenario multi -Years 200 -Seeds @(...)` çok halklı senaryoyu seçebiliyor; `Tools/metrics_compare.py` raporları tohum ve senaryo bazında karşılaştırıyor.
- `Tools/run_unity.ps1 -Mode Graphics`, grafik bağlamı gerektiren EditMode testlerini ayrı çalıştırabiliyor. `Tools/gate.ps1` ve eşik JSON'u derleme, test, soak ve performans adımlarını sonuç dosyalarıyla denetliyor. `DeterminismTests` aynı seed/komut ve kayıt sonrası devamı kapsıyor.

### Doğrulama durumu
- Kaynak ve test değişiklikleri eklendi; Unity EditMode, Graphics, çok tohumlu soak ve kalite kapısı henüz çalıştırılmadı. Kullanıcının isteğiyle oyun testleri tüm geliştirme fazlarının sonuna bırakıldı.

## [Unreleased] — Birimler üst üste yığılmıyor

### Düzeltildi
- Birimler, canavarlar ve kalabalıklar artık tek bir noktada yığılmıyor. Önceden hiçbir ayrışma yoktu: savaşta birimlerin %99'u, doğal dünyada %18'i başka bir birimin üstündeydi (güçle çağrılan 40 insan tek bir sprite gibi görünüyordu). Yeni `UnitSpacingSystem` (Burst) yakın birimleri hafifçe iter; saldıran–hedef ve çiftleşen çiftler, hareketsiz/oyuncu/gemideki/havadaki birimler hariç. Ölçüm (`CrowdProbe`): savaş %0–2, doğal dünya %2, büyük köy %5 (hedef < %10).
- Varış noktası artık karenin tam merkezi değil birime özel bir nokta; yol ara noktaları hafifçe sapar, varış 0,3 kare toleransla sayılır. Güçle çağrılan gruplar `√sayı` ile büyüyen bir diske en az 0,55 aralıkla yerleşir. Kaçan birimler ±35° ve 10–16 kare ile yelpaze gibi açılır; yakın dövüşte saldırganlar hedefin merkezine değil kendi tarafındaki kendi noktalarına yaklaşır. Yabani otlayıcılar en kalabalık olmayan bitkiyi seçer.

### Eklendi
- Araçlar: `run_unity.ps1 -Mode CrowdProbe|CrowdPreview|Perf|CivSoak`, `Tools/probe_unity.ps1` (sonuç satırı görünce Unity'yi kapatır), `Tools/civ_ab.ps1` (çok tohumlu `CivSoak`; ekonomiyi etkileyebilecek değişikliklerde tek tohum yanıltır), `Tools/test_summary.py`. `UnitSpacingTests` (11 test).
- Denendi ve geri alındı: birime özel ±%8 hız çarpanı. 6 tohumda en yüksek nüfus ortalaması ~84'ten ~51'e düştü (DECISIONS #114).

### Bilinen
- `PerfBench` 5.000 birimde ayrışma için ~0,75 ms/tick (editörde, güvenlik denetimleriyle) gösteriyor; toplam tick bu makinede 9,5–10,7 ms arasında oynuyor (bütçe 10 ms), aynı simülasyon ölçümleri arasında bile ~1 ms fark var. Derlenmiş oyunda daha düşük olması beklenir, doğrulanmadı.

## [Unreleased] — Derinlik sıralaması

### Düzeltildi
- Ağacın ya da çatının arkasındaki birimler artık kısmen gizlenir; önündekiler üstte çizilir. Önceden birimler ayrı bir katmanda olduğu için her zaman ağaç tacının ve binanın üstünde duruyordu. Ağaç, bina, birim ve araç ortak derinlik tamponuyla ayak çizgisine göre sıralanır (`SortingLayers.DepthZ`, `_ZWrite`). Uçan birimler her şeyin üstünde kalır; cesetler canlıların arkasına çizilir.
- `HousePreview.cs` içindeki `EntityId` belirsizliği (CS0104) giderildi. Android en düşük sürümü API 24'ten 25'e çıkarıldı (Unity eski sürümü kaldırıyor); Android 7.0 cihazlar artık desteklenmiyor.

### Eklendi
- Derinlik önizleme araçları: `Tools/run_unity.ps1 -Mode DepthPreview` (ağaç + ev + düşük yapılar) ve `-Mode ForestPreview` (gerçek orman); çıktılar `Logs/depth-*.png`. `DepthSortingTests` EditMode testleri.

## [Unreleased] — Tarayıcı AI bağlantı ayarları

### Eklendi
- Oyun içi krallık valisi ayarlarında bulut/yerel profil için düzenlenebilir uç nokta ve model, maskeli API anahtarı girişi, dış anahtar kaynağını seçme ve doğrulamalı kaydetme. WebGL bağlantı denemesi ve karar çağrıları UnityWebRequest üzerinden asenkron gider; ayarlar IndexedDB'ye yazılır, girilen anahtar yazılmaz ve sayfa yenilenince tekrar girilir.

### Düzeltildi
- Profil değiştirince alanlar ilgili değerleri yükler. Bir ayar test edilirken değiştirilmez. HTTP hata gövdeleri/anahtarlar uygulama günlüğüne kopyalanmaz; tekrar eden masaüstü istemci çağrılarının süre aşımı bağımsızdır.
- WebGL isteği için sağlayıcıda oyun alan adına CORS gerekir. Kaynak derlendi; Unity lisansı/koşul ekranı nedeniyle Unity Test Runner ve güncel yayın paketi henüz doğrulanmadı.

## [Unreleased] — Dört halkın karakter çizimleri

### Eklendi
- İnsan, ork, cüce ve elf için farklı silüetler ve 24'er poz içeren özgün karakter sayfaları. Ortak ayak tabanlı, sekiz renk indeksli yükleyici mevcut krallık renklerini ve yaş ölçeğini korur. Bekleme/nefes, yürüme, saldırı, yüzme ve uyku canlı birim çizimine bağlıdır; düşüş pozlarının ceset çizimi bağlantısı beklemektedir.
- Görsel tarama ve öncelikli yol haritası: `Docs/Visual_Upgrade_2026-10-04.md`. Etkileşimli varlık önizlemesi: `Docs/previews/units.html`.

### Düzeltildi
- Karakter animasyonu simülasyon zamanını izler; duraklatmada devam etmez. Hareketsiz saldırı/takip görevi yürüyüşü tek başına tetiklemez; saldırı pozları cooldown boyunca tekrar tekrar oynatılmaz.
- Kaynak ve test derlemesi kontrol edildi; Unity koşul/lisans engeli nedeniyle oyun içi doğrulama ve yeni WebGL derlemesi bekliyor. Bu değişiklik eski hosting ZIP'inde yoktur.

## [Unreleased] — Özgün sesler ve atmosfer

### Eklendi
- 61 özgün WAV: yumuşak tahta/çan arayüz efektleri, inşa/yaşam/savaş/güç efektleri, okyanus/orman/şehir/ateş/yağmur/rüzgâr döngüleri ve on çağ için eş uzunlukta sakin/gergin müzik katmanları. Çevre ve müzik sesleri kamera/çağ/savaş durumuna göre mevcut ses sistemiyle karışır. Toplam 28,4 MiB; efektler önceden, çağ müzikleri ihtiyaç olduğunda yüklenir.

### Düzeltildi
- Asenkron yüklenen ses dosyası artık devam eden ilgili ortam/müzik döngüsünün geçici klibinin yerini alır. Yüklenirken döngü sessiz kalır, ardından mevcut geçişle açılır; hâlen çalan kısa efekt kesilmez. Dünya kapalıyken müzik de sessizleştirilir. Ses ayarları korunur.
- Ses üretimi ve WAV doğrulama araçları eklendi. Yeni WebGL derlemesi Unity lisans/koşul ekranı nedeniyle henüz oluşturulamadı; eski hosting ZIP'i bu ses güncellemesini içermez.

## [Unreleased] — Harita konum ayrıntıları

### Değişti
- Fare imleci oynatıldıkça üst çubukta değişen koordinat/zemin metni kaldırıldı. Güç aracı seçili değilken haritaya kısa tıklama, arazi, biyom, sıcaklık ve varsa birim/şehir kısayollarını tek bir ayrıntı penceresinde gösterir; harita sürükleme ve güç kullanımı bu pencereyi açmaz.

## [Unreleased] — Mobil (Android) uyarlama

### Eklendi
- Dokunmatik kamera: iki parmakla kaydırma (bırakınca fling ataleti) + kıstırma zoom; brush aracı seçili değilken tek parmak sürükleme haritayı kaydırır; tek parmak güç kullanımı; kamera jesti sırasında fırça boyaması geçici olarak durur (`CameraController.TouchSteering`).
- Mobil HUD (`pg-touch` modu, yalnız `Application.isMobilePlatform`): araç şeridi/üst bar/alt bar düğmelerinde büyük dokunma hedefleri, ekran üstü zoom (+/−) düğmeleri, `Screen.safeArea` uyumlu HUD iç boşlukları (delikli/rounded ekranlar), esaret modundan çıkış düğmesi.
- Android derleme akışı: `PixelGenesis/Build Android APK` menü öğesi ve `PG.Editor.BuildGame.Android` — IL2CPP (ARMv7+ARM64), ASTC doku sıkıştırma, minSdk 24, INTERNET izni (Krallık Valisi HTTP istemcisi için), yatay yönlendirme kilidi.
- Araçlar: `Tools/run_unity.ps1 -Mode AndroidBuild` (batch APK derlemesi) ve `Tools/deploy_android.ps1` (adb ile kur + başlat + logcat özeti).

## [Unreleased] — Dinamik ittifaklar ve ittifak tanrı gücü

### Eklendi
- Dinamik ittifak mekanizması (`KingdomSystem.WantsAlliance`): Krallıkların ittifak kurma görüş eşiği ulaşılamaz 60 değerinden dengeli 25 değerine düşürüldü.
- Ortak düşman koalisyonu: Aynı düşmana karşı savaşan krallıklar nötr görüşte bile (>= 0) ittifak kurabilmektedir ("Düşmanımın düşmanı dostumdur"). Zayıf krallıklar güçlü dost komşularından himaye arayabilir (güç < %70, görüş >= 15).
- Oyuncuya ve valiye ittifak daveti: Yıllık ittifak döngüsünde AI krallıklar, oyuncu veya vali kontrolündeki krallıklarla ittifak kurmak istediklerinde doğrudan atlamak yerine `DiplomacyProposal` ile teklif gönderir; ekranda kabul/ret bildirimi çıkar.
- İttifaktan ayrılma (`MetaOrder.LeaveAlliance` & `KingdomSystem.LeaveAlliance`): Krallıklar ittifaktan ayrılabilir; müttefik sayısı 1'e düşünce ittifak feshedilir. `MetaControlWindow` ve AI Governor tarafından desteklenir.
- Yeni Tanrı Gücü `pw.alliance` (İttifak): Oyuncu haritada iki krallığın şehirlerini seçerek onları doğrudan müttefik yapabilir; aralarında aktif savaş varsa barışla sonlandırılır.
- `AllianceTests` EditMode test paketi eklendi: karşılıklı görüş, ortak düşman koalisyonu, oyuncuya teklif gönderme, ittifaktan ayrılma ve tanrı gücüyle doğrudan müttefik yapma doğrulandı.

## [Unreleased] — Atmosfer ve arazi cilası (gün/gece, mevsim, rüzgâr, su)

### Eklendi
- Zemin mikro dokusu: `ChunkColorJob` artık karo başına deterministik hash dither'i (~±%5) uyguluyor; geniş düz biyom alanları doku kazanıyor (DECISIONS #101).
- Su cilası: kıyıya değen su karoları köpük bandına açılıyor (çift komşuda güçlü), shallow→ocean→deep_ocean sınırları komşu paletleriyle yumuşak geçişe bağlıyor; köpük pikselleri shader'da nabızla parlıyor.
- Gün/gece ışık döngüsü (salt görsel): ay içinde dönen `_DayLight` global uniformu — gece mavi-lacivert, şafak/söğüm sıcak burni; sim determinizmine dokunmaz, Ayarlar'dan kapatılabilir (`ui.day_night`).
- Mevsimsel yaprak tonu: `law.seasons` açıkken ağaç/özellik sprite'ları mevsime göre `_SeasonTint` ile renkleniyor (sonbahar sarısı, ilkbahar yeşili, kış solukluğu), aylar arasında yumuşak geçiş.
- Rüzgâr sallanması: özellik sprite'ları taç üstü vertexleriyle sim rüzgâr yönü/hızına göre sallanıyor (`_Sway` + `_WindDir`); taban sabit kalır, bulutlar/overlay'ler etkilenmez.
- FX: patlamalara genişleyen şok dalgası halkası ve yükselen duman pufları eklendi; yanan karolardan sürekli ince duman, yağmur bulutlarının altında yağmur çizgileri ve kar bulutlarının altında süzülen kar tanecikleri (kamera görünür alanında, stateless/GC-sız).
- Sanat üretim rehberi: kalan 5 bina stili (elf/cüce/ork/hayvan/böcek) ve Final birim sayfaları için görsel rehber ve üretim standartları tanımlandı.

## [Unreleased] — Görsel kalite geçişi: birimler, ikonlar, arazi, FX

### Değişti
- Birim sprite'ları yeniden yazıldı: 92 tür artık tür bazlı siluete çıkıntıları (kulak, boynuz, yele, ibik, anten, yüzgeç), nefes alan bekleme animasyonu, 4 kare yürüyüş döngüsü ve üç pozlu saldırıyla çiziliyor; böcek/yılan/balık gövde planları gerçek okunur şekillere kavuştu (DECISIONS #100).
- HUD güç ve favori düğmeleri 16 px piktogram ikonlar aldı — harf kutularının yerini aile bazlı şekil dili (meteor, alev, yıldırım, kafatası, ağaç, kılıç…) aldı; ikonlar StreamingAssets'ten bir kez çözümlenip önbelleklenir.
- Arazi okunabilirliği: 18 karo ve 29 biyomun doku varyantları görünür kontrasta çıkarıldı (±4 → ±10-14 RGB).
- Krallık renkleri birimlere indi: uygar birimler krallıklarının sancak rengiyle kumaş taşır; krallık kurulduğunda palet otomatik büyür; vahşi türler kürk tonlarına döndü.
- Güç efektleri kimlik kazandı: her güç kendi renk damlalarıyla düşer (iz ve iniş parıltısıyla), yıldırım çarpması jagged çakma çizer.
- Fırça imleci kalın kontur + nabızlı dolguya geçti ve seçili gücün sekme rengine tonlanır; mini haritada başkentler parlak işaretlerle, isim plakalarında gölgeyle okunurluk arttı.

### Sınırlar
- Bu geçiş tamamen prosedüreldir: birimler Final kalitesinde değil, gelişmiş yer tutucudur; binalar ve ağaçlar (zaten Final/yüksek kalite) değişmedi.

## [Unreleased] — "Koyu Zarif" arayüz teması

### Değişti
- Tüm menüler, pencereler ve HUD tek token kaynağına bağlandı: `PG_Hud.uss` `:root` paleti + C# aynası `UiTheme.cs` (birim/krallık barları, çağ kadranı, grafik serileri, mini harita çerçevesi). Çift tanımlı USS kuralları temizlendi (DECISIONS #99).
- Ayarlar/Dünya Üretimi/Kanunlar pencerelerindeki stok gri Unity kontrolleri (kaydırıcı, açılır liste, onay kutusu, kaydırma çubuğu) temaya uyarlandı; düğmelere hover/basılı/devre dışı durumları ve 0,08 sn renk geçişleri eklendi.
- Ana menü yeniden tasarlandı: tam ekran hafif perde, ortalanmış hero kartı, büyük altın başlık + alt yazı (`ui.mainmenu.subtitle`), birincil (Devam Et / Yeni Dünya) ve ikincil eylem grupları, sürüm alt bilgisi. Pencere kapatma düğmesine tehlike vurgusu, bildirim kartlarına kategori şeridi, ölçüm çubuklarına yuvarlak iz eklendi.
- Davranış değişikliği yok: sınıf adları, pencere sürükleme/sabitleme/4 pencere sınırı, tıklama geçişi ve `pg-narrow` 44 px dokunma hedefleri korundu.

## [Unreleased] — Vali spec'i: ikili diplomasi, şehir politikası, çağrı sınırları

### Eklendi
- İkili diplomasi: barış ve ittifak artık **karşı tarafın kabulünü gerektirir**. Yeni komutlar (`ProposePeace`, `ProposeAlliance`, `AcceptProposal`, `RejectProposal`), bekleyen teklif deposu, AI krallıklar için deterministik kabul/red kuralları, 1 yıllık teklif ömrü ve panelde oyuncu onay/ret düğmeleri + bildirim. Savaş ilanı tek taraflı kalır.
- Şehir politikası: krallık genelinde gıda/konut/üretim/savunma/kültür ağırlıkları (`KingdomPolicy`); vali `SetPolicy` ile ya da oyuncu belirler, mevcut şehir planlayıcısı bina puanlarını bu ağırlıklarla çarpar (bina ve konum seçimi planlayıcıda kalır).
- Karar koordinatörü sınırları: krallık başına 90 saniye gerçek-zaman bekleme (aynı krallığın olayları birleşir), toplam 2 eşzamanlı istek, saatte 120 model çağrısı — tümü ayarlardan değiştirilebilir. Kuruluş, savaş, şehir yıkımı, kıtlık ve gelen teklifler karar isteği oluşturur.
- Bayat yanıt koruması: snapshot krallık Uid'si uyuşmayan yanıtlar atılır; oyuncunun devraldığı krallıklara vali dokunmaz; vali kontrolündeki krallıklarda otomatik savaş/barış/ittifak devreden çıkar, model 3 kez başarısız olursa oyun AI'ı devralır.
- İki fazlı beyin: Kral kararı → en fazla 3 danışman önerisi (paralel) → danışman görüşlerini gören Kral'ın son yapılandırılmış kararı; danışman görüşleri karar günlüğünde.
- Bağlantı testi: `/v1/chat/completions` ulaşılabilirliği ve `response_format: json_object` desteği Ayarlar'daki düğmeyle sınanır; desteklenmeyen uç noktalarda şemasız mod + her koşulda yerel JSON doğrulama. Krallık panelinde bağlantı durumu ve AI aç/kapat düğmesi.
- Kalıcılık: kayıt bölümü 16 (krallık politikaları + bekleyen teklifler) ve karar günlüğü v2 (danışman görüşleri + vali/oyuncu krallık listeleri); eski kayıtlar açılmaya devam eder, bekleyen ağ istekleri yüklemede başlatılmaz, API anahtarı kayda/günlüğe yazılmaz.
- Testler: sabit yanıtlarla geçerli karar / geçersiz hedef / bayat yanıt / oyuncu devralması / karşı taraf onayı / danışman sınırı; sahte yerel HTTP uç noktasıyla başarı, bozuk JSON, zaman aşımı ve erişim hatası senaryoları; politika normalizasyonu ve bölüm 15–16 gidiş-dönüşü.

## [Unreleased] — Krallık Valileri (LLM beyinleri)

### Eklendi
- Yeni `PG.Governor` katmanı: simülasyonda kurulan krallıkların stratejik yönetimi (savaş/barış/ittifak, başkent/veliaht, ordu hedefi) bir LLM "vali" beyinine devredilir. Sim determinizmi korunur: kararlar salt-okunur krallık özetinden (snapshot) üretilir, doğrulanır ve `MetaCommand` komut kuyruğuna girer (`Docs/10_Krallik_Valisi.md`, DECISIONS #97).
- Alt ajan sistemi: ana beyin bir kararda en fazla 3 uzman danışman (savaş/ekonomi/diplomasi) çağırabilir; öneriler tek karar paketinde birleştirilir, limit kodla zorlanır.
- OpenAI-uyumlu istemci ile yerel (Ollama / LM Studio) ve bulut (Z.ai GLM, OpenAI, Groq…) uç noktaları aynı yapılandırmayla çalışır; API anahtarı `PG_GOVERNOR_KEY` ortam değişkeninden veya `~/.pixelgenesis/governor_key` dosyasından okunur, hiçbir zaman ayar dosyasına yazılmaz.
- Karar günlüğü kayıt bölümü 15 ile saklanır ve yüklemede geri yüklenir; eski kayıtlar açılmaya devam eder.
- Dayanıklılık: API yok/hata/zaman aşımında krallıklar o yıl yerleşik deterministik sim-AI ile yönetilir; 3 art arda hata valinin kontrolünü bırakır. Ayarlarda aylık token bütçesi ve eşzamanlı çağrı limiti.
- UI: Ayarlar penceresine "Krallık Valileri" bölümü (aç/kapa, yerel/bulut profil, model, kapsam); Krallık penceresine son vali kararı ve gerekçe satırları. ~14 yeni TR/EN arayüz anahtarı (`ui.gov.*`).
- `GovernorTests` (EditMode): JSON ayrıştırma, doğrulayıcı reddi, ScriptedBrain uçtan uca savaş ilanı, alt ajan limiti, karar günlüğü gidiş-dönüşü — ağ erişimi yok.

## [Unreleased] — Unity 2D görsel geliştirme mimarisi ve görsel düzeltmeler

### Eklendi
- Mermiler (`Projectiles`) kendi mesh ve sıralama katmanına alındı: ok ve mermiler artık her zaman birimlerin üzerinde çizilir, birim Y-sıralamasıyla karışmaz (`UnitRenderer`).
- Fırça imleci `WorldUI` sıralama katmanına alındı; harita üstü dünya-uzayı arayüzü tutarlı hale geldi (`BrushCursor`).
- HUD üst çubuk canlı sayaçları: Toplam canlı nüfus (`👥 Nüfus`) ve yaşayan krallık sayısı (`👑 Krallık`) anlık durum paneline eklendi; aylık önbellekleme ile sıfır GC korundu.
- HUD fırça önayar butonları: Fırça paneline `[1]`, `[3]`, `[7]`, `[17]`, `[41]` hızlı çap seçim butonları eklendi; aktif boyut vurgusu ve `[` / `]` kısayol uyumluluğu sağlandı.
- Tüm `MeshRenderer` bileşenlerine açık `sortingLayerName` ataması yapıldı (`MapRenderer` ve sınırlar `Terrain`, `FeatureRenderer` `Features`, `BuildingRenderer` `Buildings`, `UnitRenderer` `Units`, `NatureRenderer` bulutları ve `PowerFxRenderer` `Weather`, gölgeler `Decals`, arayüz overlay'leri `WorldUI`).
- `NatureRenderer` bulut katmanı playbook Z-konvansiyonuna uygun olarak yerel Z=-1f derinliğine konumlandırıldı.
- HUD hover tooltip: İmleç altındaki karo koordinatı, zemin tipi, biyom, sıcaklık ve canlı birim bilgisi (isim, tür, HP) üst durum çubuğunda dinamik olarak görüntülenir; sıfır GC için önbellek koruması (`HudView.UpdateHover`).
- `PG.Render.SortingLayers` sınıfı ve `TagManager.asset` içinde standart 2D sıralama katmanları (`Water`, `Terrain`, `Decals`, `Features`, `Buildings`, `Units`, `Projectiles`, `Weather`, `DayNightLight`, `WorldUI`) yapılandırıldı.
- Tüm render sınıflarının (`NatureRenderer`, `PowerFxRenderer`, `BuildingRenderer`, `FeatureRenderer`, `UnitRenderer`, `OverlayRenderer`, `MapRenderer`) sıralama sabitleri merkezi `SortingLayers.cs` sınıfına bağlandı.
- `PixelArtPostprocessor` otomasyonu: `Art/` ve `Assets/StreamingAssets/Art/` altındaki tüm `.png` piksel sanatı varlıkları otomatik olarak `Point` filtreleme, `Mipmap` kapalı, `Uncompressed`, `Clamp`, `PPU = 1` ve `FullRect` sprite ayarlarına bağlandı; `PixelGenesis > Tools > Reimport All Pixel Art` toplu re-import menü komutu eklendi.
- `VisualDev2DTests` EditMode test paketi: kamera Z kilidi (-10f), near/far clip plane (0.3f / 100f), Y sıralaması ve sıralama katmanı tutarlılık testleri eklendi.
- Savan akasyası ve palmiye dahil içerikteki 24 ağaç türünün her birine ayrı saydam piksel görseli eklendi; `Preview Savanna Trees` ve `Preview All Trees` gerçek oyun kamerasıyla karşılaştırma sağlar.
- Meşe, çam ve kaya için özgün çevre görselleri; altı yapı stilini yan yana gösteren `Preview All Building Styles` galeri komutu.
- Köy merkezi, kuyu ve değirmen için özgün insan stili modelleri; ev/kulübenin uyumlu inşaat ve harabe görselleri; bina tabanlarında hafif temas gölgeleri. `PixelGenesis/Preview Village Graphics` ile yakın/uzak önizleme.
- İnsan evleri ve kulübeleri için tek özgün kiremit çatılı ev görseli; tamamlanmış yapılarda renkli sprite desteği. Unity menüsünde `PixelGenesis/Preview House Model` ile oyun içi önizleme.

### Düzeltildi
- `CameraController` içinde zoom ve pan sırasında kameranın Z koordinatının 0 veya pozitif değerlere kayarak haritanın near clip plane arkasında kalıp ekranın kararması/kaybolması riski (Z-clipping trap), Z derinliğinin `CameraSettings.CameraZ` (-10f) değerine kilitlenmesiyle tamamen önlendi.
- `CameraController`, `Renderer2D.asset` ve `GraphicsSettings.asset` üzerinde `TransparencySortMode.CustomAxis` (Y=1) top-down derinlik sıralaması sabitlendi; güneydeki birim ve yapıların kuzeydekilerin önünde çizilmesi garantiye alındı.
- Linear renk uzayında gri indeksli yapı/birim görsellerinin çok koyu veya yanlış renk çıkması düzeltildi. Yapı atlası görselleri artık 3×3 karoluk yapı hücrelerine, yeni çevre görselleri en çok 1 karo genişliğindeki çizim parçalarına bölünür (DECISIONS #91).
- Birim ve ağaç sprite'ları ham piksel boyutunu tile sayısı olarak çiziyordu (insan 4×7 tile, ağaçlar üst üste binen lekeler); `SpriteLibrary.PixelsPerTile = 5` ile ölçeklendi (DECISIONS #87).
- Binalar paletle renklenmiyordu (gri bloklar); artık stil malzemesi + krallık renkli çatı ile çizilir (DECISIONS #88).

### Değişti
- Placeholder binalar tile başına 5 px yoğunlukta, stil başına özgün mimariyle (kiremit çatı, ahşap çatkı, taş, çadır, tümsek, petek), pencere ışığı, baca, bayrak, kalkan/tabela ve inşaat iskelesi/harabe halleriyle yeniden üretildi; özel yapılar (kuyu, kule, değirmen, pazar, iskele, tersane, maden, mezarlık, heykel, ağıl…) ayrı çizilir. `preview_buildings.png` oyundaki paletle üretilir.
- `building_styles.json`: `wall`, `trim`, `roofMix` alanları.

## [0.11.0] — Faz 11–12: Ses, modlar, kayıt, performans, yayın hazırlığı

### Eklendi
- Ses: `sounds.json` (61 ses: güç, patlama, ölüm grupları, doğum, bina, savaş çanı/zafer borusu, arayüz, başarım, 6 ortam katmanı, 10 çağ × sakin/gergin müzik), `AudioManager` (32 kaynaklık havuz, 24 ses sınırı, pan, LOD'a göre ses, örnek/bekleme sınırları, kalabalık sesi, ortam karışımı, gerginlik katmanı ve çağ geçişi, dosya yoksa sentezlenen yer tutucu), ayarlarda 5 ses kanalı ve FPS sınırı.
- Modlar: `manifest.json`, bağımlılık ve `loadAfter` sırası, `+liste`/`-liste` yama sözdizimi, mod yerelleştirme dosyaları ve sesleri, kullanıcı mod klasörü, kayıtta mod listesi ve eksik mod uyarısı.
- Kayıt: mod bölümü, `Tests/SaveFixtures` sürüm kayıtları (her Diagnostics koşusunda yüklenir), `profile.bak` yedeği.
- Performans: `PerfBench` (5.000 birim bütçe koşusu), halka tabanlı ve hücre türüne göre budanan komşu araması, `Hostile` kısa yolu; tick başına sıfır heap ayırma doğrulandı.
- Arayüz: pencere/hedef/anahtar güçleri güç çubuğunda (incele, izle, possession, meta kontrol, editörler, istatistik, grafik, tarihçe, kanunlar, çağ saati, kayıt, ayarlar, rehber), kilitli güçlerde açan başarım ipucu, keşif gözü (`pw.eye`), "Yenilikler" penceresi, mobilde arka planda duraklatma.
- Başarım: `ach.gene_splicer` denetçisi.
- Diagnostics: TR/EN yerelleştirme eksiksizliği denetimi.

## [Unreleased] — Faz 10: Arayüz ve oyuncu araçları

### Eklendi
- Pencere sistemi: `WindowBase`/`WindowManager` (sürüklenir, iğnelenir, sekmeli, 4 Hz, en fazla 4 pencere, Esc en üsttekini kapatır), `UiContext`, `Names`.
- Pencereler: birim (7 sekme), jenerik meta (krallık, şehir, ittifak, kültür, dil, din, klan, alt tür, savaş; üyeler, şehirler, ilişkiler, savaşlar, tarihçe, kitaplar), trait/gen/eşya editörleri, meta kontrol, kanunlar, çağ saati (Painter2D kadran), istatistikler, grafikler (Painter2D, 10/50/100/1000 yıl, 8 seri), tarihçe (arama), başarımlar, harita katmanları, ayarlar (dil, UI ölçeği, kenar kaydırma, bildirim kategorileri, rehberi yeniden başlat).
- Komutlar: `EditCommand`, `LawCommand`, `MetaCommand`, `EraCommand`, `ControlCommand`; `EditCommandSystem` (ilahi işaretler, possession kontrolü).
- HUD: kategori bazlı, gruplanan, tıklanabilir bildirim kartları; araç çubuğu; favoriler ve güç araması; incele modu.
- Harita: `OverlayRenderer` (12 mod, sınır çizgileri, dönüşümlü çoklu mod, LOD 3'te krallık), şehir/krallık/kral isim plakaları.
- Profil: `profile.json` (keşifler, başarımlar, sayaçlar, favoriler, ayarlar); keşif bildirimi; `AchievementTracker` ve güç kilitleri.
- Possession: WASD, saldırı, atılma, büyü, Esc bırakma; ölünce birim penceresi.
- Ana menü (devam et, yeni dünya, yükle, ayarlar, başarımlar, çıkış) ve 8 adımlı rehber.
- İçerik: `achievements.json` içerik kaydına alındı; ~120 yeni arayüz anahtarı (TR/EN).
- Editör: PlaySmoke tüm yeni pencereleri, overlay modlarını ve possession'ı açıp kapatıyor; BioSoak yakıcı kültür senaryosu düzeltildi.

## [Unreleased] — Faz 9: Karakter derinliği

### Eklendi
- Nöronlar: `NeuronId`/`NeuronKeys` (13 çekirdek + 24 anahtarlı nöron), birim nöron maskesi, alt tür beyin paneli maskesi (`DisabledNeurons`, `SetNeuron`), düşünme döngüsünde çekirdek kapılar ve keşif/gezinme/eve dönüş/ışınlanma/sürü avı/sürü/bölge/hücum/içgüdü/biriktirme davranışları, aylık `NeuronLifeSystem` (yuva, göç, mevsimlik göç, hediye, gömme, dans, yıldız, hikâye, güneşe selam, ateş, evcilleştirme, parlak toplama), yağmacılar, kitap kurtları.
- Trait'ler: kral katili, kahraman, yaralı, topal/tek göz, sağ kalan; keşfedilen trait'ler (kayıtlı) ve olayları.
- Eşyalar: demirci kalite puanı, benzersiz/efsanevi eşya isimleri ve tarihçe kaydı, silah öldürme sayısı, yerdeki eşyaların alınması.
- Dövüş: yatay itme, hücumda çift itme.
- Denge: avcılar güçlü hayvanlara saldırmaz; yabani yırtıcılar şehirlileri yalnız çok açken avlar; et yiyenler av aramak için dolaşır.

## [Unreleased] — Faz 8: Genler, evrim, monolit, dönüşümler, zombiler

### Eklendi
- Sim: `GeneGenome` ve `Genetics` (rastgele genom, gen/sinerji statları, mutasyon işlemleri, fenotip seçimi), `EvolutionSystem` (mutasyon ve genom bölünmesi, ada izolasyonu, biyom uyumu, monolit nabzı, `law.uplift_all`/`law.pure_lines`), `MetamorphosisRules` ve `MetamorphosisSystem` (yaş, çağ, birleşme, hastalık sonu varyantları), `ZombieSystem` (ceset kalkışı, yakma, çürüme, `era.scorch`, sürüler ve lordlar, şişkin patlaması), `gene.chimera`.
- Birim: `FormerSpecies`, `TransformTick`; dönüşümde meta kaybı ve `tr.hollow`; zombi zayıflıkları; ölümsüzler yemez/uyumaz; hava yolu ve sivrisinek bulaşması, şifacı tedavisi, iyileştirme büyüsünün hastalığı geçirmesi; et yiyenlerin dolaşarak av araması.
- İçerik: `meta.fresh_to_bloater`.
- Kayıt: alt tür genomu/fenotipi (gen ve fenotip eşlemesi), ceset kalkış bayrağı, eski tür eşlemesi.
- Render: fenotip palet satırları, zombi canavarın eski türün sprite'ıyla çizilmesi.
- Editör: `BioSoak` (Bölüm 4.10 #4-#9).

## [Unreleased] — Faz 7: Kültür, dil, din, klan, plot, tarihçe

### Eklendi
- Sim: `CultureSystem` (şehir çoğunlukları, üyelik atama ve kayma, ilk tapınakla din kuruluşu, krallık kültürü/dini/dili, klanlar, nüfus sayımı, okuma, asimilasyon, vaaz, yazı, lehçe bölünmesi, kitap yazma ve çürüme), `PlotSystem` (aday roller, koşul kaydı, katılımcı toplama, ilerleme, 16 plot sonucu), `HistoryRecorder` ve `HistoryKind` şablonları, veraset kuralları (kültür `Leader`), kültür/din/klan fikir ve sadakat terimleri, din büyüleri, kitap bonusları.
- Birim: kültür, dil, din, klan ve okunan kitaplar; doğumda ebeveynden, yaratılışta yakın akrabadan miras; karakter trait'leri ve doğuştan sivil statlar.
- Şehir: dil/din/başkent/savaş zaferi bina gereksinimleri, tapınak/kütüphane/şifahane planlaması, büyük şehir kolonileri.
- İçerik: kültür, dil, din, klan trait kayıtları, plotlar, kitap türleri, biyom havuzları; `history.*` ve din toast anahtarları.
- Kayıt: bölüm 12'ye kültür, dil, din, klan, plot, kitap ve tarihçe; trait/plot/kitap eşlemeleri.
- UI: din toast'u, hata ayıklama katmanında kültür/din/klan/hastalık.
- Editör: `PG_CIV_SCENARIO=multi` (insan, ork, elf, cüce; orta harita) ve 6.15 #1 kabulü, tür başına doğum/ölüm günlüğü.

## [Unreleased] — Faz 6: Tanrı güçleri ve afetler

### Eklendi
- Sim: `PowerWorld` (düşüşler, dünya nesneleri, geri çekilen su, Hayat Oyunu/Langton ızgarası, mıknatıs, zone sıcaklık kaymaları, kıtlık), `Explosions` (küçük/orta/büyük/boşluk, tile halkaları, bina ve birim hasarı, fırlatma, radyasyon), fırlatılan birimlerin yatay hızı, hastalık çekirdeği (`DiseaseSystem`, bulaşma, taşıyıcılar, bulut etkileri, dönüşüm).
- Powers: işleyici eşlemesi (`PowerHandlers`), düşüş etkileri (`DropEffects`), `PowerWorldSystem` (hortum, yanardağ, gayzer, kara delik, tsunami cephesi, Langton karıncası, yem, iblis yarığı, Hayat Oyunu, mıknatıs), deprem/sel/çekirge/sıcaklık işleyicileri, hedefli güçler (statü, fikir, sadakat, başkent, taç, taşıma, göçmen, festival, enfeksiyon, dürtme), fırça güçleri (bereket yağmuru, ısı/soğuk, ölüleri kaldır, arınma, kutsal ışık, ısı ışını, kuraklık, zone ekle/çıkar), köy kur, kilitler, tüm afetlerin komut kuyruğundan yürütülmesi, `PowerSystems.Install`.
- Kayıt: bölüm 13 (güç durumu, güç eşlemesi); birim hastalığı ve hastalık eşlemesi.
- Render: `PowerFxRenderer` (düşüş + gölge, dünya nesneleri, patlama parlaması, kamera sarsıntısı, otomat katmanı).
- UI: iki tıklamalı güçler, kamera sarsıntısı.
- Editör: `[Powers]` duman testi (221 UI dışı güç), atom bombası ve karşı madde kabulü, kayıt gidiş-dönüşünde uçuştaki güç durumu.

## [Unreleased] — Faz 5: Krallıklar, savaş, ticaret, gemiler

### Eklendi
- Meta: `MetaWorld` (krallıklar, savaşlar, ittifaklar, ordular, fikir tablosu, `NamePool` Roma rakamlı tekrar), sancak, isim üretici (`name_sets.json`, sembol dili), unvanlar.
- `KingdomSystem`: şehir başına krallık kuruluşu, sömürgelerin kurucu krallığa bağlanması, şehir/birim adları, aylık sayım ve güç, kral/veliaht/veraset, şehir sadakati ve isyan (`law.kingdom_rebellions`, isyan savaşı), fikir yakınsaması (tür, sınır, ittifak, geçmiş savaş, kral trait'leri, krallık trait'i), ittifak kurma/ayrılma, göç (5.11), ticaret kervanları (5.9).
- `WarSystem`: yıllık savaş ilanı (`law.wars`, fikir < −20, güç oranı, uzaklık, sigmoid şans), müttefiklerin katılması, barış şansı ve 10 yıllık yasak, ordu kurma/toplanma/yürüyüş/kuşatma/dönüş, bina kuşatması (hasar × 0,5), şehir fethi ve boş şehrin yıkımı, savaşta askere alma.
- `BoatSystem`: sömürge nakliye gemisi (su yol bulma, yolcu taşıma, başka adaya çıkarma).
- Kayıt: bölüm 12 (meta; krallık trait'i ve savaş türü eşlemesi), bölüm 11'e gemiler.
- UI: krallık, savaş, barış, fetih, isyan, yeni hükümdar, ittifak bildirimleri; F3 paneline şehir adı/sadakat, krallık, birim adı/ordu; şehir sınırı krallık rengiyle.
- Editör: `PG_CIV_SCENARIO=war` (insan + ork komşu), meta raporu, determinizm hash'ine krallık/fikir/savaş.

### Değişti
- Şehirli etçiller ekinle beslenebilir, şehirli birimler yabani av peşine düşmez; göçmenler hedefi `Dest` alanında taşır.

## [Unreleased] — Faz 4: Medeniyet temeli

### Eklendi
- Faz 3 tamamlama: `MetaStore<T>`, alt tür deposu (Bölüm 4.1 türetilmiş trait setleri, yetenek bayrakları, `law.mutant_box`), birim trait kalıtımı (4.3), aylık alt tür sayımı, yumurta/koza birimleri acıkmaz, yürüyenlerin uzak yiyecek araması kısaldı.
- Content: `ResourceDef`, `BuildingDef` (hücre boyutu, maliyet, `requires` ayrıştırıcı), `BuildingStyleDef`, `JobDef`, `EquipmentTypeDef`, `MaterialDef`, `ItemQualityDef`, `HappinessEventDef`; biyom `unitTraitPool`.
- World: bina ayak izi (`SetBuilding`: Reserved + yürünmez, kapı açık), yol bayrağı, zone sahipliği; yüklemede ayak izleri kapalı kalır.
- Sim: `CivWorld` (şehirler, binalar, envanter, eşyalar, yol A*), `SettlementSystem` (kurma, katılma, yıllık zone büyümesi, terk → harabe, göçmen grubu), `CityPlannerSystem` (ihtiyaç puanları, `requires` kontrolü, yer bulma, meydan, yerinde salon yükseltmesi), `BuildingSystem` (yangın hasarı, kaybolan zemin, harabe süresi), `CityEconomySystem` (konut atama, beslenme, bozulma, kıtlık, vergi, ekin büyümesi, meslek kotası ve atama, ekipman dağıtımı, şehir mutluluğu), meslek görev zincirleri (inşaatçı, çiftçi, oduncu, toplayıcı, ocakçı/madenci, balıkçı, avcı, çoban, fırıncı, demirci, muhafız/asker devriyesi), eşya üretimi ve kalite zarı, mutluluk olay halkası, ölümde düşen eşyalar, yanan birimlerin suya koşması.
- Render: `BuildingRenderer` (stil/durum sprite'ları, yanma tonu) ve şehir sınırı katmanı.
- UI: şehir kuruldu/harabe/kıtlık bildirimleri; F3 panelinde şehir, bina ve birimin şehir/meslek bilgisi.
- Persistence: bölüm 11 (şehirler, binalar, envanter, eşyalar; bina/stil/kaynak/meslek/ekipman/malzeme/kalite/mutluluk eşlemesi).
- Editör: `PixelGenesis/Civ Soak` (Bölüm 5.12 #1 kabulü), snapshot'a köy görünümü, kayıt gidiş-dönüşünde şehir ve ayrışma raporu; `Tools/compile_check.py`.

## [Unreleased] — Faz 3: Birim çekirdeği

### Eklendi
- Content: `SpeciesDef`, `UnitTraitDef`, `StatusEffectDef`, `SpellDef`; `StatId` eşlemesi (bilinmeyen anahtarlar davranış anahtarı), `UnitFlags`, diyet/habitat/üreme türleri, statü efekt ayrıştırıcı, biyom hayvan listeleri.
- Sim: SoA `UnitStore` (yuva yeniden kullanımı + nesil, ertelenmiş ölüm, cesetler), stat formülü (trait, statü, yaş evresi, seviye), zone hücreli deterministik uzamsal indeks, `PathService` (ada kontrolü → region A* koridoru → tile A*, bütçe, yol havuzu), nöron → görev → eylem AI (kaç, saldır, avlan, beslen/otla, uyu, çiftleş, takip/sürü, dinlen, karaya çık, büyü), hareket (tile maliyeti, yüzme, itme/düşme), yakın dövüş + mermiler + XP/seviye/veteran, statü sistemi, açlık/enerji/stamina, yaşlanma, canlı/yumurta/bölünme üremesi, nüfus sınırı ve yerel kalabalık, sıcaklık/lav/yangın/boğulma hasarı, 20+ büyü, hayvan doğuşu (başlangıç + yıllık).
- Powers: yaratık/tür doğurma güçleri; afetlere nüfus bilgisi.
- Render: palet shader'lı birim sprite'ları (4×6 sayfa, animasyon, yön, yaş ölçeği, statü tonu), LOD noktaları, mermiler.
- Persistence: bölüm 10 (birimler, yollar, mermiler; tür/trait/statü eşlemesi).
- UI: F3 panelinde doğa ve birim bilgisi, imleç altındaki birim.
- Editör: soak'a ekosistem ve 5.000 birim performans senaryoları, `PixelGenesis/Feed Debug`.

## [Unreleased] — Faz 2: Doğa, iklim ve zaman

### Eklendi
- Content: `CloudDef`, `EraDef`, `DisasterDef`, `WorldLawDef` + kayıtlar (clouds, eras, disasters, world_laws); tile doğa kuralları (donma/erime, yanık, iyileşme, lav soğuma/akış/söndürme, balçık), feature `aquatic`/`burnsInto`, biyom `effectCode`/`tempOffset`/`snowCover`/`waterOnly`/`fireproof`; `BiomeEffect` ve `CloudEffect` kod tabloları.
- World: `FeatureState` (evre + kaynak), zone ağaç/bitki sayaçları ve temel sıcaklık, `DirtyMask.Features`, `SetFire`, aktif tile kuyruğu, su/kara uyumlu biyom ve feature kuralları.
- Sim: `NatureState` (değiştiriciler, rüzgâr, bulutlar, tohumlar, yanan/lav/balçık listeleri, çağ saati, kanunlar), biyom yayılımı + tohum, feature büyümesi, yangın (rüzgârlı yayılım), lav akışı/soğuma, balçık, yavaş dönüşümler, aylık Burst zone iklimi (mevsim, donma, kar, çöl buharlaşması, volkan baloncuğu, kor kıvılcımı), rüzgâr, bulut doğuşu/hareketi/yağışı, küresel yağmur, çağ sistemi (8 slotlu saat, ağırlıklı seçim, tint geçişi), yıllık afet kararı + `DisasterRequestEvent`.
- WorldGen: başlangıç bitki örtüsü (orman yoğunluğu), zone temel sıcaklığı, maden damarlarına kaynak.
- Powers: ağaç/çalı/çiçek/maden/buğday ekme, biyom tohumları, ateş, kar, lav, balçık, bulut güçleri, söndür/dondur/lav soğut fırçaları, `DisasterExecutor`.
- Render: `SpriteLibrary` (id ile PNG, atlas), chunk başına `FeatureRenderer` (evre ölçeği, LOD 3'te gizli), bulut + gölge katmanları, global çağ rengi, `PG_Sprite` shader.
- UI: üst çubukta çağ ve kalan süre; çağ değişimi ve afet bildirimleri.
- Persistence: isteğe bağlı bölümler 6–9 (FeatureState, zone iklimi, doğa, kanunlar); yükleme sonrası birebir devam.
- Editör: `PixelGenesis/Nature Soak` (uzun koşu, buz çağı, rüzgârlı yangın), snapshot'a feature/bulut ve yakın görünüm; DataSync `Art/` PNG'lerini de aynalar.
- Araç: `Tools/placeholder_art.py` feature'lara türe uygun geçici renkler verir.

## [Unreleased] — Faz 1: Dünya çekirdeği

### Eklendi
- Core: `SimConst`, `GameClock` (hız kademeleri, duraklatma, tek adım), PCG32 `SimRandom` + akış başına `SimRandomProvider`, `EntityId`/`IdPool`, tip başına kuyruklu `EventBus`.
- Content: `ContentDB.LoadAll` (StreamingAssets/Data + Mods yaması), `Registry<T>`, `ContentValidator`; tiles, biomes, features, powers, worldgen_templates, biome_table, ui_strings.
- World: SoA `WorldMap` (tek yazma kapısı, dirty maskeleri, `TileEditBatch`), `RegionGraph` (chunk içi region'lar, chunk arası kenarlar, union-find adalar, tick başına 4 chunk bütçe).
- WorldGen: Burst job'larıyla 9 adım (yükseklik + maske + domain warp, yüzdelik deniz seviyesi, seviyeler, hücresel temizlik, kıyı, nem/sıcaklık, biyom tablosu + özel yamalar, varyant, maden damarları), 8 şablon, PNG'den içe aktarma.
- Sim: `SimulationPipeline` (9 faz, ProfilerMarker), `SimWorld`, olay dağıtımı ve region yeniden hesap sistemleri.
- Powers: fırça (daire/kare, 9 boyut), stamp/stroke (Bresenham), `powers.json` op'larından fırça işlemleri, `PowerCommand` kuyruğu.
- Render: chunk başına 64×64 katmanlı `Texture2DArray`, tek mesh/tek draw call, Burst renk job'ı (kabartma gölgesi, kar/yangın/yol), su/lav animasyonlu URP 2D shader, F4 region/ada görünümü.
- UI: UI Toolkit HUD (tarih, hız, süper hız, tek adım, güç çubuğu, fırça paneli, bildirimler), yeni dünya penceresi (önizlemeli), kayıt penceresi, F3 debug overlay; kamera (imlece zoom, kaydırma, sürükleme, ataletli, piksel hizalama, LOD).
- Persistence: kayıt v1 (başlık + küçük resim + sıkıştırılmış bölümler, chunk RLE, ContentMap eşleme), 6 slot + döngüsel 3 otomatik kayıt (10 yılda bir ve çıkışta).
- Boot: `GameBootstrap` sahnesi, `SimulationRunner`, `--determinism-check`, `--headless --years`.
- Editör: `PixelGenesis/Sync Data`, `PixelGenesis/Run Diagnostics`.
## [Unreleased] — Ekosistem: yerel bitki biyokütlesi (C2 başlangıcı)

### Değişti
- 8×8 bölgelerde yenebilir feature verimi izleniyor ve vahşi otçulların üreme oranına yerel gıda sinyali ekleniyor. Sayaç kayıt açılışında harita feature'larından yeniden kurulur; şehirli türlerin mevcut gıda/konut etkisi korunur.
- Biyokütle kalan feature kaynağına göre azalır; otçullar tükenen kaynağı hedeflemez ve doğa örneklemesinde yenebilir kaynak yenilenir.
- Bitki olmayan biyom karoları da kayıtla korunan sınırlı otlak stoğu sağlar; yenince azalır, doğa örneklemesiyle geri kazanılır.

## [Unreleased] — Ekosistem C3 başlangıcı: avcı ve av baskısı

### Değişti
- Yerel avcı doğurganlığı C1'deki açık av türleriyle sayılan yakın av varlığına bağlandı. Aç birim daha geniş görüşle av arar ve hedef prowl mesafesi doygunluk azaldıkça 8–28 tile arasında açılır; mevcut açlık hasarı ve üç aylık ölüm eşiği korunur.

### Doğrulama durumu
- Avcı–av salınımı, EditMode regresyonları ve uzun soak çalıştırılmadı; C3 ve C fazı kabul edilmedi.
- Kod ve karar günlüğü eklendi; test ve 200 yıllık ekosistem soaku henüz çalıştırılmadı. Formül, çok tohumlu sonuçlara göre kalibre edilmeli.

## [Unreleased] — Ekosistem C4 başlangıcı: yerel göç

### Değişti
- Yerel gıda kıtlığı veya yoğunluk baskısındaki vahşi yetişkinler, kaynak/hemcins oranı daha iyi olan yakın bölgeye mevcut yol bulma sistemiyle göç edebilir. Avcı ve hepçiller yerel av varlığını da hedef değerlendirmesine katar.
- Göç hedefi türün hareket kabiliyetine uygun, yürünebilir bir kare olmalı; yeni kayıt alanı eklenmedi.

### Doğrulama durumu
- Göç testleri, deterministik soak ve 200 yıllık wild denemesi kullanıcı testleri en sona bırakıldığı için çalıştırılmadı. C4/C fazı kabul edilmedi.

## [Unreleased] — Ekosistem C5 başlangıcı: tür tehlikesi ve tükenme

### Değişti
- Daha önce var olmuş tür iki yıl boyunca nüfusu 2 veya altındaysa tarihçede tehlike altında, sıfıra düşerse tükenmiş olarak kaydedilir; yeni olaylar Türkçe ve İngilizce metinle tarihçe penceresinde görünür.
- Doğal yıllık spawn soyu tükenen türleri geri eklemez. Oyuncunun güçlerle manuel spawn etmesi serbest kalır ve ek kayıt biçimi gerekmez.
- Tarihçe sınırına ulaşıldığında tür tehlikesi ve tükenme kayıtları diğer eski olaylarla birlikte budanmaz.

### Doğrulama durumu
- Test, save/load ve popülasyon soak'ları kullanıcı testleri son aşamasına bırakıldığı için çalıştırılmadı. C5/C fazı kabul edilmedi.

## [Unreleased] — Ekosistem C6 başlangıcı: yoğunluk ve hastalık

### Değişti
- Temas hastalığının bulaşma olasılığı, enfekte birimin 4 tile çevresindeki canlı birim sayısına göre artıyor. Şehir, bataklık ve hastalık direnci çarpanları aynı şekilde uygulanıyor.

### Doğrulama durumu
- Salgın senaryoları, density A/B ve performans testleri kullanıcı testleri en sona bırakıldığı için çalıştırılmadı. C6/C fazı kabul edilmedi.

## [Unreleased] — Ekosistem C7 başlangıcı: leş ve besin döngüsü

### Değişti
- İki yıl sonra kalan ceset, en yakın uygun bitki/otlak kaynağına küçük bir kaynak artışı bırakıyor; mevcut biyokütle ve kayıt sayaçları güncelleniyor. Yenen ceset bu katkıyı bırakmıyor.

### Doğrulama durumu
- Ceset tüketimi/çürümesi ve besin katkısı testleri kullanıcı testleri en sona bırakıldığı için çalıştırılmadı. C7/C fazı kabul edilmedi.

## [Unreleased] — Ekosistem C8 başlangıcı: yerel kaynak baskısı

### Değişti
- Balıkçı, 12 tile yakındaki balık taşıyan su feature'ından kaynak tüketerek balık üretir; kaynak kalmadığında sınırsız stok oluşmaz. Avcı öldürmeleri ve oduncu tüketimi mevcut C1–C3 av/biomass geri beslemelerine bağlanır.

### Doğrulama durumu
- Aşırı av/odun/balık ve doğa yenilenmesi dengesi, çok tohumlu soaku ile ölçülmedi. C8/C fazı kabul edilmedi.

## [Unreleased] — İklim D1 başlangıcı: mevsim etkileri

### Değişti
- `Data/season_rules.json` dört mevsim için sıcaklık, bitki/tarım, doğurganlık, açlık, hareket ve gıda bozulması çarpanlarını düzenliyor. `law.seasons` mevsim şiddetini ayarlıyor.
- Mevsim durumu saatin ayından türetiliyor ve mevcut sıcaklık döngüsüne doğa büyümesi, şehir mahsulü, vahşi birim ihtiyaçları, yürüyüş ve depo bozulmasını bağlıyor.

### Doğrulama durumu
- Sezon, tarım, yükleme ve denge testleri kullanıcı testleri en sona bırakıldığı için çalıştırılmadı. D1/D fazı kabul edilmedi.

## [Unreleased] — İklim D2 başlangıcı: bölgesel nem ve kuraklık

### Değişti
- 8×8 bölgeler yağış, sıcaklık kaynaklı buharlaşma ve yakındaki su tamponuyla güncellenen kalıcı nem taşır. Üç düşük nemli ay kuraklık bayrağını açar; yeterli nem geri gelince kuraklık çözülür.
- Nem ve kuraklık durumu iklim kayıt bölümüne eklenir; önceki kayıt düzeni geriye uyumlu okunur.

### Doğrulama durumu
- İklim dengesi, kayıt turu ve simülasyon testleri kullanıcı testleri son aşamaya bırakıldığı için çalıştırılmadı. D2/D fazı kabul edilmedi.

## [Unreleased] — İklim D3 başlangıcı: iklime bağlı tarım

### Değişti
- Şehir tarlalarının aylık buğday olgunlaşması ile hasat miktarı yerel zon sıcaklığı ve nemine bağlandı; soğuk/kurak dönemler verimi yavaşlatıyor ve düşürüyor.
- Demir Saban, Sulama Kanalları ve Su Değirmeni tarım teknolojileri eklendi. Sulama nem kısıtını hafifletiyor.

### Doğrulama durumu
- Hasat, teknoloji farkı ve iklim senaryoları kullanıcı testleri son aşamaya bırakıldığı için çalıştırılmadı. D3/D fazı kabul edilmedi.

## [Unreleased] — İklim D4 başlangıcı: kıtlıkta neden ipucu

### Değişti
- Kıtlık olayı kuraklık/soğuk sinyalini taşır. Oyun bildirimi bunu kesin neden olarak sunmadan olası iklim etkeni diye açıklar.
- Mevcut kıtlık sonrası mutluluk/doğum, yıllık göç, bağlılık ve isyan bağlantıları korundu.

### Doğrulama durumu
- İklim kaynaklı kıtlık, göç, isyan ve kullanıcı bildirimi senaryoları kullanıcı testleri son aşamaya bırakıldığı için çalıştırılmadı. D4/D fazı kabul edilmedi.

## [Unreleased] — İklim D5 başlangıcı: iklim göstergeleri

### Değişti
- Harita katmanlarına 8×8 zon nemi/kuraklığı, mevsim ve iklim ile icatlara göre tahmini mahsul verimi eklendi.
- Katman penceresi renk yönlerini ve değişen mevcut mevsimi açıklar; tüm katmanlar simülasyonu salt-okunur biçimde gösterir.

### Doğrulama durumu
- Overlay ve mobil okunaklılık kontrolü Unity'de yapılmadı; D5/D fazı kullanıcı kabulü bekliyor.
## G1 tarih kaydı — izlenen kişi ve hükümdar ölümleri

- Biyografisi izlenen kişilerin ölümleri düşük önemle, hükümdar ölümleri yüksek önemle tarihçeye yazılıyor; sıradan ve izlenmeyen ölümler kroniği doldurmuyor.
- Ölüm anındaki krallık/hükümdar durumu kaydediliyor ve aynı krallığın yeni hükümdarlığı yakın tarihli ölüm olayına bağlanıyor. Regresyon testleri eklendi; Unity testleri kullanıcı isteğiyle henüz çalıştırılmadı.\n
## [Unreleased] — Faz kabul ölçümleri QA listesi

- Yayın QA üreteci artık A–J fazlarının ilgili ölçüm kapılarını, sabit tohum/süre bilgilerini ve beklenen metrik eşiklerini tek listede veriyor. Bu satırlar çalıştırma sonucu değildir; durumları final test kanıtıyla doldurulmalı.

## [Unreleased] — Faz F1 warp içerik sınırları

- WorldGen şablonlarının warp genliği ve frekansı runtime yüklemede ve içerik doğrulamasında finite aralıklarla kontrol ediliyor; geçersiz mod override'ları içerik yüklemesini anlaşılır hata ile durduruyor. Unity regresyon testi yazıldı, çalıştırılmadı.
## [Unreleased] — Faz G4 uygarlık ve yabanıl nüfus grafiği

- Dünya grafikleri medeniyet ve yabanıl nüfusunu ayrı çiziyor; yüzyıl/çağ özetleri bu iki değeri saklayıp save yüklemesinden sonra grafiğe geri veriyor. HST1 v4, eski v1–v3 özetleri uyumlu okuyor ve geçmiş ayrımı bilinmeyen yılları veri yok olarak bırakıyor. Round-trip ve migration regresyon kaynakları eklendi; Unity testleri çalıştırılmadı.

## [Unreleased] — Faz B0 salon yükseltme tanısı

- Soak şehir raporları bir sonraki salonun kimliğini, karşılanabilirliğini, yer bulunmasını ve eksik kaynak miktarlarını ayrı ayrı kaydeder. JSON round-trip regresyon kaynağı eklendi; offline derleme geçti. Unity Test Runner ve soak kullanıcı final testine bırakıldı.

## [Unreleased] — Faz A3 soak karşılaştırma tanıları

- Tohum bazlı soak karşılaştırması son yıl medeniyet/yabanıl nüfusunu, krallık/şehir durumunu, medeniyetsiz geçen yılları ve yeni raporlardaki salon yükseltme tıkanmalarını yan yana gösterir. Eski raporlar için bulunmayan alanlar `n/a` olarak kalır. Kaynak regresyonları gate'e bağlandı; kullanıcı final testleri çalıştırılmadı.

## [Unreleased] — Faz E4 pazar fiyat sapması ölçümü

- Yıllık soak raporu tamamlanmış pazarlar için kaynak fiyatlarının denge değerinden ortalama sapmasını ve örnek sayısını saklar. Karşılaştırıcı son yıl verisini tohum bazında gösterir; örneksiz raporlar `n/a` kalır. Fiyat yakınsaması ve ticaret kabulü gerçek soak sonucunu bekliyor.

## [Unreleased] — Faz F2 nehir geçitleri regresyonu

- Dünya üretimi regresyonu artık sığ yürünebilir geçitlerle geniş, köprü gerektiren nehir yataklarını ayırıyor ve sabit tohumda harita karmasını karşılaştırıyor. Unity testleri ve görsel/çok tohum kabulü kullanıcı final testine bırakıldı.
