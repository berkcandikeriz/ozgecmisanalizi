using System.Text;
using OzgecmisAnalizi.Core.Extensions;

namespace OzgecmisAnalizi.Business.Analysis;

/// <summary>Metin içinde kelime sınırlarına dikkat ederek beceri arar ("java" ile "javascript" karışmaz).</summary>
public static class SkillMatcher
{
    /// <summary>Metni eşleştirme formuna getirir: katlanmış, noktalama boşluk, başında ve sonunda boşluk.</summary>
    public static string PrepareText(string? text)
    {
        var key = text.ToSearchKey();
        var sb = new StringBuilder(key.Length + 2);
        sb.Append(' ');
        for (var i = 0; i < key.Length; i++)
        {
            var ch = key[i];
            if (char.IsLetterOrDigit(ch) || ch is '#' or '+')
            {
                sb.Append(ch);
            }
            else if (ch == '/')
            {
                // "Java/Spring" iki ayrı kelime olarak da bulunabilsin; "ci/cd" anahtarı da aynı biçime dönüştüğü için eşleşir.
                sb.Append(" / ");
            }
            else if (ch is '.' or '-' or '&' && i + 1 < key.Length && char.IsLetterOrDigit(key[i + 1]) && (i == 0 || key[i - 1] != ' ' || ch == '.'))
            {
                // ".net", "node.js", "e-fatura", "s&op" korunur; cümle sonu noktası boşluğa döner.
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        sb.Append(' ');
        return CollapseSpaces(sb.ToString());
    }

    public static string NormalizeKey(string value) => PrepareText(value).Trim();

    public static bool Contains(string preparedText, string key) =>
        key.Length > 0 && preparedText.Contains(' ' + key + ' ', StringComparison.Ordinal);

    public static bool ContainsAny(string preparedText, IEnumerable<string> keys) => keys.Any(k => Contains(preparedText, k));

    private static string CollapseSpaces(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastSpace = false;
        foreach (var ch in value)
        {
            if (ch == ' ')
            {
                if (!lastSpace)
                {
                    sb.Append(ch);
                }

                lastSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastSpace = false;
            }
        }

        return sb.ToString();
    }
}
