using System.Globalization;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Translation;

/// <summary>PDF'te kullanılan sabit başlık ve ifadelerin dile göre karşılıkları.</summary>
public sealed record CvLabels(
    string Summary,
    string Experience,
    string Education,
    string Skills,
    string Languages,
    string Projects,
    string Certificates,
    string Contact,
    string Present,
    string NamePlaceholder,
    string Beginner,
    string Intermediate,
    string Good,
    string Advanced,
    string Native)
{
    public string Level(LanguageLevel level) => level switch
    {
        LanguageLevel.Baslangic => Beginner,
        LanguageLevel.Orta => Intermediate,
        LanguageLevel.Iyi => Good,
        LanguageLevel.Ileri => Advanced,
        LanguageLevel.AnaDil => Native,
        _ => level.ToString()
    };
}

/// <summary>CV çıktısı için desteklenen diller. "tr" kaynak/varsayılan dildir.</summary>
public static class CvLanguages
{
    public const string Default = "tr";

    private sealed record Entry(string Code, string Name, string NativeName, string Culture, CvLabels Labels);

    private static readonly Entry[] All =
    [
        new("tr", "Türkçe", "Türkçe", "tr-TR", new(
            "Profesyonel Özet", "İş Deneyimi", "Eğitim", "Beceriler", "Yabancı Dil", "Projeler", "Sertifikalar ve Kurslar", "İletişim", "Günümüz", "Ad Soyad",
            "Başlangıç", "Orta", "İyi", "İleri", "Ana dil")),
        new("en", "İngilizce", "English", "en-US", new(
            "Professional Summary", "Work Experience", "Education", "Skills", "Languages", "Projects", "Certifications & Courses", "Contact", "Present", "Full Name",
            "Beginner", "Intermediate", "Upper-Intermediate", "Advanced", "Native")),
        new("de", "Almanca", "Deutsch", "de-DE", new(
            "Profil", "Berufserfahrung", "Ausbildung", "Kenntnisse", "Sprachen", "Projekte", "Zertifikate und Kurse", "Kontakt", "heute", "Vor- und Nachname",
            "Grundkenntnisse", "Mittelstufe", "Gute Kenntnisse", "Verhandlungssicher", "Muttersprache")),
        new("fr", "Fransızca", "Français", "fr-FR", new(
            "Profil professionnel", "Expérience professionnelle", "Formation", "Compétences", "Langues", "Projets", "Certifications et formations", "Contact", "Aujourd'hui", "Prénom Nom",
            "Débutant", "Intermédiaire", "Bon niveau", "Courant", "Langue maternelle")),
        new("es", "İspanyolca", "Español", "es-ES", new(
            "Perfil profesional", "Experiencia laboral", "Formación", "Habilidades", "Idiomas", "Proyectos", "Certificaciones y cursos", "Contacto", "Actualidad", "Nombre y apellidos",
            "Básico", "Intermedio", "Bueno", "Avanzado", "Nativo")),
        new("it", "İtalyanca", "Italiano", "it-IT", new(
            "Profilo professionale", "Esperienza lavorativa", "Istruzione", "Competenze", "Lingue", "Progetti", "Certificazioni e corsi", "Contatti", "Oggi", "Nome e cognome",
            "Base", "Intermedio", "Buono", "Avanzato", "Madrelingua")),
        new("nl", "Felemenkçe", "Nederlands", "nl-NL", new(
            "Profiel", "Werkervaring", "Opleiding", "Vaardigheden", "Talen", "Projecten", "Certificaten en cursussen", "Contact", "heden", "Voor- en achternaam",
            "Beginner", "Gemiddeld", "Goed", "Gevorderd", "Moedertaal")),
        new("pt", "Portekizce", "Português", "pt-PT", new(
            "Resumo profissional", "Experiência profissional", "Formação académica", "Competências", "Idiomas", "Projetos", "Certificações e cursos", "Contacto", "Atual", "Nome completo",
            "Básico", "Intermédio", "Bom", "Avançado", "Nativo")),
        new("ru", "Rusça", "Русский", "ru-RU", new(
            "О себе", "Опыт работы", "Образование", "Навыки", "Языки", "Проекты", "Сертификаты и курсы", "Контакты", "по настоящее время", "Имя Фамилия",
            "Начальный", "Средний", "Хороший", "Продвинутый", "Родной"))
    ];

    private static readonly Dictionary<string, Entry> ByCode = All.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<TranslationLanguageDto> List() =>
        All.Select(e => new TranslationLanguageDto(e.Code, e.Name, e.NativeName)).ToList();

    public static bool IsSupported(string? code) => code is not null && ByCode.ContainsKey(code);

    /// <summary>Bilinmeyen ya da boş kodu varsayılan dile çevirir.</summary>
    public static string Normalize(string? code) =>
        code is not null && ByCode.TryGetValue(code.Trim(), out var e) ? e.Code : Default;

    public static CvLabels Labels(string? code) => ByCode[Normalize(code)].Labels;

    public static CultureInfo Culture(string? code) => CultureInfo.GetCultureInfo(ByCode[Normalize(code)].Culture);

    /// <summary>Çeviri istemlerinde kullanılan İngilizce dil adı.</summary>
    public static string EnglishName(string code) => CultureInfo.GetCultureInfo(ByCode[Normalize(code)].Culture).Parent.EnglishName;

    public static string DisplayName(string? code) => ByCode[Normalize(code)].Name;
}
