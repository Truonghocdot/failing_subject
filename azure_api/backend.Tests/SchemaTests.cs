using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

/// <summary>Ràng buộc ở tầng DB (docs/06-database.md §4) – lưới an toàn cuối nếu service bỏ sót kiểm tra.</summary>
public class SchemaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(Guid UserId, long WalletId, long ExpenseCategoryId)> SeedAsync()
    {
        var user = await factory.CreateUserAsync();
        var id = user.Auth.User.Id;
        return await factory.WithDbAsync(async db => (
            id,
            await db.Wallets.Where(w => w.UserId == id).Select(w => w.Id).SingleAsync(),
            await db.Categories.Where(c => c.UserId == id && c.Kind == CategoryKind.Expense).Select(c => c.Id).FirstAsync()));
    }

    [Fact]
    public async Task Enums_AreStoredAsLowercaseText()
    {
        var (userId, _, _) = await SeedAsync();

        var types = await factory.WithDbAsync(db => db.Database
            .SqlQuery<string>($"SELECT type AS Value FROM wallets WHERE user_id = {userId.ToString()}")
            .ToListAsync());

        Assert.Equal(["cash"], types);
    }

    [Fact]
    public async Task ValidTransaction_IsSaved_WithDecimalPrecision()
    {
        var (userId, walletId, categoryId) = await SeedAsync();

        var saved = await factory.WithDbAsync(async db =>
        {
            var tx = new Transaction
            {
                UserId = userId, Kind = TransactionKind.Expense, WalletId = walletId, CategoryId = categoryId,
                Amount = 45_000.50m, OccurredOn = new DateOnly(2026, 10, 28), Note = "Phở bò",
            };
            db.Transactions.Add(tx);
            await db.SaveChangesAsync();
            return await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == tx.Id);
        });

        Assert.Equal(45_000.50m, saved.Amount);
        Assert.Equal(new DateOnly(2026, 10, 28), saved.OccurredOn);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
    }

    public static TheoryData<string, decimal, bool, bool> InvalidShapes => new()
    {
        // kind, amount, hasCategory, transferToSameWallet
        { "amount = 0", 0m, true, false },
        { "amount âm", -1m, true, false },
        { "expense thiếu danh mục", 10m, false, false },
        { "transfer về chính ví nguồn", 10m, false, true },
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public async Task InvalidTransaction_IsRejectedByCheckConstraint(string _, decimal amount, bool hasCategory, bool selfTransfer)
    {
        var (userId, walletId, categoryId) = await SeedAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Transactions.Add(new Transaction
            {
                UserId = userId,
                Kind = selfTransfer ? TransactionKind.Transfer : TransactionKind.Expense,
                WalletId = walletId,
                ToWalletId = selfTransfer ? walletId : null,
                CategoryId = hasCategory ? categoryId : null,
                Amount = amount,
                OccurredOn = new DateOnly(2026, 10, 28),
            });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task WalletName_IsUniquePerUser_IgnoringCase()
    {
        var (userId, _, _) = await SeedAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Wallets.Add(new Wallet { UserId = userId, Name = "TIỀN MẶT", Type = WalletType.Cash, Color = "#16a34a" });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task DeletingWalletInUse_IsRestricted()
    {
        var (userId, walletId, categoryId) = await SeedAsync();
        await factory.WithDbAsync(async db =>
        {
            db.Transactions.Add(new Transaction
            {
                UserId = userId, Kind = TransactionKind.Expense, WalletId = walletId, CategoryId = categoryId,
                Amount = 1m, OccurredOn = new DateOnly(2026, 10, 1),
            });
            return await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Wallets.Remove(await db.Wallets.SingleAsync(w => w.Id == walletId));
            return await db.SaveChangesAsync();
        }));
    }
}
