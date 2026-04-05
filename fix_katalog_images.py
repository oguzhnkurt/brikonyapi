"""
1. Orphan eski düşük kaliteli dosyaları sil (index > PAGE_MAX)
2. DB MainImagePath ve ProjectImages'ı temizle
3. İnce strip görüntülü sayfalar için sütun bazlı daha iyi crop'lar oluştur
"""
import fitz, pyodbc, os
from PIL import Image

PDF_PATH    = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonKatalog.pdf"
KATALOG_DIR = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonYapi.Web/wwwroot/images/projects/katalog"
IMG_BASE    = "/images/projects/katalog"

# extract_hq_images.py'nin her sayfa için ürettiği görsel sayısı
PAGE_MAX = {
    6:2,7:4,8:2,9:9,11:6,13:8,15:8,17:7,19:9,21:5,23:6,25:9,
    26:13,27:8,28:13,29:12,31:7,33:7,34:2,35:11,37:10,39:8,
    41:8,43:10,45:10,47:10,49:12,51:8,53:12,54:3,55:8,57:8,
    59:6,61:8,63:8,65:8,67:12,69:8,71:12,73:8,75:12,77:12,
    79:12,80:4,81:4,83:12
}
# Atanmayan sayfalar (kapak vs) - tamamen sil
UNASSIGNED_PAGES = {1, 3, 84, 85}

def parse_file(f):
    """p008_img08.jpeg -> (page=8, idx=8)"""
    try:
        pn  = int(f[1:4])    # "008" -> 8
        idx = int(f[8:10])   # "08"  -> 8  (img08)
        return pn, idx
    except:
        return None, None

# ─── 1. Orphan dosyaları tespit et ve sil ────────────────────────────────────
print("=== 1. Orphan dosya temizliği ===")
deleted_files = set()

for f in sorted(os.listdir(KATALOG_DIR)):
    if not f.endswith('.jpeg'): continue
    pn, idx = parse_file(f)
    if pn is None: continue

    should_delete = False
    if pn in UNASSIGNED_PAGES:
        should_delete = True
    elif pn in PAGE_MAX and idx > PAGE_MAX[pn]:
        should_delete = True

    if should_delete:
        os.remove(os.path.join(KATALOG_DIR, f))
        deleted_files.add(f)
        print(f"  Silindi: {f}")

print(f"  Toplam {len(deleted_files)} orphan dosya silindi")

# ─── 2. DB temizliği ─────────────────────────────────────────────────────────
print("\n=== 2. DB temizliği ===")
conn = pyodbc.connect(
    "DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=BrikonYapiDb;"
    "Trusted_Connection=yes;TrustServerCertificate=yes;"
)
cur = conn.cursor()

# ProjectImages'tan silinmiş dosyaları kaldır
cur.execute("SELECT Id, ProjectId, ImagePath FROM ProjectImages WHERE ImagePath LIKE '/images/projects/katalog%'")
rows = cur.fetchall()
removed = 0
for row in rows:
    fname = row.ImagePath.split('/')[-1]
    if fname in deleted_files:
        cur.execute("DELETE FROM ProjectImages WHERE Id=?", row.Id)
        removed += 1
print(f"  {removed} ProjectImages kaydı kaldırıldı")

# MainImagePath orphan dosyaya işaret ediyorsa img01'e güncelle
cur.execute("SELECT Id, Slug, MainImagePath FROM Projects WHERE MainImagePath LIKE '/images/projects/katalog%'")
projects = cur.fetchall()
main_fixed = 0
for p in projects:
    fname = p.MainImagePath.split('/')[-1] if p.MainImagePath else None
    if fname and fname in deleted_files:
        try:
            pn = int(fname[1:4])
            new_main = f"{IMG_BASE}/p{pn:03d}_img01.jpeg"
            if os.path.exists(os.path.join(KATALOG_DIR, f"p{pn:03d}_img01.jpeg")):
                cur.execute("UPDATE Projects SET MainImagePath=? WHERE Id=?", new_main, p.Id)
                print(f"  MainImagePath düzeltildi ID {p.Id} ({p.Slug}): -> p{pn:03d}_img01.jpeg")
                main_fixed += 1
        except:
            pass
print(f"  {main_fixed} proje MainImagePath güncellendi")

# ─── 3. İnce strip sayfalar için sütun crop'ları ────────────────────────────
print("\n=== 3. İnce görsel sayfaları için sütun crop'u ===")

ZOOM = 4
doc = fitz.open(PDF_PATH)

for pn in sorted(PAGE_MAX.keys()):
    img01_path = os.path.join(KATALOG_DIR, f"p{pn:03d}_img01.jpeg")
    if not os.path.exists(img01_path):
        continue

    img01 = Image.open(img01_path)
    ratio = img01.width / img01.height
    if ratio <= 2.5:
        continue  # Zaten iyi oran

    # Bu sayfa ince — sütun bazlı crop yap
    page  = doc[pn - 1]
    mat   = fitz.Matrix(ZOOM, ZOOM)
    infos = page.get_image_info(xrefs=True)

    MIN_W_PTS = 80; MIN_H_PTS = 40
    left_boxes  = [i["bbox"] for i in infos
                   if (i["bbox"][2]-i["bbox"][0]) > MIN_W_PTS
                   and (i["bbox"][3]-i["bbox"][1]) > MIN_H_PTS
                   and i["bbox"][0] < 200]
    right_boxes = [i["bbox"] for i in infos
                   if (i["bbox"][2]-i["bbox"][0]) > MIN_W_PTS
                   and (i["bbox"][3]-i["bbox"][1]) > MIN_H_PTS
                   and i["bbox"][0] >= 200]

    best_crop = None
    best_ratio_diff = 999

    for boxes in [left_boxes, right_boxes]:
        if not boxes: continue
        x0 = min(b[0] for b in boxes)
        y0 = min(b[1] for b in boxes)
        x1 = max(b[2] for b in boxes)
        y1 = max(b[3] for b in boxes)
        w = x1 - x0; h = y1 - y0
        if w < 80 or h < 80: continue
        cr = w / h
        diff = abs(cr - 1.6)  # 1.6 hedef oran
        if diff < best_ratio_diff:
            best_ratio_diff = diff
            best_crop = fitz.Rect(x0, y0, x1, y1)

    if best_crop is None:
        print(f"  Sayfa {pn}: uygun sütun crop bulunamadı (ratio={ratio:.2f})")
        continue

    pix = page.get_pixmap(matrix=mat, clip=best_crop, alpha=False)
    # Temp dosyaya kaydet sonra replace et
    tmp_path = img01_path + ".tmp"
    pix.save(tmp_path, "jpeg", jpg_quality=92)
    os.replace(tmp_path, img01_path)
    new_ratio = pix.width / pix.height
    print(f"  Sayfa {pn}: {img01.width}x{img01.height} (ratio={ratio:.2f}) "
          f"-> {pix.width}x{pix.height} (ratio={new_ratio:.2f}) ✓")

doc.close()
conn.commit()
conn.close()
print("\nTamamlandı.")
