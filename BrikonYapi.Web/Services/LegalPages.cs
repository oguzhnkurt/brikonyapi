using System.Net;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Sanal POS / ödeme kuruluşu başvurusu için sitede bulunması gereken yasal metinler
    /// (mesafeli sözleşme, ön bilgilendirme, iptal-iade, KVKK aydınlatma, gizlilik-çerez).
    ///
    /// Metinlerdeki {{...}} yer tutucuları Admin &gt; Site Ayarları &gt; "Şirket / Yasal Bilgiler"
    /// bölümündeki değerlerle doldurulur; boş bırakılan alan sayfada köşeli parantezli
    /// "[Ticari Unvan]" gibi görünür ki eksik bilgi fark edilsin.
    ///
    /// ÖNEMLİ: Bu metinler genel bir taslaktır; yayına almadan önce şirketin hukuk
    /// danışmanı tarafından gözden geçirilmelidir.
    /// </summary>
    public static class LegalPages
    {
        public record LegalPage(string Slug, string Title, string Summary, string BodyHtml);

        // Admin > Site Ayarları'ndaki anahtarlar
        public const string KeyCompanyTitle = "LegalCompanyTitle";
        public const string KeyTaxOffice    = "LegalTaxOffice";
        public const string KeyTaxNumber    = "LegalTaxNumber";
        public const string KeyMersisNo     = "LegalMersisNo";

        public static readonly string[] SettingKeys = { KeyCompanyTitle, KeyTaxOffice, KeyTaxNumber, KeyMersisNo };

        public static IReadOnlyList<LegalPage> All { get; } = new List<LegalPage>
        {
            new("mesafeli-satis-sozlesmesi", "Mesafeli Hizmet ve Ödeme Sözleşmesi",
                "Kat Maliki Paneli üzerinden yapılan çevrim içi ödemelere ilişkin sözleşme.", MesafeliSozlesme),
            new("on-bilgilendirme-formu", "Ön Bilgilendirme Formu",
                "Ödeme öncesinde tarafınıza sunulan bilgiler.", OnBilgilendirme),
            new("iptal-ve-iade-kosullari", "İptal ve İade Koşulları",
                "Hatalı veya mükerrer ödemelerin iadesi ve iptal süreçleri.", IptalIade),
            new("kvkk-aydinlatma-metni", "KVKK Aydınlatma Metni",
                "6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında aydınlatma metni.", Kvkk),
            new("gizlilik-politikasi", "Gizlilik ve Çerez Politikası",
                "Sitemizde bilgilerinizin nasıl korunduğu ve çerez kullanımı.", Gizlilik),
        };

        public static LegalPage? Find(string? slug) =>
            All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

        /// <summary>Yer tutucuları site ayarlarındaki değerlerle (HTML-encode ederek) doldurur.</summary>
        public static string Render(string html, IDictionary<string, string?> settings, string siteUrl)
        {
            string V(string key, string fallback)
            {
                var v = settings.TryGetValue(key, out var s) ? s : null;
                return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? fallback : v.Trim());
            }

            // MERSİS no girilmemişse o satırı tamamen gizle (köşeli parantezli yer tutucu göstermek yerine).
            if (!settings.TryGetValue(KeyMersisNo, out var mersis) || string.IsNullOrWhiteSpace(mersis))
                html = html.Replace("\n  <li><strong>MERSİS No:</strong> {{MERSIS}}</li>", "");

            return html
                .Replace("{{UNVAN}}",   V(KeyCompanyTitle, "[Ticari Unvan]"))
                .Replace("{{VD}}",      V(KeyTaxOffice, "[Vergi Dairesi]"))
                .Replace("{{VKN}}",     V(KeyTaxNumber, "[Vergi No]"))
                .Replace("{{MERSIS}}",  V(KeyMersisNo, "[MERSİS No]"))
                .Replace("{{ADRES}}",   V("Address", "[Adres]"))
                .Replace("{{EPOSTA}}",  V("Email", "[E-posta]"))
                .Replace("{{TELEFON}}", V("PhoneNumber", "[Telefon]"))
                .Replace("{{SITE}}",    WebUtility.HtmlEncode(siteUrl));
        }

        private const string SirketBilgisi = @"
<ul class=""lg-company"">
  <li><strong>Unvan:</strong> {{UNVAN}}</li>
  <li><strong>Adres:</strong> {{ADRES}}</li>
  <li><strong>Vergi Dairesi / No:</strong> {{VD}} / {{VKN}}</li>
  <li><strong>MERSİS No:</strong> {{MERSIS}}</li>
  <li><strong>Telefon:</strong> {{TELEFON}}</li>
  <li><strong>E-posta:</strong> {{EPOSTA}}</li>
  <li><strong>Web sitesi:</strong> {{SITE}}</li>
