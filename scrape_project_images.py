"""
brikonyapi.com proje sayfalarından orijinal kaliteli görselleri indir,
DB'yi güncelle.
"""
import requests, pyodbc, os, re, time
from urllib.parse import urljoin

BASE = "https://brikonyapi.com/"
HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"}
IMG_ROOT = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonYapi.Web/wwwroot/images/projects/web"
IMG_URL_ROOT = "/images/projects/web"

# DB slug -> site URL eşlemesi
SLUG_URL = {
    "besiktas-yildiz-mahallesi-mor-apartmani":      "besiktas-mor-apartmani-insaati.html",
    "bakirkoy-senlik-mahallesi-bilgic-apartmani":   "akdeniz-apt.-yukari-bahcelievler-mah..html",
    "turkan-hanim-apartmani":                        "turkan-hanim-apartmani-ankara-emek.html",
    "ihlamur-apartmani":                             "ihlamur-apt.kentsel-donusum-projesi.html",
    "trakpark-premium":                              "trakpark-premium-42-adet-villa-ve-sosyal-tesis-insaati.html",
    "afyon-gomu-tarimkoy-villalari":                 "afyon---gomu-55-adet-tarimkoy-villasi-insaati.html",
    "devlet-hastanesi-zonguldak":                    "toki---zonguldak-devrek-100-yatakli-yeni-devlet-hastanesi-insaati-ile-altyapi-ve-cevre-duzenlemesi-insaati.html",
    "istanbul-havalimani-akaryakit-bina":            "istanbul-yeni-havalimani-akaryakit-yonetim-binasi.html",
    "istanbul-havalimani-camii":                     "istanbul-3.havalimani-cami-ince-isleri.html",
    "toki-kucukcekmece-ilkogretim":                  "toki-istanbul-kucukcekmece-32-derslikli-ilkogretim-okulu--insaati.html",
    "toki-afyon-serbanli-tarimkoy":                  "afyon---serban-55-adet-tarimkoy-villasi-insaati.html",
    "odtu-parlar-ogrenci-yurdu":                     "odtu-prof.-dr.-mustafa-n.-parlar-egitim-ve-arastirma-vakfi-300-kisilik-ogrenci-yurdu-insaati.html",
    "karabuk-uni-teknik-egitim":                     "karabuk-universitesi-kampusu-insaati.html",
    "mugla-bodrum-degirmentepe":                     "bodrum-degirmentepe-evleri-villa-insaati.html",
    "hacettepe-jeodezi-fotogrametri":                "hacettepe-universitesi-beytepe-kampusu-jeodezi-ve-fotogrometri-muhendisligi-bolumu-insaati.html",
    "hacettepe-makina-muhendisligi":                 "hacettepe-universitesi-beytepe-kampusu-makina-muhendisligi-bolumu-insaati.html",
    "zonguldak-uni-yemekhane":                       "zonguldak-karaelmas-universitesi-merkezi-yemekhane-ve-mutfak-binalari-insaati.html",
    "hacettepe-kongre-merkezi":                      "hacettepe-universitesi-beytepe-kampusu-kongre-merkezi-insaati.html",
    "hacettepe-beytepe-otomotiv":                    "hacettepe-universitesi-beytepe-kampusu-otomotiv-muhendisligi-bolumu-bina-insaati.html",
    "cukurambar-30-daire":                           "cok-katli-konut-30-daire.html",
    "mugla-bodrum-firuze-tas-evler":                 "firuzan-tas-evler-renovasyon.html",
    "karabuk-uni-kongre-salonu":                     "karabuk-universitesi-kongre-salonu-insaati.html",
    "cukurambar-32-daire":                           "cok-katli-konut-32-daire-4-dukkan.html",
    "hacettepe-morfoloji-dis-cephe":                 "hacettepe-universitesi-morfoloji-binasi-dis-cephe-onarimi.html",
    "hacettepe-kafeterya":                           "hacettepe-universitesi-kafeterya-binasi-insaati.html",
    "hacettepe-yabanci-diller":                      "hacettepe-universitesi-beytepe-kampusu-yabanci-diller-yuksekokulu-insaati.html",
    "ankara-imitkoy-villa":                          "umitkoy-villa-insaatlari.html",
    "cankaya-mkb-mesleki-teknik-lisesi":             "cankaya-anadolu-otelcilik-turizm-meslek-lisesi.html",
    "safranbolu-guzel-sanatlar-lisesi":              "safranbolu-guzel-sanatlar-lisesi.html",
    "safranbolu-hatice-sultan-konagi":               "safranbolu-hatice-sultan-konagi-onarim-ve-restorasyonu.html",
    "hacettepe-hastaneleri-cevre":                   "hacettepe-universitesi-hastaneleri-cevre-duzenlemesi.html",
    "hacettepe-altyapi-kanalizasyon":                "hacettepe-universitesi-agac-isleri-endustri-muhendisligi-bolumu-altyapi-kanalizasyon-insaati.html",
    "hacettepe-dis-hekimligi":                       "hacettepe-universitesi-dis-hekimligi-fakultesi-buyuk-onarim-insaati.html",
    "zonguldak-uni-alapli-myo":                      "zonguldak-karaelmas-universitesi-alapli-meslek-yuksek-okulu-ve-lojman-insaati.html",
    "polatli-sabanözü-yatili-ilkogretim":            "polatli-sabanozu-yatili-ilkogretim-bolge-okulu-insaati.html",
    "haymana-bumsuz-ilkogretim":                     "haymana-bumsuz-ilkogretim-okulu-insaati.html",
    "haymana-oyaca-ilkogretim":                      "haymana-oyaca-ilkogretim-okulu-insaati.html",
    "hacettepe-beytepe-anaokulu":                    "hacettepe-universitesi-beytepe-anaokulu-insaati.html",
    "karabuk-universitesi":                          "karabuk-universitesi-kampusu-insaati.html",
}

