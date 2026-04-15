namespace Contacts.Domain.Exceptions;

/// <summary>
/// Base class for domain exceptions.
/// Domain exceptions represent rule violations, not system errors.
/// They translate to 4xx HTTP responses, never 5xx.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class ContactNotFoundException : DomainException
{
    public ContactNotFoundException(Guid id)
        : base($"Contact with ID '{id}' was not found.") { }
}

public sealed class DuplicateEmailException : DomainException
{
    public DuplicateEmailException(string email)
        : base($"A contact with email '{email}' already exists.") { }
}