</ul>";

        private const string MesafeliSozlesme = @"
<h2>1. Taraflar</h2>
<p><strong>Hizmet Sağlayıcı:</strong></p>" + SirketBilgisi + @"
<p><strong>Alıcı (Kat Maliki):</strong> {{SITE}} adresindeki Kat Maliki Paneli'ne kendisine tanımlanan kullanıcı bilgileriyle giriş yaparak ödeme işlemi gerçekleştiren, Hizmet Sağlayıcı ile arasında bağımsız bölüm/kat karşılığı veya benzeri bir sözleşme ilişkisi bulunan gerçek ya da tüzel kişi.</p>

<h2>2. Konu</h2>
<p>İşbu sözleşmenin konusu; Alıcı'nın, Hizmet Sağlayıcı ile arasında daha önce imzalanmış olan sözleşme (""Ana Sözleşme"") ve buna bağlı ödeme planından doğan taksit, hakediş, aidat ve benzeri ödemelerini {{SITE}} üzerinden kredi kartı / banka kartı ile çevrim içi olarak yapmasına ilişkin usul ve esasların belirlenmesidir.</p>
<p>Ödenecek tutarlar, vade tarihleri ve para birimi Ana Sözleşme ile belirlenmiş olup Kat Maliki Paneli'nde ""Ödemelerim"" ekranında gösterilir. Bu sözleşme, Ana Sözleşme'nin hükümlerini değiştirmez.</p>

<h2>3. Ödeme</h2>
<ul>
  <li>Ödemeler, lisanslı ödeme kuruluşu altyapısı üzerinden 3D Secure doğrulaması ile alınır.</li>
  <li>Kart bilgileri Hizmet Sağlayıcı tarafından görülmez ve saklanmaz; kart verileri doğrudan ödeme kuruluşu tarafından işlenir.</li>
  <li>Taksit seçenekleri, kartı çıkaran bankanın ve ödeme kuruluşunun sunduğu koşullara bağlıdır. Taksitli ödemelerde vade farkı uygulanıp uygulanmayacağı ödeme ekranında gösterilir.</li>
  <li>Başarılı ödeme sonrasında ödeme kaydı Kat Maliki Paneli'nde güncellenir; ödemeye ilişkin mali belge (fatura/makbuz) mevzuata uygun şekilde düzenlenir.</li>
</ul>

<h2>4. Tarafların Yükümlülükleri</h2>
<ul>
  <li>Alıcı, ödeme sırasında kullandığı kartın kendisine ait olduğunu veya kart sahibinin onayıyla işlem yaptığını kabul eder.</li>
  <li>Alıcı, panel giriş bilgilerini gizli tutmakla yükümlüdür; bu bilgilerin üçüncü kişilerce kullanılmasından doğan sonuçlardan Hizmet Sağlayıcı sorumlu değildir.</li>
  <li>Hizmet Sağlayıcı, ödeme kayıtlarını doğru ve güncel tutmakla ve Alıcı'yı ödemeleri hakkında bilgilendirmekle yükümlüdür.</li>
</ul>

<h2>5. Cayma Hakkı</h2>
<p>Çevrim içi ödeme, Ana Sözleşme'den doğan mevcut bir borcun ifasına yöneliktir; yeni bir mal veya hizmet satışı niteliği taşımadığından, ödemenin kendisi bakımından ayrıca cayma hakkı doğmaz. Ana Sözleşme'ye ilişkin hak ve yükümlülükler Ana Sözleşme hükümlerine tabidir.</p>

<h2>6. İptal ve İade</h2>
<p>Hatalı veya mükerrer ödemelerin iadesi ""İptal ve İade Koşulları"" sayfasında belirtilen esaslara göre yapılır.</p>

<h2>7. Uyuşmazlıkların Çözümü</h2>
<p>İşbu sözleşmeden doğan uyuşmazlıklarda, Ticaret Bakanlığı'nca ilan edilen parasal sınırlar dâhilinde Alıcı'nın yerleşim yerindeki Tüketici Hakem Heyetleri, bu sınırları aşan durumlarda Tüketici Mahkemeleri yetkilidir.</p>

<h2>8. Yürürlük</h2>
<p>Alıcı, ödeme işlemini onaylamadan önce işbu sözleşmeyi ve Ön Bilgilendirme Formu'nu okuduğunu ve kabul ettiğini beyan eder. Sözleşme, ödeme işleminin onaylanmasıyla yürürlüğe girer.</p>";

        private const string OnBilgilendirme = @"
<h2>1. Hizmet Sağlayıcı Bilgileri</h2>" + SirketBilgisi + @"

