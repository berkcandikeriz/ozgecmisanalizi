# ÖzgeçmişAnalizi

CV'nizi ücretsiz analiz eden, düzenleyip PDF olarak indirmenizi sağlayan ve size uygun iş ilanlarını listeleyen platform.

- **CV analizi:** genel puan, kapsam / yapı / anlatım / ATS / beceri eşleşmesi alt puanları, bölüm bölüm öneriler
- **ATS kontrolleri:** iletişim bilgileri, standart başlıklar, tarih biçimi, uzunluk, simge yoğunluğu, anahtar kelimeler
- **68 meslek, 705 beceri:** eksik beceri tespiti ve öğrenme önerileri
- **Maaş tahmini:** deneyim seviyesi ve şehre göre net aylık aralık (2026 net asgari ücrete endeksli, yapılandırılabilir)
- **Kariyer yolu:** bulunduğunuz basamak ve sonraki basamak için odak becerileri
- **CV oluşturucu:** yüklenen PDF/DOCX'i (köprü bağlantıları dahil) düzenlenebilir CV'ye çevirir, 8 ATS uyumlu PDF şablonu
- **İlana göre uyarlama** ve ilana özel CV kopyasını başvuru panosuna bağlama
- **Ön yazı:** CV ile aynı şablonda PDF, eski ön yazının üslubunu örnek alma
- **CV danışmanı:** CV ve son analiz hakkında sohbet (yapay zeka yoksa analizden derlenen yanıtlar)
- **Başvuru takip panosu**, yaklaşan mülakatlar, "uygun değil" işaretleme, PDF analiz raporu
- **Ücretsiz ve sınırsız:** kota, deneme süresi veya kilitli özellik yoktur
- **İş önerileri:** anahtarsız çalışan Remotive, Jobicy, Arbeitnow (uzaktan ilanlar); ücretsiz anahtarla Careerjet (tr_TR) ve Jooble (Türkiye ilanları), isteğe bağlı JSearch + LinkedIn / Indeed / Kariyer.net / Yenibiris / Eleman.net arama bağlantıları
- **Açık ve koyu tema**, mobil uyumlu arayüz

## Mimari

```
src/
  OzgecmisAnalizi.Core        Ortak altyapı: global hata middleware'i, korelasyon kimliği, Serilog,
                              extension'lar (Türkçe metin katlama, slug), FileGuard, Guard, Result tipleri
  OzgecmisAnalizi.Entities    Varlıklar, enum'lar ve tüm istemcilerle paylaşılan DTO'lar (EF bağımlılığı yok)
  OzgecmisAnalizi.DataAccess  EF Core 10 code-first: AppDbContext, Fluent konfigürasyonlar, Migrations/,
                              generic repository + UnitOfWork, soft delete, meslek kataloğu seed'i
  OzgecmisAnalizi.Business    İş kuralları: CV ayrıştırma, analiz motoru, maaş, kariyer, PDF, iş arama, AI
  OzgecmisAnalizi.API         REST API (JWT, OpenAPI + Scalar, rate limit, CORS)
  OzgecmisAnalizi.WebUI       ASP.NET Core MVC arayüz — veriye yalnızca API üzerinden erişir
replica/                      Klon planlama dokümanları (recon, mimari, tasarım tokenları, özellik matrisi)
```

Bağımlılık yönü: `WebUI → Entities + Core` · `API → Business → DataAccess → Entities → Core`.
MVC arayüzü API'yi HTTP ile tükettiği için Angular, React veya Flutter istemcileri aynı API'ye bağlanabilir.

## Gereksinimler

- .NET 10 SDK
- SQL Server LocalDB (Visual Studio ile gelir) veya herhangi bir SQL Server

## Kurulum

```powershell
powershell -ExecutionPolicy Bypass -File .\kurulum.ps1     # derleme, migration, veritabanı, git
powershell -ExecutionPolicy Bypass -File .\calistir.ps1    # API + MVC'yi başlatır
```

