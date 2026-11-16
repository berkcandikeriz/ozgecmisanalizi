using System.Globalization;
using OzgecmisAnalizi.Core.Extensions;

namespace OzgecmisAnalizi.Business.Translation;

/// <summary>
/// Çeviri motoru yokken kullanılan Türkçe → İngilizce sözlük. Yalnızca kısa ifadeleri (unvan, beceri, bölüm, dil adı) çevirir;
/// cümleleri çevirmeye çalışmaz. Türkçe ad tamlamaları İngilizce ile aynı sırada (niteleyen önde) olduğundan
/// "İnsan Kaynakları Uzmanı" → "Human Resources Specialist" gibi kelime kelime karşılık güvenlidir.
/// Bir ifadedeki kelimelerin tamamı bilinmiyorsa ifade olduğu gibi bırakılır.
/// </summary>
public static class CvGlossary
{
    private const int MaxWords = 5;

    private static readonly Dictionary<string, string> Phrases = Build(new()
    {
        // Diller
        ["İngilizce"] = "English", ["Türkçe"] = "Turkish", ["Almanca"] = "German", ["Fransızca"] = "French",
        ["İspanyolca"] = "Spanish", ["İtalyanca"] = "Italian", ["Rusça"] = "Russian", ["Arapça"] = "Arabic",
        ["Japonca"] = "Japanese", ["Çince"] = "Chinese", ["Korece"] = "Korean", ["Felemenkçe"] = "Dutch",
        ["Hollandaca"] = "Dutch", ["Portekizce"] = "Portuguese", ["Farsça"] = "Persian", ["Yunanca"] = "Greek",
        ["Azerbaycan Türkçesi"] = "Azerbaijani", ["Kürtçe"] = "Kurdish", ["Bulgarca"] = "Bulgarian", ["Lehçe"] = "Polish",

        // Dereceler
        ["Lisans"] = "Bachelor's Degree", ["Yüksek Lisans"] = "Master's Degree", ["Tezli Yüksek Lisans"] = "Master's Degree (Thesis)",
        ["Tezsiz Yüksek Lisans"] = "Master's Degree (Non-thesis)", ["Doktora"] = "PhD", ["Ön Lisans"] = "Associate Degree",
        ["Önlisans"] = "Associate Degree", ["Lise"] = "High School", ["Anadolu Lisesi"] = "Anatolian High School",
        ["Meslek Lisesi"] = "Vocational High School", ["Fen Lisesi"] = "Science High School", ["MBA"] = "MBA",
        ["Onur"] = "Honors", ["Yüksek Onur"] = "High Honors", ["Devam ediyor"] = "Ongoing", ["Mezun"] = "Graduated",

        // Bölümler
        ["İşletme"] = "Business Administration", ["İktisat"] = "Economics", ["Ekonomi"] = "Economics", ["Hukuk"] = "Law",
        ["Tıp"] = "Medicine", ["Hemşirelik"] = "Nursing", ["Eczacılık"] = "Pharmacy", ["Diş Hekimliği"] = "Dentistry",
        ["Psikoloji"] = "Psychology", ["Sosyoloji"] = "Sociology", ["Matematik"] = "Mathematics", ["Fizik"] = "Physics",
        ["Kimya"] = "Chemistry", ["Biyoloji"] = "Biology", ["İstatistik"] = "Statistics", ["Mimarlık"] = "Architecture",
        ["Uluslararası İlişkiler"] = "International Relations", ["Kamu Yönetimi"] = "Public Administration",
        ["Siyaset Bilimi ve Kamu Yönetimi"] = "Political Science and Public Administration",
        ["Elektrik-Elektronik Mühendisliği"] = "Electrical and Electronics Engineering",
        ["Elektrik Elektronik Mühendisliği"] = "Electrical and Electronics Engineering",
        ["Bilgisayar Programcılığı"] = "Computer Programming", ["Yönetim Bilişim Sistemleri"] = "Management Information Systems",
        ["Halkla İlişkiler"] = "Public Relations", ["İletişim Tasarımı"] = "Communication Design",
        ["Rehberlik ve Psikolojik Danışmanlık"] = "Guidance and Psychological Counseling", ["Sınıf Öğretmenliği"] = "Primary School Teaching",
        ["Dış Ticaret"] = "Foreign Trade", ["Turizm İşletmeciliği"] = "Tourism Management", ["Gastronomi ve Mutfak Sanatları"] = "Gastronomy and Culinary Arts",

        // Beceriler
        ["Takım Çalışması"] = "Teamwork", ["Ekip Çalışması"] = "Teamwork", ["Problem Çözme"] = "Problem Solving",
        ["Analitik Düşünme"] = "Analytical Thinking", ["Eleştirel Düşünme"] = "Critical Thinking", ["Zaman Yönetimi"] = "Time Management",
        ["İletişim Becerileri"] = "Communication Skills", ["Sunum Becerileri"] = "Presentation Skills", ["Liderlik"] = "Leadership",
        ["Müzakere"] = "Negotiation", ["İkna"] = "Persuasion", ["Satın Alma"] = "Procurement", ["Tedarik Zinciri"] = "Supply Chain",
        ["Tedarik Zinciri Yönetimi"] = "Supply Chain Management", ["Stok Yönetimi"] = "Inventory Management", ["Bordro"] = "Payroll",
        ["İşe Alım"] = "Recruitment", ["Ön Muhasebe"] = "Bookkeeping", ["Genel Muhasebe"] = "General Accounting",
        ["Maliyet Muhasebesi"] = "Cost Accounting", ["Bütçe Yönetimi"] = "Budget Management", ["Bütçeleme"] = "Budgeting",
        ["Stratejik Planlama"] = "Strategic Planning", ["Raporlama"] = "Reporting", ["Sosyal Medya Yönetimi"] = "Social Media Management",
        ["İçerik Üretimi"] = "Content Creation", ["Arama Motoru Optimizasyonu"] = "Search Engine Optimization",
        ["Nesne Yönelimli Programlama"] = "Object-Oriented Programming", ["Veri Yapıları"] = "Data Structures",
        ["Birim Testi"] = "Unit Testing", ["Sürüm Kontrolü"] = "Version Control", ["Makine Öğrenmesi"] = "Machine Learning",
        ["Derin Öğrenme"] = "Deep Learning", ["Yapay Zeka"] = "Artificial Intelligence", ["Veri Görselleştirme"] = "Data Visualization",
        ["İş Sağlığı ve Güvenliği"] = "Occupational Health and Safety", ["Ehliyet"] = "Driving License", ["B Sınıfı Ehliyet"] = "Class B Driving License",
        ["MS Office"] = "MS Office", ["Uzaktan"] = "Remote", ["Hibrit"] = "Hybrid", ["Yarı Zamanlı"] = "Part-time", ["Tam Zamanlı"] = "Full-time",
        ["Serbest Çalışan"] = "Freelance", ["Gönüllü"] = "Volunteer",
        ["Ön Uç"] = "Front-end", ["Arka Uç"] = "Back-end", ["Tam Yığın"] = "Full-stack", ["Full Stack"] = "Full-stack",
        ["İnsan Kaynakları"] = "Human Resources", ["Bilgi Teknolojileri"] = "Information Technology",

        // Yer adları
        ["İstanbul"] = "Istanbul", ["İzmir"] = "Izmir", ["Türkiye"] = "Türkiye", ["Muğla"] = "Mugla", ["Eskişehir"] = "Eskisehir",
        ["Diyarbakır"] = "Diyarbakir", ["Şanlıurfa"] = "Sanliurfa", ["Kırıkkale"] = "Kirikkale", ["Çanakkale"] = "Canakkale"
    });

