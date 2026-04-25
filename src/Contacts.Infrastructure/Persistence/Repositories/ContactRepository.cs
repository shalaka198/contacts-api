using Microsoft.EntityFrameworkCore;
using Contacts.Application.Interfaces;
using Contacts.Domain.Common;
using Contacts.Domain.Entities;

namespace Contacts.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository : IContactRepository
{
    private readonly AppDbContext _context;

    public ContactRepository(AppDbContext context) => _context = context;

    public async Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken) =>
        await _context.Contacts
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Contact?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        await _context.Contacts
            .FirstOrDefaultAsync(
                c => EF.Functions.ILike(EF.Property<string>(c, "email"), email),
                cancellationToken);

    public async Task<(IReadOnlyList<Contact> Items, int TotalCount)> ListAsync(
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.Contacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, term) ||
                EF.Functions.ILike(c.LastName, term) ||
                EF.Functions.ILike(EF.Property<string>(c, "email"), term) ||
                (c.Organisation != null && EF.Functions.ILike(c.Organisation, term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Contact contact, CancellationToken cancellationToken) =>
        await _context.Contacts.AddAsync(contact, cancellationToken);

    public void Update(Contact contact) =>
        _context.Contacts.Update(contact);
}
