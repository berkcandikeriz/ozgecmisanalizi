using OzgecmisAnalizi.Core.Exceptions;

namespace OzgecmisAnalizi.Core.Tools;

/// <summary>Servis girişlerinde kısa doğrulama yardımcıları.</summary>
public static class Guard
{
    public static void Against(bool condition, string error)
    {
        if (condition)
        {
            throw new ValidationException(error);
        }
    }

    public static string Required(string? value, string fieldName, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} zorunludur.");
        }

        if (value.Length > maxLength)
        {
            throw new ValidationException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        }

        return value.Trim();
    }

    public static T Found<T>(T? entity, string name, object key) where T : class =>
        entity ?? throw new NotFoundException(name, key);
}