<h2>2. Hizmetin Konusu</h2>
<p>{{SITE}} üzerindeki Kat Maliki Paneli aracılığıyla, Alıcı ile Hizmet Sağlayıcı arasında imzalanmış sözleşmeden doğan taksit, hakediş, aidat ve benzeri ödemelerin kredi kartı veya banka kartı ile çevrim içi olarak yapılması.</p>

<h2>3. Ödeme Tutarı</h2>
<p>Ödenecek tutar, vade tarihi ve para birimi ödeme ekranında ve Kat Maliki Paneli'ndeki ""Ödemelerim"" bölümünde, vergiler dâhil toplam tutar olarak gösterilir. Taksitli ödemelerde uygulanacak koşullar ödeme ekranında ayrıca belirtilir.</p>

<h2>4. Ödeme Şekli</h2>
<p>Ödemeler, lisanslı ödeme kuruluşu altyapısı üzerinden 3D Secure doğrulaması ile alınır. Kart bilgileri Hizmet Sağlayıcı tarafından saklanmaz.</p>

<h2>5. Cayma Hakkı</h2>
<p>Çevrim içi ödeme mevcut bir sözleşmeden doğan borcun ifası niteliğinde olduğundan ayrıca cayma hakkı doğmaz. Hatalı veya mükerrer ödemeler ""İptal ve İade Koşulları"" çerçevesinde iade edilir.</p>

<h2>6. Şikâyet ve İtiraz</h2>
<p>Ödemelerinize ilişkin talep ve şikâyetlerinizi {{EPOSTA}} adresine veya {{TELEFON}} numarasına iletebilirsiniz. Uyuşmazlık hâlinde Tüketici Hakem Heyetleri ve Tüketici Mahkemelerine başvurabilirsiniz.</p>";

        private const string IptalIade = @"
<h2>1. Genel</h2>
<p>{{SITE}} üzerinden yapılan ödemeler, Kat Maliki ile {{UNVAN}} arasında imzalanmış sözleşmeden doğan ödeme planına ilişkindir. Bu nedenle usulüne uygun yapılmış bir ödemenin iadesi, ancak ilgili sözleşme hükümleri çerçevesinde mümkündür.</p>

<h2>2. Hatalı veya Mükerrer Ödemeler</h2>
<ul>
  <li>Aynı taksit için birden fazla ödeme yapılması, tutarın yanlış girilmesi veya teknik bir hata nedeniyle fazladan tahsilat olması hâlinde, fazla ödenen tutar iade edilir.</li>
  <li>İade talebinizi, ödeme tarihinden itibaren makul süre içinde {{EPOSTA}} adresine ad-soyad, bağımsız bölüm bilgisi ve ödeme tarihi ile birlikte iletmeniz yeterlidir.</li>
  <li>Talebin incelenip onaylanmasının ardından iade, ödemenin yapıldığı karta en geç 14 gün içinde yapılır.</li>
</ul>

<h2>3. İadenin Hesaba Yansıması</h2>
<p>İade tutarının kart hesabınıza yansıma süresi, kartı çıkaran bankanın işlem sürelerine bağlıdır. Taksitli işlemlerde iade, bankanın uygulamasına göre taksitler hâlinde yansıyabilir.</p>

<h2>4. Ödeme İptali</h2>
<p>Aynı gün içinde, gün sonu işlemleri tamamlanmadan bildirilen hatalı ödemeler iptal edilebilir; iptal edilen tutar kartınıza bloke kaldırma şeklinde yansır.</p>

<h2>5. İletişim</h2>
<p>İptal ve iade talepleriniz için: {{EPOSTA}} · {{TELEFON}}</p>";

        private const string Kvkk = @"
<h2>1. Veri Sorumlusu</h2>
<p>6698 sayılı Kişisel Verilerin Korunması Kanunu (""KVKK"") uyarınca kişisel verileriniz, veri sorumlusu sıfatıyla aşağıda bilgileri yer alan şirketimiz tarafından işlenmektedir:</p>" + SirketBilgisi + @"

<h2>2. İşlenen Kişisel Veriler</h2>
<ul>
  <li><strong>Kimlik:</strong> ad, soyad, T.C. kimlik numarası</li>
  <li><strong>İletişim:</strong> telefon numarası, e-posta adresi, adres</li>
  <li><strong>Müşteri işlem:</strong> bağımsız bölüm bilgileri, ödeme planı, ödeme kayıtları, dekontlar, talep ve şikâyetler</li>
  <li><strong>Finans:</strong> ödeme tutarı ve işlem sonucu (kart numarası ve güvenlik kodu şirketimizce saklanmaz; ödeme kuruluşu tarafından işlenir)</li>
  <li><strong>İşlem güvenliği:</strong> IP adresi, giriş kayıtları, oturum bilgileri</li>
</ul>

