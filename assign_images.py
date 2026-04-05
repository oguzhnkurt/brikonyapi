import fitz
import pyodbc
import os

PDF_PATH = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonKatalog.pdf"
IMG_BASE = "/images/projects/katalog"

# PDF sayfa -> proje slug eşlemesi (PDF sayfaları incelenerek)
# Çift sayfalı düzen: her proje tek sayfada, görsel sayfaları çift sayfalı
PAGE_PROJECT_MAP = {
    6:  "istanbul-havalimani-akaryakit-bina",
    7:  "istanbul-havalimani-akaryakit-bina",
    8:  "bayrampasa-cevik-kuvvet-hali-saha",
    9:  "bahcelievler-kentsel-donusum",
    11: "turk-metal-mustafa-ozbek-spor-salonu",
    13: "emek-mahallesi-kentsel-donusum",  # ID:11
    15: "emek-mahallesi-kentsel-donusum",
    17: "ihlamur-apartmani",               # ID:7
    19: "toki-kucukcekmece-ilkogretim",
    21: "toki-afyon-serbanli-tarimkoy",
    23: "odtu-parlar-ogrenci-yurdu",
    25: "afyon-gomu-tarimkoy-villalari",   # ID:6
    26: "trakpark-premium",                # ID:5
    27: "trakpark-premium",
    28: "ferkoline-residences-kagithane",  # ID:4
    29: "besiktas-yildiz-mahallesi-mor-apartmani",  # ID:1
    31: "karabuk-uni-teknik-egitim",
    33: "mugla-bodrum-degirmentepe",
    34: "hacettepe-jeodezi-fotogrametri",
    35: "hacettepe-makina-muhendisligi",
    37: "zonguldak-uni-yemekhane",
    39: "hacettepe-kongre-merkezi",
    41: "hacettepe-beytepe-otomotiv",
    43: "cukurambar-30-daire",
    45: "mugla-bodrum-firuze-tas-evler",
    47: "karabuk-uni-kongre-salonu",
    49: "cukurambar-32-daire",
    51: "hacettepe-morfoloji-dis-cephe",
    53: "hacettepe-kafeterya",
    55: "hacettepe-yabanci-diller",
    57: "ankara-imitkoy-villa",
    59: "cankaya-mkb-mesleki-teknik-lisesi",
    61: "safranbolu-guzel-sanatlar-lisesi",
    63: "safranbolu-hatice-sultan-konagi",
    65: "hacettepe-hastaneleri-cevre",
    67: "hacettepe-altyapi-kanalizasyon",
    69: "hacettepe-dis-hekimligi",
    71: "zonguldak-uni-alapli-myo",
    73: "polatli-sabanözü-yatili-ilkogretim",
    75: "haymana-bumsuz-ilkogretim",
    77: "haymana-oyaca-ilkogretim",
    79: "hacettepe-beytepe-anaokulu",
    80: "hacettepe-beytepe-anaokulu",
    81: "turkan-hanim-apartmani",          # ID:3
    83: "devlet-hastanesi-zonguldak",      # ID:8
}

# Katalog klasöründeki görselleri oku
KATALOG_DIR = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonYapi.Web/wwwroot/images/projects/katalog"
files = sorted(os.listdir(KATALOG_DIR))

# Dosya adından sayfa numarası çıkar: p006_img07.jpeg -> sayfa 6
def page_of(filename):
    try:
        return int(filename[1:4])
    except:
        return 0

# Sayfa -> dosya listesi
from collections import defaultdict
page_files = defaultdict(list)
for f in files:
    p = page_of(f)
    if p > 0:
        page_files[p].append(f)

# DB bağlantısı
conn = pyodbc.connect(
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost;"
    "DATABASE=BrikonYapiDb;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)
cur = conn.cursor()

# Proje slug -> ID eşlemesi
cur.execute("SELECT Id, Slug FROM Projects")
slug_to_id = {row.Slug: row.Id for row in cur.fetchall()}

assigned = 0
for page_num, slug in PAGE_PROJECT_MAP.items():
    proj_id = slug_to_id.get(slug)
    if not proj_id:
        print(f"  BULUNAMADI: {slug}")
        continue

    page_imgs = page_files.get(page_num, [])
    if not page_imgs:
        print(f"  Gorsel yok: sayfa {page_num} -> {slug}")
        continue

    # İlk görsel ana görsel olarak ayarla
    first_img = page_imgs[0]
    main_path = f"{IMG_BASE}/{first_img}"

    # Mevcut MainImagePath yoksa güncelle
    cur.execute("SELECT MainImagePath FROM Projects WHERE Id=?", proj_id)
    row = cur.fetchone()
    if row and not row.MainImagePath:
        cur.execute("UPDATE Projects SET MainImagePath=? WHERE Id=?", main_path, proj_id)

    # Mevcut ProjectImages sil (sadece katalog görselleri)
    cur.execute("DELETE FROM ProjectImages WHERE ProjectId=? AND ImagePath LIKE '/images/projects/katalog%'", proj_id)

    # Tüm sayfa görsellerini ekle
    for idx, img_file in enumerate(page_imgs):
        img_path = f"{IMG_BASE}/{img_file}"
        is_main = 1 if idx == 0 else 0
        cur.execute(
            "INSERT INTO ProjectImages (ProjectId, ImagePath, OrderIndex, IsMain, IsPlan) VALUES (?,?,?,?,0)",
            proj_id, img_path, idx, is_main
        )

    print(f"  OK: sayfa {page_num} -> {slug} ({proj_id}) | {len(page_imgs)} gorsel")
    assigned += 1

conn.commit()
conn.close()
print(f"\nToplam {assigned} proje gorsel atandi.")
