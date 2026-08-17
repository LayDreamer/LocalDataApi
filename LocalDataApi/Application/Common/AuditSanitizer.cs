using System.Text.Json;

namespace LocalDataApi.Application.Common;

/// <summary>统一限制审计文本长度并清除常见凭据字段。</summary>
public static class AuditSanitizer
{
    private static readonly string[] SensitiveNames = ["password", "token", "secret", "authorization", "cookie", "salt", "hash"];

    public static string? Truncate(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maximumLength)];

    public static string? MaskMessage(string? value, int maximumLength = 1024)
    {
        var text = Truncate(value, maximumLength);
        return string.IsNullOrWhiteSpace(text) ? null : text.Replace("\r", " ").Replace("\n", " ");
    }

    public static string Serialize(IDictionary<string, object?> values)
    {
        var safe = values.ToDictionary(
            pair => pair.Key,
            pair => IsSensitive(pair.Key) ? "***" : SanitizeValue(pair.Value));
        return JsonSerializer.Serialize(safe);
    }

    public static bool IsSensitive(string name)
        => SensitiveNames.Any(sensitive => name.Contains(sensitive, StringComparison.OrdinalIgnoreCase));

    private static object? SanitizeValue(object? value)
        => value switch
        {
            null => null,
            string text => Truncate(text, 512),
            _ => value
        };
}
