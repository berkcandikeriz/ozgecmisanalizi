# Recon map: JobStep (web)

Scope: CV puanlama + ATS okunabilirlik, CV oluşturucu ve PDF dışa aktarma, ilana göre CV uyarlama,
iş önerileri (eşleşme puanı ile), başvuru takip panosu, ön yazı üretimi.
For: Türkiye'deki iş arayanlar; ücretsiz, kendi sunucunda çalışan bir platform (ÖzgeçmişAnalizi).
Date: 2026-10-28

> Clean-room: Yalnızca herkese açık pazarlama sayfaları okundu. Kod, görsel, logo ve metin kopyalanmadı.

## Sources

| # | source | URL | notes |
| --- | --- | --- | --- |
| 1 | marketing site | https://www.jobstep.io/ | 4 adımlı akış, özellik listesi, fiyat kademeleri, SSS |
| 2 | ATS checker | https://www.jobstep.io/en/resume/ats-checker | okunabilirlik, yapı, içerik, isteğe bağlı ilan eşleşmesi |
| 3 | application tracker | https://www.jobstep.io/en/job-application/job-application-tracker | aşamalar, alanlar, aksiyonlar |
| 4 | AI job search | https://www.jobstep.io/en/job-application/ai-job-search | tercihler, eşleşme puanı, kayıt |

## Core loop

CV'yi yükle → puan ve bölüm bölüm öneriler al → CV'yi düzenleyip PDF indir → uygun ilanları gör ve başvuruyu takip et.

## Screens

| ID | screen | route / how to reach | purpose | key components | states seen |
| --- | --- | --- | --- | --- | --- |
| S01 | Landing | / | değer önerisi, adımlar, özellikler, şablonlar, SSS | hero, step cards, feature grid, accordion | filled |
| S02 | Giriş / Kayıt | /hesap/giris, /hesap/kayit | hesap | form, input, button | empty, error |
| S03 | Panel | /panel | CV'ler, son analizler, başvurular özeti | stat tiles, list | empty, filled |
| S04 | CV yükle | /cv/yukle | PDF/DOCX yükleme | dropzone, select | empty, uploading, error |
| S05 | Analiz sonucu | /analiz/{id} | genel puan, alt puanlar, öneriler, beceri boşluğu, maaş, kariyer yolu | score ring, meters, chips, timeline | filled |
| S06 | CV düzenleyici | /cv/{id}/duzenle | yapılandırılmış CV düzenleme, şablon seçimi | repeatable sections, template picker | empty, filled |
| S07 | İlana göre uyarla | /cv/{id}/uyarla | ilan metni ile anahtar kelime karşılaştırma | textarea, keyword chips | empty, filled |
| S08 | İş önerileri | /isler | CV'ye göre ilanlar ve site aramaları | job cards, match badge, filters | empty, filled, no-provider |
| S09 | Başvuru takip | /basvurular | aşamalı pano | kanban columns, cards | empty, filled |
| S10 | Ön yazı | /on-yazi | ilan + CV'den ön yazı | form, editor | empty, filled |
| S11 | Meslekler | /meslekler | 56+ meslek, beceri ve maaş aralıkları | grid, search | filled |

## Flows

```
F01 CV'yi analiz et
    S01 -> S02 -> S04 -> S05
    happy path clicks: 4
    edge: taranmış (görsel) PDF, 10 MB üstü dosya, Türkçe karakter, boş metin

F02 CV oluştur / düzenle ve indir
    S05 -> S06 -> PDF indir
    happy path clicks: 3
    edge: çok uzun deneyim listesi, boş bölümler

F03 İlana göre uyarla
    S06 -> S07 -> eksik anahtar kelimeleri ekle -> PDF
F04 Uygun işleri bul ve takibe al
    S05 -> S08 -> "Takibe al" -> S09
    edge: API anahtarı yok (yalnızca site arama linkleri), sonuç yok
F05 Başvuruyu aşamalar arasında taşı
    S09 kart -> aşama değiştir
F06 Ön yazı üret
    S08/S09 -> S10
```

## Components

| component | variants | states | used on |
| --- | --- | --- | --- |
| Button | primary, secondary, ghost, danger | default, hover, focus, disabled, loading | all |
| Input/Textarea/Select | — | default, focus, error, disabled | S02, S04, S06, S07, S10 |
| Card | default, interactive | hover | all |
| Score ring | — | 0-100 renk eşikleri | S05 |
| Meter | — | low/mid/high | S05 |
| Chip | neutral, success, danger | — | S05, S07, S08 |
| Kanban column | per stage | empty, filled | S09 |
| Theme toggle | light, dark, system | — | header |

## Inferred data model

```
User           id, email, full_name, password_hash, role                      confidence: high
Resume         id, user_id, title, template, content(json: personal, summary, experiences,
               educations, skills, languages, certificates, projects)          evidence: S2 "editable version"; high
ResumeAnalysis id, resume_id, overall, comprehensiveness, structure, clarity, ats,
               label, percentile, suggestions(json), skills found/missing       evidence: ATS checker page; medium
JobApplication id, user_id, title, company, url, stage, match_score, interview_at, notes, resume_id
               evidence: tracker page; high
CoverLetter    id, user_id, resume_id, job_title, company, content               evidence: cover letter page; medium
Profession     id, code, name, category, salary factors, skills[], career steps (bizim eklememiz)
```

## Feature matrix

See `features.csv`.

## Out of scope

- JobStep'in kendi ilan veritabanı ve ortaklıkları (ağ/veri onların).
- LinkedIn/Indeed sayfalarının kazınması (kullanım şartlarına aykırı). Bunun yerine resmi API'ler (Jooble, JSearch) ve arama linkleri.
- Stripe ödeme (platform ücretsiz).

## Size

Screens 11, flows 6, entities 6. Hard parts: CV metninden yapı çıkarma, puanlama tutarlılığı, iş ilanı kaynakları. Size: M.
