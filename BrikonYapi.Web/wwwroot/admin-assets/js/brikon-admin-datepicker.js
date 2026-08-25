/* ==========================================================================
   Brikon Yapı — Admin panel tarih/saat seçici
   Tarayıcıların yerleşik <input type="datetime-local"> bileşeni (özellikle saat
   seçimi) sade/kurumsal görünmüyor ve tarayıcıdan tarayıcıya farklı davranıyordu.
   Bu dosya, admin temasına zaten paket olarak dahil olan flatpickr kütüphanesini
   (vendor.min.js/vendor.min.css içinde) Türkçe yerelleştirme ile başlatan tek bir
   yardımcı fonksiyon sağlar; tüm admin formları bunu kullanır.
   ========================================================================== */
(function () {
    if (typeof window.flatpickr === "undefined") return;

    var trLocale = {
        weekdays: {
            shorthand: ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"],
            longhand: ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"]
        },
        months: {
            shorthand: ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"],
            longhand: ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"]
        },
        firstDayOfWeek: 1,
        rangeSeparator: " — ",
        weekAbbreviation: "Hf",
        scrollTitle: "Artırmak için kaydırın",
        toggleTitle: "AA/PM değiştirmek için tıklayın",
        time_24hr: true
    };

    /**
     * Bir <input> elemanını Türkçe, 24 saatlik, "gg.aa.yyyy ss:dd" görünümlü
     * flatpickr tarih-saat seçiciye çevirir. Elemanın gerçek (forma giden) değeri
     * "Y-m-d\TH:i" biçiminde kalır — bu, tarayıcının datetime-local için ürettiği
     * biçimle aynıdır, böylece sunucu tarafı hiçbir değişiklik gerektirmez.
     */
    window.brikonInitDateTimePicker = function (input, extraOptions) {
        if (!input) return null;

        var options = Object.assign({
            enableTime: true,
            time_24hr: true,
            dateFormat: "Y-m-d\\TH:i",
            altInput: true,
            altFormat: "d.m.Y H:i",
            allowInput: true,
            disableMobile: true,
            locale: trLocale
        }, extraOptions || {});

        var fp = flatpickr(input, options);

        if (fp && fp.altInput) {
            fp.altInput.classList.add("form-control");
            if (input.classList.contains("form-control-sm")) {
                fp.altInput.classList.add("form-control-sm");
            }
        }

        return fp;
    };
})();
