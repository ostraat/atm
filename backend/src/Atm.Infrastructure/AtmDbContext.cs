using Atm.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atm.Infrastructure;

public sealed class AtmDbContext : DbContext
{
    public AtmDbContext(DbContextOptions<AtmDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Kind);
        });

        modelBuilder.Entity<LedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => new { x.AccountId, x.CreatedAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.ResponseBody).IsRequired();
        });
    }
}
