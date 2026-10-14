# PixelGenesis menü simgeleri

`Tools/build_menu_icons.py` bu klasöre 46 düzenlenebilir SVG ve 64×64 saydam PNG üretir; PNG kopyaları `Assets/StreamingAssets/Art/Final/icons/menu` yoluna yerleşir. `PgIcons.Menu` final görselleri tek kez okuyup önbelleğe alır; eksik varlıkta düğme metni görünmeye devam eder.

Kapsam: ana menü, hızlı araç şeridi, dünya/medeniyet/yaratık/doğa/yıkım/diğer sekmeleri, üst kayıt düğmeleri, kayıt penceresi ve ortak pencere işlemleri. Simge ile metin aynı düğmenin içindedir; dokunmatik alan boyutu `PG_Hud.uss` kurallarından gelir. Küçük pencere başlığına sabitle/kapat simgeleri eklenmez. Tasarım kontrolü: `Docs/previews/menu-icons.html`.
