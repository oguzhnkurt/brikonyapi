namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>
    /// Bir kat malikinin belirli bir projeye erişimini admin'in tek tek atadığı kayıt.
    /// Bağımsız bölüm sahipliğinden (Unit) bağımsızdır: admin bir maliki, bölümü olmayan
    /// bir projenin oylamasına/sohbetine de dahil edebilir, ya da bölümü olan bir projeden
    /// dışarıda bırakabilir. Ödeme planı ve inşaat ilerlemesi hâlâ gerçek Unit sahipliğine
    /// dayanır — bu tablo yalnızca Oylama ve Sohbet modüllerinin görünürlüğünü kapsar.
    /// </summary>
    public class OwnerProjectAccess
    {
        public int Id { get; set; }

        public int OwnerId { get; set; }
        public Owner? Owner { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>Bu projenin oylamalarını görebilir/oy kullanabilir.</summary>
        public bool CanSeeProject { get; set; } = true;

        /// <summary>Bu projenin sohbet grubuna katılabilir.</summary>
        public bool CanChat { get; set; } = true;

        /// <summary>
        /// Bu malik, bu projenin Temsil Heyeti (kat malikleri temsilcisi) üyesi olarak atanmış mı?
        /// Bağımsız bölüm sahipliğinden bağımsızdır — admin tek tek atar. Kat Maliki tarafında
        /// listelerde küçük bir yıldız rozetiyle gösterilir.
        /// </summary>
        public bool IsCommitteeMember { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
