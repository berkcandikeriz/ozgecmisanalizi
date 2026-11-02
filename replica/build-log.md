# Build log

| ID | screen | date | status | notes |
| --- | --- | --- | --- | --- |
| S01 | Landing | 2026-10-28 | done | imza öğesi: işaretlenmiş CV + puan halkası |
| S02 | Giriş / Kayıt | 2026-10-28 | done | JWT (API) → cookie (MVC) |
| S03 | Panel | 2026-10-28 | done | |
| S04 | CV yükle | 2026-10-28 | done | PDF/DOCX/TXT, 10 MB |
| S05 | Analiz sonucu | 2026-10-28 | done | puan, alt puanlar, ATS, eksik beceri, maaş, kariyer, PDF rapor |
| S06 | CV düzenleyici | 2026-10-28 | done | 4 şablon, tekrarlanabilir bölümler |
| S07 | İlana göre uyarla | 2026-10-28 | done | |
| S08 | İş önerileri | 2026-10-28 | done | API anahtarı yoksa yalnızca site bağlantıları |
| S09 | Başvuru takip | 2026-10-28 | done | sürükle-bırak yok; aşama seçimiyle taşıma |
| S10 | Ön yazı | 2026-10-28 | done | AI yoksa şablon |
| S11 | Meslekler | 2026-10-28 | done | |

Daha zor çıkan: CV metninden yapı çıkarma (sezgisel; kullanıcı düzeltebilsin diye editöre yönlendiriliyor).
Bulut ortamında .NET SDK indirilemediği için derleme ve testler kullanıcı makinesinde `kurulum.ps1` ile yapılır.

## İş arama modülü revizyonu
- Araştırma: LinkedIn (iş ortağı API), Indeed (Publisher API kapatıldı), Kariyer.net / Yenibiris / Eleman.net (herkese açık API yok).
- Eklenen sağlayıcılar: Remotive, Jobicy, Arbeitnow (anahtarsız, önbellekli, atıflı) ve Careerjet v4 (tr_TR, ücretsiz anahtar).
- Uzaktan ilanlar Türkçe unvanlar için katalogdaki İngilizce unvanlarla eşleştirilir; "Uzaktan ilanları da göster" seçeneği eklendi.
- "Henüz bir ilan API'si yapılandırılmadı" uyarısı kaldırıldı; yerine sitelerin API durumunu açıklayan bilgi notu gösterilir.