Elle çalıştırmak için:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project src/OzgecmisAnalizi.DataAccess --startup-project src/OzgecmisAnalizi.API --output-dir Migrations
dotnet run --project src/OzgecmisAnalizi.API      # https://localhost:7101/scalar/v1
dotnet run --project src/OzgecmisAnalizi.WebUI    # https://localhost:7201
```

API ilk açılışta bekleyen migration'ları uygular ve meslek kataloğunu yükler.

## Yapılandırma (`src/OzgecmisAnalizi.API/appsettings.json`)

| Ayar | Açıklama |
| --- | --- |
| `ConnectionStrings:Default` | SQL Server bağlantısı |
| `Jwt:Key` | En az 32 karakter. Üretimde `Jwt__Key` ortam değişkeni veya user-secrets |
| `Ai:Provider` | `None` (kural motoru), `OpenAICompatible` veya `Gemini` |
| `Ai:BaseUrl`, `Ai:ApiKey`, `Ai:Model` | OpenAI uyumlu: OpenAI, Groq, OpenRouter veya yerel **Ollama** (`http://localhost:11434/v1`, anahtar gerekmez). Gemini: [AI Studio](https://aistudio.google.com/apikey) ücretsiz anahtar |
| `JobSearch:Careerjet:ApiKey` | [Careerjet](https://www.careerjet.com/partners/) ücretsiz yayıncı anahtarı — Türkiye (`tr_TR`) ilanları. `Referer`: hesapta kayıtlı site adresi |
| `JobSearch:Jooble:ApiKey` | [Jooble API](https://jooble.org/api/about) ücretsiz anahtar — Türkiye ilanları |
| `JobSearch:JSearch:ApiKey` | JSearch (RapidAPI) — Google for Jobs üzerinden ilanlar |
| `JobSearch:Remotive/Jobicy/Arbeitnow:Enabled` | Anahtarsız uzaktan ilan kaynakları (varsayılan açık). Kullanım koşulları gereği yanıtlar 2–6 saat önbellekte tutulur ve kaynak bağlantısı gösterilir |
| `JobSearch:IncludeRemoteByDefault` | İş aramasında uzaktan ilanlar varsayılan olarak eklensin mi |
| `JobSearch:SiteLinks` | Site arama bağlantı şablonları (`{q}`, `{l}`, `{qslug}`) |
| `Salary:MinimumNetWage` | Net asgari ücret (2026: 28.075,50 TL). Yıllık güncelleyin |

Gizli anahtarları depoya koymayın:

```powershell
cd src/OzgecmisAnalizi.API
dotnet user-secrets set "Ai:ApiKey" "..."
dotnet user-secrets set "JobSearch:Careerjet:ApiKey" "..."
dotnet user-secrets set "JobSearch:Jooble:ApiKey" "..."
```

## Notlar

- **İlan sitelerinin API durumu (Ekim 2026):** LinkedIn Job Postings API yalnızca onaylı iş ortaklarına (ATS) açıktır ve ilan *yayınlamak* içindir; Indeed Publisher (arama) API'si 2023'te yeni kayda kapatılıp 2024'te sonlandırıldı, kalanlar iş ortağı API'leridir; Kariyer.net, Yenibiris ve Eleman.net herkese açık bir geliştirici API'si yayımlamıyor. Bu sitelerin sayfaları kullanım koşulları gereği kazınmaz; aramayı kendi sonuç sayfalarında açan bağlantılar verilir. Bu sitelerdeki ilanların bir kısmı Careerjet ve Jooble gibi toplayıcılar üzerinden listelenir.
- Taranmış (görsel) PDF'lerden metin çıkarılamaz; OCR kapsam dışıdır.
- QuestPDF [Community lisansı](https://www.questpdf.com/license/) ile kullanılır.
- Maaş aralıkları piyasa araştırması değil, katsayıya dayalı tahminlerdir; katsayılar `Professions` tablosunda güncellenebilir.
