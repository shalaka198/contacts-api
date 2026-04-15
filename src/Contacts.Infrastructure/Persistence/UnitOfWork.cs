using Contacts.Application.Interfaces;

namespace Contacts.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> CommitAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
