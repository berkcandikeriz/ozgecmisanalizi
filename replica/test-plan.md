# Test plan

| ID | case | expected |
| --- | --- | --- |
| F01-H1 | Kayıt → PDF CV yükle → "Yükle ve analiz et" | Analiz sayfası, puan 0-100, ATS kontrolleri dolu |
| F01-E1 | Taranmış PDF | "okunabilir metin çıkarılamadı" mesajı, 422 |
| F01-E2 | 11 MB dosya | "10 MB sınırını aşıyor" |
| F01-E3 | .exe dosyası .pdf olarak adlandırılmış | "içerik uzantısıyla uyuşmuyor" |
| F01-N1 | Başka kullanıcının CV kimliği | 404 (sahiplik kontrolü) |
| F02-H1 | Editörde deneyim ekle/sil → kaydet → PDF (4 şablon) | PDF indirilir, Türkçe karakterler doğru |
| F02-E1 | 60 karakterlik ad, 30 deneyim | Taşma yok, PDF çok sayfalı |
| F03-H1 | İlan metni yapıştır | Eşleşen/eksik anahtar kelimeler |
| F04-H1 | API anahtarı yok | Uyarı + 5 site bağlantısı |
| F04-H2 | Jooble anahtarı var | İlanlar eşleşme puanına göre sıralı, "Takibe al" panoya ekler |
| F05-H1 | Aşama değiştir | Kart yeni sütunda, Başvuruldu'da AppliedAt dolar |
| F06-H1 | Ön yazı (AI yok) | Şablon mektup, CV'deki ad ve şirket |
| X-1 | Koyu tema + 390px genişlik | Yatay kaydırma yok, kontrast AA |
| X-2 | Giriş: 11 hatalı deneme / dk | 429 |
