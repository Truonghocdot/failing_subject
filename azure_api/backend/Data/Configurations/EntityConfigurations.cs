using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

// Thiết kế bảng: docs/06-database.md §4. Thời gian luôn do app gán (UTC), không dựa vào default của DB.
// Xoá user → CASCADE dữ liệu của user; giữa các bảng con dùng RESTRICT (service kiểm tra trước, trả 409).

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(254);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(100);
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.ReplacedByHash).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.UserAgent).HasMaxLength(200);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.Property(x => x.Name).HasMaxLength(50);
        b.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        b.Property(x => x.Color).HasMaxLength(7).IsFixedLength();
        b.HasOne(x => x.User).WithMany(u => u.Wallets).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(50);
        b.HasIndex(x => new { x.UserId, x.Kind, x.Name }).IsUnique();
        b.Property(x => x.Icon).HasMaxLength(40);
        b.Property(x => x.Color).HasMaxLength(7).IsFixedLength();
        b.HasOne(x => x.User).WithMany(u => u.Categories).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_transactions_amount", "amount > 0");
            t.HasCheckConstraint("ck_transactions_shape",
                "(kind IN ('expense','income') AND category_id IS NOT NULL AND to_wallet_id IS NULL)"
                + " OR (kind = 'transfer' AND category_id IS NULL AND to_wallet_id IS NOT NULL AND to_wallet_id <> wallet_id)");
        });
        b.Property(x => x.Note).HasMaxLength(500);

        // Danh sách + báo cáo theo khoảng ngày; ngân sách / báo cáo theo danh mục
        b.HasIndex(x => new { x.UserId, x.OccurredOn, x.Id });
        b.HasIndex(x => new { x.CategoryId, x.OccurredOn });

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToWallet).WithMany().HasForeignKey(x => x.ToWalletId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_budgets_amount", "amount > 0"));
        b.HasIndex(x => new { x.UserId, x.Month, x.CategoryId }).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
