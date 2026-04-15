namespace Contacts.Domain.ValueObjects;

/// <summary>
/// Phone number value object. E.164 format enforced.
/// Null/empty is valid as phone is optional.
/// </summary>
public sealed record PhoneNumber
{
    private static readonly System.Text.RegularExpressions.Regex E164Regex =
        new(@"^\+[1-9]\d{6,14}$",
            System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

    public string? Value { get; }

    private PhoneNumber(string? value) => Value = value;

    public static PhoneNumber None() => new(null);

    public static PhoneNumber From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return None();

        var normalised = value.Trim();
        if (!E164Regex.IsMatch(normalised))
            throw new ArgumentException(
                $"'{value}' is not a valid E.164 phone number (e.g. +6421234567).", nameof(value));

        return new PhoneNumber(normalised);
    }

    public override string ToString() => Value ?? string.Empty;
}
