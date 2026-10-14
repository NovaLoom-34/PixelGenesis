import json, os
from lib import save, mods

# ---------------- INVENTIONS & TECHNOLOGIES ----------------
# Medeniyetlerin bilginleri, kütüphaneleri ve nüfusuyla zamanla icat ettiği buluşlar.
# Her buluş yeni aletler (kazma, tırpan, tüfek), yeni taşıtlar (araba, gemi türleri)
# veya üretim/askeriye bonusları açar.
INV = [
    ("wheel", "Tekerlek", "transport", 100, [], "speed:+10%", "Kara taşımacılığının temeli; yük arabalarının ve tekerlekli ulaşımın önünü açar."),
    ("plow", "Demir Saban", "production", 120, [], "farmYield:+40%", "Tarlaların daha derin sürülmesini sağlar; çiftlik hasadı artar ve tırpan kullanımı başlar."),
    ("sails", "Yelken ve Denizcilik", "transport", 150, [], "boatSpeed:+25%", "Rüzgâr gücüyle deniz aşırı seyir; balıkçı ve ticaret filoları denizlere açılır."),
    ("cart", "Yük Arabası", "transport", 200, ["inv.wheel"], "tradeProfit:+50%", "Şehir içi ve şehirler arası kaynak taşımayı hızlandırır; yük arabaları karada dolaşmaya başlar."),
    ("advanced_mining", "Derin Madencilik", "production", 220, [], "mineYield:+50%", "Kazma ve yeraltı tahkimatı; maden ve taş ocaklarının verimi artar."),
    ("shipbuilding", "Gemi Yapımı ve Donanma", "transport", 300, ["inv.sails"], "boatHp:+50%|colonizeChance:+40%", "Büyük kalyon ve savaş gemisi inşası; denizlerde donanma hakimiyeti ve sömürgecilik."),
    ("catapult", "Kuşatma Mancınığı", "military", 350, ["inv.wheel"], "wallDmg:+100%", "Ağır kuşatma makineleri; ordular savaşta düşman binalarını ve surlarını hızla yerle bir eder."),
    ("printing_press", "Matbaa", "science", 400, [], "readChance:+100%", "Kitap ve bilgiyi hızla çoğaltır; kütüphaneler araştırma ve kültür yayılımını ikiye katlar."),
    ("gunpowder", "Barut", "military", 500, [], "morale:+20", "Patlayıcı karışım; demirciler barutlu tüfekler üretir ve ordular ateşli silahlara kavuşur."),
    ("steam_engine", "Buhar Makinesi", "production", 700, ["inv.advanced_mining"], "craftQuality:+1", "Mekanik güç ve buhar enerjisi; atölyeler ve madenler çağ atlar."),
    ("automobile", "İlk Araba (Buharlı Taşıt)", "transport", 950, ["inv.wheel", "inv.steam_engine"], "speed:+30%", "Karada kendi gücüyle giden ilk motorlu taşıt! Şehirler arasında yüksek hızla seyahat eder ve liderleri taşır.")
]

items = []
for i, n, cat, cost, reqs, eff, desc in INV:
    items.append({
        "id": "inv." + i,
        "name": n,
        "category": cat,
        "cost": cost,
        "reqs": reqs,
        "effects": mods(eff),
        "description": desc
    })

save("inventions", items, "Buluşlar ve İcatlar", ["id", "name", "category", "cost", "description"])
