namespace Contacts.Domain.ValueObjects;

/// <summary>
/// Email value object. Validates format on construction.
/// Immutable by design — changing email means creating a new instance.
/// </summary>
public sealed record Email
{
    private static readonly System.Text.RegularExpressions.Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty.", nameof(value));

        var normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > 254 || !EmailRegex.IsMatch(normalised))
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));

        return new Email(normalised);
    }

    public override string ToString() => Value;
}
