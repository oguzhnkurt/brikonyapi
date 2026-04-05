import fitz
import os

PDF_PATH = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonKatalog.pdf"
OUT_DIR  = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonYapi.Web/wwwroot/images/projects/katalog"

ZOOM = 4  # 288 DPI — 4x büyütme

TARGET_PAGES = [
    6,7,8,9,11,13,15,17,19,21,23,25,26,27,28,29,
    31,33,34,35,37,39,41,43,45,47,49,51,53,54,55,
    57,59,61,63,65,67,69,71,73,75,77,79,80,81,83
]

MIN_W = 80   # PDF points — bu boyuttan küçük görseller (ince çizgiler, logolar) atlanır
MIN_H = 50

mat = fitz.Matrix(ZOOM, ZOOM)
doc = fitz.open(PDF_PATH)

total = 0
for page_num in TARGET_PAGES:
    page = doc[page_num - 1]
    img_infos = page.get_image_info(xrefs=True)

    seen_xrefs = set()
    idx = 0
    for info in img_infos:
        bbox  = info["bbox"]        # PDF koordinatları (points)
        xref  = info.get("xref", 0)
        w_pts = bbox[2] - bbox[0]
        h_pts = bbox[3] - bbox[1]

        if w_pts < MIN_W or h_pts < MIN_H:
            continue
        if xref and xref in seen_xrefs:
            continue
        if xref:
            seen_xrefs.add(xref)

        idx += 1
        clip   = fitz.Rect(bbox)
        clip_pix = page.get_pixmap(matrix=mat, clip=clip, alpha=False)

        if clip_pix.width < 100 or clip_pix.height < 100:
            continue

        filename = f"p{page_num:03d}_img{idx:02d}.jpeg"
        filepath = os.path.join(OUT_DIR, filename)

        img_bytes = clip_pix.tobytes("jpeg", jpg_quality=92)
        with open(filepath, "wb") as f:
            f.write(img_bytes)

        total += 1

    print(f"Sayfa {page_num:2d}: {idx} görsel | örnek: p{page_num:03d}_img01.jpeg "
          f"({clip_pix.width}x{clip_pix.height} px)" if idx > 0 else f"Sayfa {page_num:2d}: 0 görsel")

doc.close()
print(f"\nToplam {total} yüksek kaliteli görsel kaydedildi -> {OUT_DIR}")
