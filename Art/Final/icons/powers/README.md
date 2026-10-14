# Dünya araç simgeleri

`Tools/build_power_icons.py`, `Data/powers.json` içindeki ilk 20 dünya aracının sırasını doğrular, bu klasöre düzenlenebilir SVG kaynakları ve 64×64 şeffaf PNG'ler üretir. PNG'leri aynı zamanda `Assets/StreamingAssets/Art/Final/icons/powers` altına kopyalar. `PgIcons` final görseli bulduğunda onu, diğer araçlarda eski placeholder simgeyi kullanır.

İkonlarda koyu yuvarlak zemin ve arazi/su/bitki/araç için ayrı silüetler bulunur. Oyun içi düğmede 24 tasarım pikseli, dokunmatik düğmede 28 tasarım pikseli gösterilir. Tasarım inceleme sayfası: `Docs/previews/power-icons.html`.