def scrape_images(page_url):
    """Sayfadan dosya/* görsel URL'lerini çek"""
    try:
        r = requests.get(page_url, headers=HEADERS, timeout=15)
        if r.status_code != 200 or not r.text.strip():
            return []
        imgs = re.findall(r'"(dosya/[^"]+\.[jJpPnNgGwWbB][^"]*)"', r.text)
        seen, result = set(), []
        for img in imgs:
            if img not in seen:
                seen.add(img)
                result.append(urljoin(BASE, img))
        return result
    except Exception as e:
        print(f"  HATA {page_url}: {e}")
        return []

def download_image(url, filepath):
    try:
        r = requests.get(url, headers=HEADERS, timeout=20, stream=True)
        if r.status_code == 200:
            with open(filepath, "wb") as f:
                for chunk in r.iter_content(8192):
                    f.write(chunk)
            return os.path.getsize(filepath) > 1000
    except:
        pass
    return False

# DB bağlantısı
conn = pyodbc.connect(
    "DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=BrikonYapiDb;"
    "Trusted_Connection=yes;TrustServerCertificate=yes;"
)
cur = conn.cursor()
cur.execute("SELECT Id, Slug FROM Projects")
db_projects = {row.Slug: row.Id for row in cur.fetchall()}

total_imgs = 0
for slug, site_page in SLUG_URL.items():
    proj_id = db_projects.get(slug)
    if not proj_id:
        print(f"DB'de yok: {slug}")
        continue

    page_url = BASE + site_page
    img_urls = scrape_images(page_url)
    if not img_urls:
        print(f"  [{slug}] Görsel bulunamadı ({page_url})")
        continue

    # Klasör oluştur
    out_dir = os.path.join(IMG_ROOT, slug)
    os.makedirs(out_dir, exist_ok=True)

    # İndir
    downloaded = []
    for idx, url in enumerate(img_urls):
        ext = url.rsplit(".", 1)[-1].lower()
        if ext not in ("jpg", "jpeg", "png", "webp"):
            ext = "jpg"
        fname = f"{idx+1:02d}.{ext}"
        fpath = os.path.join(out_dir, fname)
        if download_image(url, fpath):
            downloaded.append(fname)

    if not downloaded:
        print(f"  [{slug}] İndirilemedi")
        continue

    # DB güncelle — önce eski katalog görsellerini sil
    cur.execute("DELETE FROM ProjectImages WHERE ProjectId=? AND ImagePath LIKE '/images/projects/katalog%'", proj_id)
    # Eski web görsellerini de temizle
    cur.execute("DELETE FROM ProjectImages WHERE ProjectId=? AND ImagePath LIKE '/images/projects/web%'", proj_id)

    # Yeni görselleri ekle
    for idx, fname in enumerate(downloaded):
        img_path = f"{IMG_URL_ROOT}/{slug}/{fname}"
        cur.execute(
            "INSERT INTO ProjectImages (ProjectId, ImagePath, OrderIndex, IsMain, IsPlan) VALUES (?,?,?,?,0)",
            proj_id, img_path, idx, 1 if idx == 0 else 0
        )

    # MainImagePath güncelle
    main_path = f"{IMG_URL_ROOT}/{slug}/{downloaded[0]}"
    cur.execute("UPDATE Projects SET MainImagePath=? WHERE Id=?", main_path, proj_id)

    print(f"  OK [{slug}] -> {len(downloaded)} görsel")
    total_imgs += len(downloaded)
    time.sleep(0.3)

conn.commit()
conn.close()
print(f"\nToplam {total_imgs} görsel indirildi.")
