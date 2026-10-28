# Architecture: ÖzgeçmişAnalizi (JobStep'in temel özelliklerinin yeniden inşası)

Kullanıcının kendi stack tercihi uygulandı (replica-architect Step 1: "use what the user already knows").

## Stack

| layer | choice | why |
| --- | --- | --- |
| runtime | .NET 10 (LTS) | güncel LTS, tek dil |
| web UI | ASP.NET Core MVC 10 | kullanıcı isteği; API'yi HTTP ile tüketir, ileride Angular/React/Flutter eklenebilir |
| API | ASP.NET Core Web API + OpenAPI + Scalar | çoklu istemci |
| database | SQL Server (LocalDB geliştirme) | Windows geliştirici ortamı |
| ORM | EF Core 10, code-first, migration'lar repoda | kullanıcı isteği |
| auth | JWT (API), cookie (MVC) | stateless API |
| PDF | QuestPDF (Community lisans), PdfPig (okuma), OpenXml (DOCX) | açık kaynak |
| AI | OpenAI uyumlu (OpenAI, Groq, OpenRouter, Ollama) veya Gemini, isteğe bağlı | ücretsiz/yerel seçenekler; anahtar yoksa kural motoru |
| jobs | Jooble API, JSearch (RapidAPI) + site arama linkleri | resmi API'ler, kazıma yok |
| logging | Serilog (konsol + dosya) | |

## Katmanlar

```
OzgecmisAnalizi.Core        Ortak altyapı: hata yönetimi, middleware, loglama, extension'lar, araçlar, Result tipleri
OzgecmisAnalizi.Entities    Varlıklar, enum'lar, DTO'lar (EF bağımlılığı yok)
OzgecmisAnalizi.DataAccess  DbContext, Fluent konfigürasyonlar, migration'lar, repository + UnitOfWork, seed
OzgecmisAnalizi.Business    İş kuralları: ayrıştırma, analiz, maaş, kariyer, PDF, iş arama, AI
OzgecmisAnalizi.API         REST uç noktaları
OzgecmisAnalizi.WebUI       MVC arayüz (API istemcisi)
```

Bağımlılık yönü: WebUI → Entities(DTO) + Core ; API → Business → DataAccess → Entities → Core.

## Schema (EF Core code-first)

Tables: Users, Resumes (Content JSON kolon), ResumeAnalyses (JSON detay), Professions, ProfessionSkills,
JobApplications, CoverLetters. Erişim kuralı: her sorguda `UserId == currentUser` kontrolü servis katmanında.
Soft delete global query filter ile.

## API

| method path | does | who | flow |
| --- | --- | --- | --- |
| POST /api/auth/register, /login | hesap, JWT | anon | F01 |
| GET /api/auth/me | profil | user | |
| GET/POST/PUT/DELETE /api/resumes | CV CRUD | owner | F02 |
| POST /api/resumes/upload | dosyadan CV oluştur | owner | F01 |
| GET /api/resumes/{id}/pdf?template= | PDF indir | owner | F02 |
| POST /api/resumes/{id}/tailor | ilana göre uyarlama | owner | F03 |
| POST /api/analyses/resume/{resumeId} | analiz et | owner | F01 |
| GET /api/analyses/{id}, /{id}/report | sonuç, PDF rapor | owner | F01 |
| GET /api/professions | meslek kataloğu | anon | |
| GET /api/jobs/search | iş önerileri | user | F04 |
| GET/POST/PUT/PATCH/DELETE /api/applications | başvuru takip | owner | F05 |
| POST /api/cover-letters | ön yazı üret | owner | F06 |

## The parts that bite

- dosya yükleme: 10 MB sınır, yalnızca pdf/docx/txt, içerik türü + uzantı kontrolü
- taranmış PDF: metin çıkmazsa kullanıcıya açık hata (OCR kapsam dışı)
- Türkçe normalizasyon: İ/ı, ş, ğ eşleştirme için katlama
- dış API limitleri: iş sağlayıcıları zaman aşımı + hata yutma, linkler her zaman döner
- maaş: tahmini ve yapılandırılabilir; asgari ücret appsettings'te

## Build order

1. Vertical slice: kayıt → yükle → analiz → sonuç (S02, S04, S05)
2. Must: CV düzenleyici + PDF, iş önerileri
3. Should: uyarlama, ön yazı, başvuru takip, PDF rapor
