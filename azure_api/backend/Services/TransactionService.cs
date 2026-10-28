using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Đã qua kiểm tra hình thức ở endpoint; service kiểm tra phần cần DB (sở hữu, lưu trữ, loại danh mục).</summary>
public sealed record TransactionInput(
    TransactionKind Kind, long WalletId, long? ToWalletId, long? CategoryId, decimal Amount, DateOnly OccurredOn, string? Note);

/// <summary>Bộ lọc đã chuẩn hoá: khoảng ngày luôn có (mặc định tháng hiện tại).</summary>
public sealed record TransactionFilter(DateOnly From, DateOnly To, long? WalletId, long? CategoryId, TransactionKind? Kind, string? Q);

/// <summary>Giao dịch thu / chi / chuyển tiền (docs/02-nghiep-vu.md §4).</summary>
public sealed class TransactionService(AppDbContext db)
{
    public const int MaxExportRows = 10_000;
    private const string NotFound = "Không tìm thấy giao dịch";

    private static readonly Expression<Func<Transaction, TransactionDto>> ToDto = t => new TransactionDto(
        t.Id, t.Kind, t.Amount, t.OccurredOn, t.Note,
        new WalletRef(t.Wallet.Id, t.Wallet.Name, t.Wallet.Color),
        t.ToWallet == null ? null : new WalletRef(t.ToWallet.Id, t.ToWallet.Name, t.ToWallet.Color),
        t.Category == null ? null : new CategoryRef(t.Category.Id, t.Category.Name, t.Category.Icon, t.Category.Color),
        t.CreatedAt);

    public async Task<PagedTransactions> ListAsync(Guid userId, TransactionFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var query = Filtered(userId, filter);

        var totalItems = await query.CountAsync(ct);
        var income = await query.Where(t => t.Kind == TransactionKind.Income).SumAsync(t => t.Amount, ct);
        var expense = await query.Where(t => t.Kind == TransactionKind.Expense).SumAsync(t => t.Amount, ct);
        var items = await Ordered(query).Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        return new PagedTransactions(items, page, pageSize, totalItems, totalPages, new MoneyTotals(income, expense));
    }

    public async Task<TransactionDto> GetAsync(Guid userId, long id, CancellationToken ct) =>
        await db.Transactions.AsNoTracking().Where(t => t.Id == id && t.UserId == userId).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw ApiProblemException.NotFound(NotFound);

    public async Task<TransactionDto> CreateAsync(Guid userId, TransactionInput input, CancellationToken ct)
    {
        await ValidateReferencesAsync(userId, input, existing: null, ct);
        var tx = new Transaction { UserId = userId };
        Apply(tx, input);
        db.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, tx.Id, ct);
    }

    public async Task<TransactionDto> UpdateAsync(Guid userId, long id, TransactionInput input, CancellationToken ct)
    {
        var tx = await db.Transactions.SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        await ValidateReferencesAsync(userId, input, tx, ct);
        Apply(tx, input);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, id, ct);
    }

    public async Task DeleteAsync(Guid userId, long id, CancellationToken ct)
    {
        var deleted = await db.Transactions.Where(t => t.Id == id && t.UserId == userId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw ApiProblemException.NotFound(NotFound);
    }

    /// <summary>CSV UTF-8 có BOM (Excel đọc đúng tiếng Việt), chống CSV injection (docs/02-nghiep-vu.md §7).</summary>
    public async Task<byte[]> ExportCsvAsync(Guid userId, TransactionFilter filter, CancellationToken ct)
    {
        var rows = await Ordered(Filtered(userId, filter)).Take(MaxExportRows).Select(ToDto).ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Ngày,Loại,Ví,Ví nhận,Danh mục,Số tiền,Ghi chú");
        foreach (var t in rows)
        {
            string[] cells =
            [
                t.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                KindLabel(t.Kind),
                Cell(t.Wallet.Name),
                Cell(t.ToWallet?.Name),
                Cell(t.Category?.Name),
                t.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                Cell(t.Note),
            ];
            csv.AppendLine(string.Join(',', cells));
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private IQueryable<Transaction> Filtered(Guid userId, TransactionFilter f)
    {
        var query = db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.OccurredOn >= f.From && t.OccurredOn <= f.To);
        if (f.WalletId is { } walletId) query = query.Where(t => t.WalletId == walletId || t.ToWalletId == walletId);
        if (f.CategoryId is { } categoryId) query = query.Where(t => t.CategoryId == categoryId);
        if (f.Kind is { } kind) query = query.Where(t => t.Kind == kind);
        if (f.Q is { } q) query = query.Where(t => t.Note != null && t.Note.Contains(q));
        return query;
    }

    // Cái nhập sau đứng trên trong cùng ngày
    private static IQueryable<Transaction> Ordered(IQueryable<Transaction> query) =>
        query.OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.Id);

    private static void Apply(Transaction tx, TransactionInput input)
    {
        tx.Kind = input.Kind;
        tx.WalletId = input.WalletId;
        tx.ToWalletId = input.Kind == TransactionKind.Transfer ? input.ToWalletId : null;
        tx.CategoryId = input.Kind == TransactionKind.Transfer ? null : input.CategoryId;
        tx.Amount = input.Amount;
        tx.OccurredOn = input.OccurredOn;
        tx.Note = input.Note;
    }

    /// <summary>
    /// Ví / danh mục phải của user (của người khác → báo "không tìm thấy", không lộ id tồn tại),
    /// chưa lưu trữ – trừ khi giao dịch đang sửa vốn đã trỏ tới đúng ví / danh mục đó.
    /// </summary>
    private async Task ValidateReferencesAsync(Guid userId, TransactionInput input, Transaction? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var walletIds = new[] { input.WalletId, input.ToWalletId ?? 0 }.Where(id => id > 0).Distinct().ToList();
        var wallets = await db.Wallets.AsNoTracking()
            .Where(w => w.UserId == userId && walletIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);

        CheckWallet("walletId", input.WalletId, existing?.WalletId);
        if (input.Kind == TransactionKind.Transfer) CheckWallet("toWalletId", input.ToWalletId!.Value, existing?.ToWalletId);

        if (input.Kind != TransactionKind.Transfer)
        {
            var category = await db.Categories.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == input.CategoryId && c.UserId == userId, ct);
            var expectedKind = input.Kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;
            if (category is null) errors["categoryId"] = ["Không tìm thấy danh mục"];
            else if (category.Kind != expectedKind)
                errors["categoryId"] = [expectedKind == CategoryKind.Income ? "Hãy chọn danh mục thu" : "Hãy chọn danh mục chi"];
            else if (category.IsArchived && category.Id != existing?.CategoryId) errors["categoryId"] = ["Danh mục đã lưu trữ"];
        }

        if (errors.Count > 0) throw ApiProblemException.Validation(errors);

        void CheckWallet(string field, long id, long? current)
        {
            if (!wallets.TryGetValue(id, out var wallet)) errors[field] = ["Không tìm thấy ví"];
            else if (wallet.IsArchived && id != current) errors[field] = ["Ví đã lưu trữ"];
        }
    }

    private static string KindLabel(TransactionKind kind) => kind switch
    {
        TransactionKind.Expense => "Chi",
        TransactionKind.Income => "Thu",
        _ => "Chuyển tiền",
    };

    /// <summary>Ô bắt đầu bằng = + - @ (công thức Excel) được thêm ' phía trước; có dấu phẩy / nháy / xuống dòng → bọc nháy kép.</summary>
    internal static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
