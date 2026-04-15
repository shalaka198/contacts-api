using MediatR;
using Microsoft.EntityFrameworkCore;
using Contacts.Domain.Entities;

namespace Contacts.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    private readonly IPublisher _publisher;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect and dispatch domain events before/after save
        var aggregates = ChangeTracker
            .Entries()
            .Where(e => e.Entity is Domain.Common.AggregateRoot)
            .Select(e => (Domain.Common.AggregateRoot)e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch events AFTER successful persistence
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                await _publisher.Publish(domainEvent, cancellationToken);

            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
