namespace OzgecmisAnalizi.Core.Extensions;

public static class CollectionExtensions
{
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source) => source is null || !source.Any();

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
        source.Where(x => x is not null)!;

    /// <summary>Anahtar katlamasıyla (Türkçe duyarsız) tekrar edenleri kaldırır.</summary>
    public static List<string> DistinctBySearchKey(this IEnumerable<string> source) =>
        source.Where(s => s.HasValue())
              .GroupBy(s => s.ToSearchKey())
              .Select(g => g.First().Trim())
              .ToList();
}
