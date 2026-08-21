namespace BrikonYapi.Web.Models.ViewModels
{
    /// <summary>Excel'den okunan tek bir satırın işlenme sonucu.</summary>
    public class OwnerImportRow
    {
        /// <summary>Excel'deki satır numarası (hata mesajlarında gösterilir).</summary>
        public int RowNumber { get; set; }

        public string FullName { get; set; } = "";
        public string Email    { get; set; } = "";
        public string? Phone   { get; set; }

        /// <summary>Hesap açıldıysa üretilen şifre; açılmadıysa boş.</summary>
        public string? Password { get; set; }

        public bool Success { get; set; }

        /// <summary>Başarısızsa sebebi (ör. "Bu e-posta zaten kayıtlı").</summary>
        public string? Error { get; set; }
    }

    public class OwnerBulkImportResult
    {
        public List<OwnerImportRow> Rows { get; set; } = new();

        public int CreatedCount => Rows.Count(r => r.Success);
        public int FailedCount  => Rows.Count(r => !r.Success);
    }
}
