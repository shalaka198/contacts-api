using Contacts.Domain.Common;
using Contacts.Domain.Entities;

namespace Contacts.Application.Interfaces;

/// <summary>
/// Repository interface for the Contact aggregate.
/// Follows the Repository pattern — abstracts persistence from domain logic.
/// Only methods the domain actually needs are exposed here (ISP).
/// </summary>
public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken = default);
    Task<Contact?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Contact> Items, int TotalCount)> ListAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);
    void Update(Contact contact);
}
