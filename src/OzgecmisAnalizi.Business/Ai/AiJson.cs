using System.Text.Json;

namespace OzgecmisAnalizi.Business.Ai;

public static class AiJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>Model yanıtındaki ilk JSON nesnesini (```json bloğu içinde olsa bile) çözer.</summary>
    public static T? TryParse<T>(string? text) where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class AiAnalysisResponse
{
    public string? Summary { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Improvements { get; set; }
    public Dictionary<string, List<string>>? SectionTips { get; set; }
}

public sealed class AiTailorResponse
{
    public string? Summary { get; set; }
    public List<string>? Suggestions { get; set; }
}

public static class AiPrompts
{
    public const string System =
        "Sen Türkiye iş piyasasını iyi bilen kıdemli bir insan kaynakları uzmanı ve kariyer koçusun. " +
        "Yanıtların Türkçe, somut, uygulanabilir ve nazik olsun. Uydurma bilgi verme; CV'de olmayan deneyimi varmış gibi yazma.";

    public static string Analysis(string resumeText, string? profession, int overall, IEnumerable<string> missingSkills) =>
        $$"""
        Aşağıdaki CV'yi {{(profession is null ? "genel olarak" : $"\"{profession}\" pozisyonu için")}} değerlendir.
        Kural motorunun verdiği genel puan: {{overall}}/100. Eksik görülen beceriler: {{string.Join(", ", missingSkills.Take(10))}}.

        Yalnızca şu JSON biçiminde yanıt ver:
        {
          "summary": "CV hakkında 3-4 cümlelik genel değerlendirme",
          "strengths": ["en fazla 4 güçlü yön"],
          "improvements": ["en fazla 6 öncelikli, somut iyileştirme önerisi"],
          "sectionTips": { "Özet": ["..."], "İş deneyimi": ["..."], "Beceriler ve diller": ["..."] }
        }

        CV METNİ:
        ---
        {{resumeText}}
        ---
        """;

    public static string Tailor(string resumeText, string jobDescription, IEnumerable<string> missingKeywords) =>
        $$"""
        Aşağıdaki CV'yi verilen iş ilanına uyarlamak için öneriler üret.
        İlanda geçip CV'de bulunmayan anahtar kelimeler: {{string.Join(", ", missingKeywords.Take(15))}}.
        Kişide olmayan deneyimi ekleme; yalnızca var olanı ilanın diliyle yeniden ifade et.

        Yalnızca şu JSON biçiminde yanıt ver:
        { "summary": "ilana uyarlanmış 3-4 cümlelik profesyonel özet", "suggestions": ["en fazla 6 somut öneri"] }

        İLAN:
        ---
        {{jobDescription}}
        ---
        CV:
        ---
        {{resumeText}}
        ---
        """;

    public static string CoverLetter(string resumeText, string jobTitle, string company, string? jobDescription, string tone, string? styleReference = null) =>
        $"""
        Aşağıdaki CV'ye dayanarak "{company}" şirketindeki "{jobTitle}" pozisyonu için Türkçe bir ön yazı (cover letter) yaz.
        Ton: {tone}. Uzunluk: 220-320 kelime, 4 paragraf; paragrafları boş satırla ayır. Selamlama ile başla, imza olarak CV'deki adı kullan.
        CV'de olmayan deneyim veya başarı uydurma. Yalnızca mektup metnini döndür.
        {(styleReference is null ? "" : "Kullanıcının daha önce yazdığı şu ön yazının üslubunu, cümle uzunluğunu ve hitap biçimini örnek al; içeriğini kopyalama:\n---\n" + styleReference + "\n---")}

        İLAN (varsa):
        ---
        {jobDescription ?? "Belirtilmedi"}
        ---
        CV:
        ---
        {resumeText}
        ---
        """;
}

public sealed class AiImproveResponse
{
    public string? Summary { get; set; }
    public List<AiImprovedExperience>? Experiences { get; set; }
}

public sealed class AiImprovedExperience
{
    public int Index { get; set; }
    public List<string>? Bullets { get; set; }
}

public static class AiImprovePrompt
{
    public static string Build(string resumeJson, string? profession, IEnumerable<string> improvements) =>
        $$"""
        Aşağıdaki CV bölümlerini {{(profession is null ? "genel" : $"\"{profession}\"")}} pozisyonu için iyileştir.
        Analiz önerileri: {{string.Join(" | ", improvements.Take(8))}}

        KURALLAR:
        - Kişinin yazmadığı hiçbir deneyimi, aracı, sayıyı, yüzdeyi veya başarıyı EKLEME. Sayı yoksa sayı uydurma.
        - Her maddeyi güçlü bir eylem fiiliyle başlat, tek fikir ve en fazla 2 satır olsun, birinci tekil zamir kullanma.
        - Özet 60-90 kelime, kalıp ifade (takım oyuncusu, dinamik vb.) içermesin.
        - Türkçe yaz; teknoloji adlarını olduğu gibi bırak.

        Yalnızca şu JSON biçiminde yanıt ver:
        { "summary": "...", "experiences": [ { "index": 0, "bullets": ["...", "..."] } ] }

        CV:
        {{resumeJson}}
        """;
}