    /// <summary>Kelime kelime çeviri için sözlük; ifadedeki tüm kelimeler burada olmalı.</summary>
    private static readonly Dictionary<string, string> Words = Build(new()
    {
        ["ve"] = "and", ["kıdemli"] = "Senior", ["uzman"] = "Specialist", ["uzmanı"] = "Specialist", ["yardımcısı"] = "Assistant",
        ["müdür"] = "Manager", ["müdürü"] = "Manager", ["yönetici"] = "Manager", ["yöneticisi"] = "Manager", ["şefi"] = "Chief",
        ["şef"] = "Chief", ["sorumlusu"] = "Officer", ["sorumlu"] = "Officer", ["asistan"] = "Assistant", ["asistanı"] = "Assistant",
        ["stajyer"] = "Intern", ["stajyeri"] = "Intern", ["stajı"] = "Internship", ["staj"] = "Internship", ["analist"] = "Analyst",
        ["analisti"] = "Analyst", ["danışman"] = "Consultant", ["danışmanı"] = "Consultant", ["temsilci"] = "Representative",
        ["temsilcisi"] = "Representative", ["koordinatör"] = "Coordinator", ["koordinatörü"] = "Coordinator", ["direktör"] = "Director",
        ["direktörü"] = "Director", ["lider"] = "Lead", ["lideri"] = "Lead", ["mühendis"] = "Engineer", ["mühendisi"] = "Engineer",
        ["mühendisliği"] = "Engineering", ["geliştirici"] = "Developer", ["geliştiricisi"] = "Developer", ["geliştirme"] = "Development",
        ["tasarımcı"] = "Designer", ["tasarımcısı"] = "Designer", ["tasarım"] = "Design", ["tasarımı"] = "Design", ["öğretmen"] = "Teacher",
        ["öğretmeni"] = "Teacher", ["eğitmen"] = "Trainer", ["eğitmeni"] = "Trainer", ["hemşire"] = "Nurse", ["muhasebeci"] = "Accountant",
        ["muhasebe"] = "Accounting", ["finans"] = "Finance", ["finansal"] = "Financial", ["satış"] = "Sales", ["pazarlama"] = "Marketing",
        ["insan"] = "Human", ["kaynakları"] = "Resources", ["proje"] = "Project", ["projesi"] = "Project", ["ürün"] = "Product",
        ["veri"] = "Data", ["bilimci"] = "Scientist", ["bilimi"] = "Science", ["bilgisayar"] = "Computer", ["yazılım"] = "Software",
        ["yazılımı"] = "Software", ["endüstri"] = "Industrial", ["makine"] = "Mechanical", ["makina"] = "Mechanical", ["inşaat"] = "Civil",
        ["elektrik"] = "Electrical", ["elektronik"] = "Electronics", ["kimya"] = "Chemical", ["gıda"] = "Food", ["çevre"] = "Environmental",
        ["harita"] = "Geomatics", ["mekatronik"] = "Mechatronics", ["biyomedikal"] = "Biomedical", ["müşteri"] = "Customer",
        ["hizmetleri"] = "Services", ["hizmet"] = "Service", ["ilişkileri"] = "Relations", ["yönetimi"] = "Management", ["yönetim"] = "Management",
        ["grafik"] = "Graphic", ["operasyon"] = "Operations", ["operasyonları"] = "Operations", ["lojistik"] = "Logistics", ["kalite"] = "Quality",
        ["kontrol"] = "Control", ["güvence"] = "Assurance", ["test"] = "Test", ["sistem"] = "Systems", ["sistemleri"] = "Systems",
        ["ağ"] = "Network", ["destek"] = "Support", ["teknik"] = "Technical", ["iş"] = "Business", ["araştırma"] = "Research",
        ["ekip"] = "Team", ["kurumsal"] = "Corporate", ["iletişim"] = "Communication", ["dijital"] = "Digital", ["içerik"] = "Content",
        ["sosyal"] = "Social", ["medya"] = "Media", ["üretim"] = "Production", ["planlama"] = "Planning", ["bakım"] = "Maintenance",
        ["mağaza"] = "Store", ["bölge"] = "Regional", ["genel"] = "General", ["ofis"] = "Office", ["idari"] = "Administrative",
        ["işler"] = "Affairs", ["bilgi"] = "Information", ["teknolojileri"] = "Technologies", ["teknolojisi"] = "Technology",
        ["güvenliği"] = "Security", ["siber"] = "Cyber", ["bulut"] = "Cloud", ["mobil"] = "Mobile", ["web"] = "Web", ["oyun"] = "Game",
        ["arayüz"] = "Interface", ["kullanıcı"] = "User", ["deneyimi"] = "Experience",  ["veritabanı"] = "Database", ["yöneticiliği"] = "Management",
        ["e-ticaret"] = "E-commerce", ["dış"] = "Foreign", ["ticaret"] = "Trade", ["ithalat"] = "Import", ["ihracat"] = "Export",
        ["tedarik"] = "Supply", ["zinciri"] = "Chain", ["depo"] = "Warehouse", ["sevkiyat"] = "Shipping", ["çağrı"] = "Call",
        ["merkezi"] = "Center", ["halkla"] = "Public", ["kamu"] = "Public", ["hukuk"] = "Legal", ["avukat"] = "Lawyer",
        ["eczacı"] = "Pharmacist", ["doktor"] = "Doctor", ["fizyoterapist"] = "Physiotherapist", ["diyetisyen"] = "Dietitian",
        ["psikolog"] = "Psychologist", ["aşçı"] = "Cook", ["aşçıbaşı"] = "Head Chef", ["garson"] = "Waiter", ["kasiyer"] = "Cashier",
        ["muhasebesi"] = "Accounting", ["vergi"] = "Tax", ["denetim"] = "Audit", ["denetçi"] = "Auditor", ["denetçisi"] = "Auditor",
        ["iç"] = "Internal", ["risk"] = "Risk", ["uyum"] = "Compliance", ["bankacılık"] = "Banking", ["sigorta"] = "Insurance",
        ["yatırım"] = "Investment", ["portföy"] = "Portfolio", ["eğitim"] = "Training", ["öğrenme"] = "Learning", ["programı"] = "Program",
        ["programcı"] = "Programmer", ["programcısı"] = "Programmer", ["otomasyon"] = "Automation", ["robotik"] = "Robotics",
        ["süreç"] = "Process", ["iyileştirme"] = "Improvement", ["stratejisi"] = "Strategy", ["strateji"] = "Strategy"
    });

