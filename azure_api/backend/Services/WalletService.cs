using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed record WalletInput(string Name, WalletType Type, decimal OpeningBalance, string Color, bool IsArchived);

/// <summary>Ví + số dư tính từ giao dịch (docs/02-nghiep-vu.md §2).</summary>
public sealed class WalletService(AppDbContext db)
{
    private const string NotFound = "Không tìm thấy ví";

    public async Task<WalletListDto> ListAsync(Guid userId, bool includeArchived, CancellationToken ct)
    {
        var wallets = await db.Wallets.AsNoTracking()
            .Where(w => w.UserId == userId && (includeArchived || !w.IsArchived))
            .OrderBy(w => w.IsArchived).ThenBy(w => w.SortOrder).ThenBy(w => w.Id)
            .ToListAsync(ct);
        var balances = await BalancesAsync(userId, ct);

        var items = wallets.Select(w => ToDto(w, balances)).ToList();
        return new WalletListDto(items, items.Where(w => !w.IsArchived).Sum(w => w.Balance));
    }

    public async Task<WalletDto> GetAsync(Guid userId, long id, CancellationToken ct)
    {
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        return ToDto(wallet, await BalancesAsync(userId, ct));
    }

    public async Task<WalletDto> CreateAsync(Guid userId, WalletInput input, CancellationToken ct)
    {
        await EnsureUniqueNameAsync(userId, input.Name, exceptId: null, ct);
        var maxOrder = await db.Wallets.Where(w => w.UserId == userId).MaxAsync(w => (int?)w.SortOrder, ct) ?? -1;

        var wallet = new Wallet
        {
            UserId = userId,
            Name = input.Name,
            Type = input.Type,
            OpeningBalance = input.OpeningBalance,
            Color = input.Color,
            SortOrder = maxOrder + 1,
        };
        db.Wallets.Add(wallet);
        await SaveAsync(ct);
        return await GetAsync(userId, wallet.Id, ct);
    }

    public async Task<WalletDto> UpdateAsync(Guid userId, long id, WalletInput input, CancellationToken ct)
    {
        var wallet = await db.Wallets.SingleOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        await EnsureUniqueNameAsync(userId, input.Name, exceptId: id, ct);
        if (input.IsArchived && !wallet.IsArchived) await EnsureNotLastActiveAsync(userId, id, "lưu trữ", ct);

        wallet.Name = input.Name;
        wallet.Type = input.Type;
        wallet.OpeningBalance = input.OpeningBalance;
        wallet.Color = input.Color;
        wallet.IsArchived = input.IsArchived;
        await SaveAsync(ct);
        return await GetAsync(userId, id, ct);
    }

    public async Task DeleteAsync(Guid userId, long id, CancellationToken ct)
    {
        var wallet = await db.Wallets.SingleOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        if (await db.Transactions.AnyAsync(t => t.WalletId == id || t.ToWalletId == id, ct))
            throw ApiProblemException.Conflict("Ví đã có giao dịch – hãy lưu trữ thay vì xoá");
        if (!wallet.IsArchived) await EnsureNotLastActiveAsync(userId, id, "xoá", ct);

        db.Wallets.Remove(wallet);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Số dư = số dư đầu + thu − chi − chuyển đi + nhận về. 2 truy vấn GROUP BY phía DB (nguồn + đích) rồi ghép.
    /// </summary>
    public async Task<Dictionary<long, decimal>> BalancesAsync(Guid userId, CancellationToken ct)
    {
        var fromSource = await db.Transactions
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.WalletId)
            .Select(g => new { WalletId = g.Key, Delta = g.Sum(t => t.Kind == TransactionKind.Income ? t.Amount : -t.Amount) })
            .ToListAsync(ct);
        var received = await db.Transactions
            .Where(t => t.UserId == userId && t.ToWalletId != null)
            .GroupBy(t => t.ToWalletId!.Value)
            .Select(g => new { WalletId = g.Key, Delta = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var deltas = fromSource.ToDictionary(x => x.WalletId, x => x.Delta);
        foreach (var r in received) deltas[r.WalletId] = deltas.GetValueOrDefault(r.WalletId) + r.Delta;
        return deltas;
    }

    private static WalletDto ToDto(Wallet w, Dictionary<long, decimal> deltas) =>
        new(w.Id, w.Name, w.Type, w.OpeningBalance, w.OpeningBalance + deltas.GetValueOrDefault(w.Id), w.Color, w.IsArchived, w.SortOrder);

    // collation utf8mb4_unicode_ci → so sánh không phân biệt hoa thường ngay trong SQL
    private async Task EnsureUniqueNameAsync(Guid userId, string name, long? exceptId, CancellationToken ct)
    {
        if (await db.Wallets.AnyAsync(w => w.UserId == userId && w.Name == name && w.Id != exceptId, ct))
            throw ApiProblemException.Conflict("Tên ví đã tồn tại", "name");
    }

    private async Task EnsureNotLastActiveAsync(Guid userId, long id, string action, CancellationToken ct)
    {
        if (!await db.Wallets.AnyAsync(w => w.UserId == userId && w.Id != id && !w.IsArchived, ct))
            throw ApiProblemException.Conflict($"Không thể {action} ví cuối cùng đang dùng");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 2 request cùng tên đồng thời → unique index chặn
            throw ApiProblemException.Conflict("Tên ví đã tồn tại", "name");
        }
    }
}
