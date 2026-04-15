namespace Contacts.Domain.Common;

/// <summary>
/// Strongly-typed ID using a record struct to prevent primitive obsession
/// and enforce type safety at compile time.
/// </summary>
public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.NewGuid());

    public static ContactId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ContactId cannot be empty.", nameof(value));

        return new ContactId(value);
    }

    public override string ToString() => Value.ToString();
}