    private static Dictionary<string, string> Build(Dictionary<string, string> source)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (tr, en) in source)
        {
            map[tr.ToSearchKey()] = en;
        }

        return map;
    }

    /// <summary>Türkçe kısa ifadenin İngilizce karşılığını bulur; bulamazsa false.</summary>
    public static bool TryTranslateToEnglish(string text, out string translated)
    {
        translated = text;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (Phrases.TryGetValue(trimmed.ToSearchKey(), out var phrase))
        {
            translated = phrase;
            return true;
        }

        var words = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxWords)
        {
            return false;
        }

        // Soldan sağa en uzun eşleşme: önce 3, sonra 2 kelimelik sözlük ifadeleri, en son tek kelime.
        var output = new List<string>(words.Length);
        for (var i = 0; i < words.Length;)
        {
            var matched = false;
            for (var len = Math.Min(3, words.Length - i); len >= 1 && !matched; len--)
            {
                var key = string.Join(' ', words.Skip(i).Take(len)).Trim(',', ';', '(', ')').ToSearchKey();
                if (Phrases.TryGetValue(key, out var value) || (len == 1 && Words.TryGetValue(key, out value)))
                {
                    output.Add(value);
                    i += len;
                    matched = true;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        // Aynı İngilizce kelimenin art arda tekrarını (örn. "Yazılım Geliştirme Geliştiricisi") sadeleştir.
        var result = string.Join(' ', output.Where((w, i) => i == 0 || !string.Equals(w, output[i - 1], StringComparison.OrdinalIgnoreCase)));
        translated = CultureInfo.InvariantCulture.TextInfo.ToUpper(result[0]) + result[1..];
        return true;
    }
}
