using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OzgecmisAnalizi.DataAccess.Configurations;

internal static class JsonConversionExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Karmaşık bir nesneyi nvarchar(max) JSON kolonu olarak saklar; değişiklik takibi içerik karşılaştırmasıyla yapılır.</summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder) where T : class, new()
    {
        var comparer = new ValueComparer<T>(
            (a, b) => Serialize(a) == Serialize(b),
            v => Serialize(v).GetHashCode(),
            v => Deserialize<T>(Serialize(v)));

        builder.HasConversion(v => Serialize(v), v => Deserialize<T>(v), comparer)
               .HasColumnType("nvarchar(max)");
        return builder;
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, Options);

    private static T Deserialize<T>(string? json) where T : class, new() =>
        string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, Options) ?? new T();
}
