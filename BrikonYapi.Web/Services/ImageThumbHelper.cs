using System;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Kart görünümlerinde (ana sayfa proje marquee'si, proje listesi ızgarası, hero alt sekmeleri)
    /// büyük görsel yerine küçük bir "thumbs" varyantı kullanılmasını sağlar.
    ///
    /// Sadece statik seed görselleri için geçerlidir ("/images/projects/web/{slug}/{dosya}").
    /// Bu klasördeki her görsel için "{slug}/thumbs/{dosya}" altında 480px genişliğinde,
    /// küçük boyutlu bir kopya önceden üretilmiştir (bkz. proje kökünde resize_images.py).
    ///
    /// Admin panelinden yüklenen görseller ("/uploads/projects/...") zaten yükleme sırasında
    /// ImageSharp ile 1920px genişliğe sıkıştırıldığından (bkz. ProjectsController.SaveFileAsync),
    /// bunlar için thumb üretmeye gerek yok — olduğu gibi döner.
    /// </summary>
    public static class ImageThumbHelper
    {
        private const string Marker = "/images/projects/web/";

        public static string Card(string? path)
        {
            if (string.IsNullOrEmpty(path)) return path ?? "";

            var idx = path.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return path;

            var afterMarker = path[(idx + Marker.Length)..]; // "{slug}/{dosya}"
            var slashIdx = afterMarker.IndexOf('/');
            if (slashIdx < 0) return path;

            var slug = afterMarker[..slashIdx];
            var file = afterMarker[(slashIdx + 1)..];

            // Alt klasördeki galeri görselleri (ör. "plans/01.jpg") için thumb üretilmedi — olduğu gibi dön.
            if (file.Contains('/')) return path;

            return $"{path[..idx]}{Marker}{slug}/thumbs/{file}";
        }
    }
}
