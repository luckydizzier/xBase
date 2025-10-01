# EF Core Provider Cookbook

## Installation
```bash
dotnet add package XBase.EFCore --version 0.1.0-preview
```

## DbContext Setup
```csharp
public sealed class LedgerContext : DbContext
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseXBase("Data Source=/data/ledger;JournalMode=Sync");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("ledger");
            entity.HasKey(x => x.AccountNo);
            entity.Property(x => x.AccountNo).HasColumnName("account_no").HasCharEncoding("cp437");
            entity.Property(x => x.Balance).HasColumnName("balance").HasColumnType("currency");
            entity.HasIndex(x => x.Balance).HasDatabaseName("ledger_balance");
        });
    }
}
```

## Change Tracking Guidance
- Enable optimistic concurrency using `entity.Property(x => x.RowVersion).IsRowVersion()`; maps to hidden journal stamp.
- Use `context.Database.BeginTransaction()` to coordinate with external ADO.NET operations.
- Batch SaveChanges by grouping domain operations; provider pipelines flush indexes per transaction.

## LINQ Support Matrix
| Feature | Status | Notes |
|---------|--------|-------|
| Basic projections | ✅ | `Select`, `Where`, `OrderBy`, `Take/Skip` with index pushdown |
| Joins | ✅ | Nested-loop join with filtered index scans |
| GroupBy | ⚠️ | Server evaluates aggregate subset; fallback to client when unsupported |
| Contains/In-Memory Set | ✅ | Parameter expansion |
| Raw SQL | ✅ | `FromSql` + parameterization |

## Migration Workflow
1. Scaffold migration: `dotnet ef migrations add InitLedger`.
2. Inspect generated DDL; provider emits `.ddl` delta scripts.
3. Apply via CLI `xbase ddl apply --path Migrations` or `context.Database.Migrate()`.
4. Checkpoint: `xbase ddl checkpoint` to persist schema version and prune backlog.

## Troubleshooting
| Symptom | Resolution |
|---------|------------|
| Missing collation | Ensure `HasCharEncoding` matches CODEPAGES registry or register custom mapping. |
| `InvalidTagExpression` | Review index expressions for unsupported functions; cross-reference INDEXES.md. |
| Long-running migrations | Split large DDL into multiple migrations; leverage Online DDL staging. |

**End of ef-core-provider.md**