<h2>3. İşleme Amaçları</h2>
<ul>
  <li>Kat maliki ile imzalanan sözleşmenin kurulması ve ifası, ödemelerin alınması ve takibi</li>
  <li>Ödeme hatırlatmaları, proje ve inşaat ilerleme bildirimlerinin (WhatsApp, SMS, e-posta) gönderilmesi</li>
  <li>Kat Maliki Paneli'ne güvenli girişin sağlanması</li>
  <li>Muhasebe, fatura ve yasal yükümlülüklerin yerine getirilmesi</li>
  <li>Talep ve şikâyetlerin yanıtlanması</li>
</ul>

<h2>4. Hukuki Sebepler</h2>
<p>Kişisel verileriniz KVKK'nın 5/2 maddesinde yer alan; sözleşmenin kurulması veya ifası için gerekli olması (c), veri sorumlusunun hukuki yükümlülüğünü yerine getirmesi (ç) ve temel hak ve özgürlüklerinize zarar vermemek kaydıyla meşru menfaatimiz (f) hukuki sebeplerine dayanılarak işlenir.</p>

<h2>5. Aktarım</h2>
<p>Kişisel verileriniz, yukarıdaki amaçlarla sınırlı olarak; ödeme işlemleri için lisanslı ödeme kuruluşuna ve bankalara, bildirim gönderimi için iletişim hizmet sağlayıcılarına, yasal zorunluluk hâlinde yetkili kamu kurum ve kuruluşlarına aktarılabilir. Web sitesi altyapısı ve mesajlaşma hizmetleri için yurt dışında sunucu barındıran hizmet sağlayıcılar kullanılması hâlinde aktarım, KVKK'nın 9. maddesine uygun şekilde gerçekleştirilir.</p>

<h2>6. Toplama Yöntemi</h2>
<p>Kişisel verileriniz; sözleşme süreçlerinde tarafınızca verilen bilgiler, Kat Maliki Paneli, web sitesi formları ve iletişim kanalları aracılığıyla elektronik ve fiziki ortamda toplanır.</p>

<h2>7. Haklarınız</h2>
<p>KVKK'nın 11. maddesi uyarınca; kişisel verilerinizin işlenip işlenmediğini öğrenme, bilgi talep etme, işlenme amacını öğrenme, aktarıldığı üçüncü kişileri bilme, eksik veya yanlış işlenmişse düzeltilmesini, şartları oluştuğunda silinmesini veya yok edilmesini isteme, itiraz etme ve zarara uğramanız hâlinde giderilmesini talep etme haklarına sahipsiniz.</p>
<p>Taleplerinizi {{ADRES}} adresine yazılı olarak veya {{EPOSTA}} adresine iletebilirsiniz. Başvurular en geç 30 gün içinde ücretsiz olarak sonuçlandırılır.</p>";

        private const string Gizlilik = @"
<h2>1. Genel</h2>
<p>{{UNVAN}} olarak {{SITE}} ziyaretçilerimizin ve kat maliklerimizin gizliliğine önem veriyoruz. Kişisel verilerin işlenmesine ilişkin ayrıntılı bilgi ""KVKK Aydınlatma Metni"" sayfasında yer almaktadır.</p>

<h2>2. Ödeme Güvenliği</h2>
<ul>
  <li>Sitemiz SSL sertifikası ile şifrelenmiş bağlantı (HTTPS) üzerinden hizmet verir.</li>
  <li>Kartlı ödemeler lisanslı ödeme kuruluşu altyapısında 3D Secure ile alınır; kart numarası, son kullanma tarihi ve güvenlik kodu sunucularımızda saklanmaz.</li>
</ul>

<h2>3. Çerezler</h2>
<p>Sitemizde yalnızca sitenin çalışması için zorunlu çerezler kullanılır:</p>
<ul>
  <li><strong>Oturum çerezleri:</strong> Kat Maliki Paneli ve yönetim paneline giriş yapan kullanıcıların oturumunu sürdürmek için.</li>
  <li><strong>Güvenlik çerezleri:</strong> Formların sahte isteklere karşı korunması için.</li>
  <li><strong>Dil tercihi çerezi:</strong> Seçtiğiniz site dilini hatırlamak için.</li>
</ul>
<p>Tarayıcı ayarlarınızdan çerezleri silebilir veya engelleyebilirsiniz; ancak bu durumda panel girişleri çalışmayabilir.</p>

<h2>4. Üçüncü Taraf Bağlantılar</h2>
<p>Sitemizde yer alan harita, sosyal medya gibi üçüncü taraf bağlantılarının gizlilik uygulamalarından ilgili taraflar sorumludur.</p>

<h2>5. İletişim</h2>
<p>Gizlilikle ilgili sorularınız için: {{EPOSTA}} · {{TELEFON}}</p>";
    }
}
