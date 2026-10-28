using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider time) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // MySQL không có kiểu thời gian kèm múi giờ → lưu UTC, đọc ra gán lại Kind = Utc
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<Guid>().HaveColumnType("char(36)");
        configurationBuilder.Properties<decimal>().HavePrecision(15, 2);
        // Provider Oracle đọc cột date ra DateTime → tự đổi qua lại DateOnly
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");

        configurationBuilder.Properties<WalletType>().HaveConversion<LowercaseEnumConverter<WalletType>>().HaveMaxLength(10);
        configurationBuilder.Properties<CategoryKind>().HaveConversion<LowercaseEnumConverter<CategoryKind>>().HaveMaxLength(10);
        configurationBuilder.Properties<TransactionKind>().HaveConversion<LowercaseEnumConverter<TransactionKind>>().HaveMaxLength(10);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));

    /// <summary>Enum ↔ chuỗi chữ thường ("ewallet", "transfer") – dễ đọc khi truy vấn tay, khớp CHECK constraint.</summary>
    private sealed class LowercaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
        v => v.ToString().ToLowerInvariant(),
        v => Enum.Parse<TEnum>(v, true))
        where TEnum : struct, Enum;
}
