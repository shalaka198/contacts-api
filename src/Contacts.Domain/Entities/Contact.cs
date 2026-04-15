using Contacts.Domain.Common;
using Contacts.Domain.Events;
using Contacts.Domain.ValueObjects;

namespace Contacts.Domain.Entities;

/// <summary>
/// Contact aggregate root.
/// Encapsulates all business logic. No public setters — mutation only via behaviour methods.
/// </summary>
public sealed class Contact : AggregateRoot
{
    // Private constructor prevents invalid state
    private Contact() { }

    public ContactId Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public PhoneNumber? Phone { get; private set; }
    public string? Organisation { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Factory method — the only way to create a valid Contact.
    /// </summary>
    public static Contact Create(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber? phone = null,
        string? organisation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentNullException.ThrowIfNull(email, nameof(email));

        var contact = new Contact
        {
            Id = ContactId.New(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            Phone = phone,
            Organisation = organisation?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        contact.RaiseDomainEvent(new ContactCreatedEvent(contact.Id, contact.Email));

        return contact;
    }

    /// <summary>
    /// Updates mutable contact details. Raises a domain event on change.
    /// </summary>
    public void Update(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber? phone = null,
        string? organisation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentNullException.ThrowIfNull(email, nameof(email));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        Phone = phone;
        Organisation = organisation?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new ContactUpdatedEvent(Id, Email));
    }

    /// <summary>
    /// Soft delete — data is retained for audit purposes.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new ContactDeletedEvent(Id));
    }
}
