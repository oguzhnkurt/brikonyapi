import fitz
import os
import sys

PDF_PATH = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonKatalog.pdf"
OUT_DIR  = "C:/Users/Oguzhan/IdeaProjects/BrikonYapi/BrikonYapi.Web/wwwroot/images/projects/katalog"

os.makedirs(OUT_DIR, exist_ok=True)

doc = fitz.open(PDF_PATH)
print(f"Toplam sayfa: {doc.page_count}")

img_count = 0
for page_num in range(doc.page_count):
    page = doc[page_num]
    images = page.get_images(full=True)

    for img_index, img in enumerate(images):
        xref = img[0]
        try:
            base_image = doc.extract_image(xref)
            img_bytes = base_image["image"]
            img_ext   = base_image["ext"]
            width     = base_image["width"]
            height    = base_image["height"]

            # Cok kucuk gorsel atlaniyor (logo, ikon vb)
            if width < 100 or height < 100:
                continue

            filename = f"p{page_num+1:03d}_img{img_index+1:02d}.{img_ext}"
            filepath = os.path.join(OUT_DIR, filename)
            with open(filepath, "wb") as f:
                f.write(img_bytes)
            img_count += 1
            print(f"  Sayfa {page_num+1} | {filename} | {width}x{height}")
        except Exception as e:
            print(f"  HATA sayfa {page_num+1} img {img_index}: {e}")

print(f"\nToplam {img_count} gorsel cikarildi -> {OUT_DIR}")
doc.close()
