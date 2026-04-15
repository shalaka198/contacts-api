namespace Contacts.Application.Interfaces;

/// <summary>
/// Unit of Work pattern — coordinates persistence of multiple aggregates
/// within a single atomic transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
