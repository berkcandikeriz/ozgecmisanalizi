using System.Text.RegularExpressions;

namespace OzgecmisAnalizi.WebUI.Infrastructure;

public enum DiffKind
{
    Same,
    Added,
    Removed
}

public sealed record DiffSegment(DiffKind Kind, string Text);

/// <summary>
/// Kelime düzeyinde karşılaştırma (en uzun ortak alt dizi). Satır sonları ayrı birer belirteç olduğu için
/// madde yapısı korunur. Çok uzun metinlerde tümüyle "silindi / eklendi" olarak gösterilir.
/// </summary>
public static partial class TextDiff
{
    private const int MaxCells = 4_000_000;

    [GeneratedRegex(@"\n|[^\S\n]+|[^\s]+")]
    private static partial Regex Tokens();

    public static IReadOnlyList<DiffSegment> Words(string? before, string? after)
    {
        var a = Tokenize(before);
        var b = Tokenize(after);

        if (a.Count == 0) return b.Count == 0 ? [] : [new DiffSegment(DiffKind.Added, string.Concat(b))];
        if (b.Count == 0) return [new DiffSegment(DiffKind.Removed, string.Concat(a))];
        if ((long)a.Count * b.Count > MaxCells)
        {
            return [new DiffSegment(DiffKind.Removed, string.Concat(a)), new DiffSegment(DiffKind.Added, string.Concat(b))];
        }

        // LCS tablosu
        var lcs = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                lcs[i, j] = Same(a[i], b[j]) ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<DiffSegment>();
        int x = 0, y = 0;
        while (x < a.Count && y < b.Count)
        {
            if (Same(a[x], b[y]))
            {
                Push(result, DiffKind.Same, b[y]);
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                Push(result, DiffKind.Removed, a[x++]);
            }
            else
            {
                Push(result, DiffKind.Added, b[y++]);
            }
        }

        while (x < a.Count) Push(result, DiffKind.Removed, a[x++]);
        while (y < b.Count) Push(result, DiffKind.Added, b[y++]);
        return result;
    }

    /// <summary>Virgülle ayrılmış listeler (beceriler) için: korunan, eklenen ve çıkarılan öğeler.</summary>
    public static (IReadOnlyList<string> Kept, IReadOnlyList<string> Added, IReadOnlyList<string> Removed) Items(string? before, string? after)
    {
        static List<string> Split(string? v) => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var a = Split(before);
        var b = Split(after);
        var aKeys = a.Select(Key).ToHashSet();
        var bKeys = b.Select(Key).ToHashSet();
        return (b.Where(s => aKeys.Contains(Key(s))).ToList(), b.Where(s => !aKeys.Contains(Key(s))).ToList(), a.Where(s => !bKeys.Contains(Key(s))).ToList());
    }

    private static string Key(string s) => s.Trim().ToLowerInvariant();

    private static List<string> Tokenize(string? text) =>
        string.IsNullOrEmpty(text) ? [] : Tokens().Matches(text.Replace("\r", string.Empty)).Select(m => m.Value).ToList();

    // Boşluk farkları değişiklik sayılmaz.
    private static bool Same(string a, string b) =>
        a == b || (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b) && a.Contains('\n') == b.Contains('\n'));

    private static void Push(List<DiffSegment> list, DiffKind kind, string text)
    {
        if (list.Count > 0 && list[^1].Kind == kind)
        {
            list[^1] = list[^1] with { Text = list[^1].Text + text };
        }
        else
        {
            list.Add(new DiffSegment(kind, text));
        }
    }
}
