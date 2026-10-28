using System.Globalization;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Ngân sách theo (tháng, danh mục chi) – docs/02-nghiep-vu.md §5. Month luôn là ngày 01.</summary>
public sealed class BudgetService(AppDbContext db)
{
    private const string NotFound = "Không tìm thấy ngân sách";

    public async Task<IReadOnlyList<BudgetDto>> ListAsync(Guid userId, DateOnly month, CancellationToken ct)
    {
        var budgets = await db.Budgets.AsNoTracking()
            .Where(b => b.UserId == userId && b.Month == month)
            .OrderBy(b => b.Category.SortOrder).ThenBy(b => b.Category.Name)
            .Select(b => new { b.Id, b.Amount, Category = new CategoryRef(b.Category.Id, b.Category.Name, b.Category.Icon, b.Category.Color) })
            .ToListAsync(ct);
        if (budgets.Count == 0) return [];

        var spent = await SpentAsync(userId, month, budgets.Select(b => b.Category.Id).ToList(), ct);
        return budgets.Select(b => ToDto(b.Id, month, b.Category, b.Amount, spent.GetValueOrDefault(b.Category.Id))).ToList();
    }

    /// <summary>Mỗi (user, tháng, danh mục) tối đa 1 ngân sách → có rồi thì sửa số tiền.</summary>
    public async Task<BudgetDto> UpsertAsync(Guid userId, DateOnly month, long categoryId, decimal amount, CancellationToken ct)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(b => b.UserId == userId && b.Month == month && b.CategoryId == categoryId, ct);
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);

        string? error = null;
        if (category is null) error = "Không tìm thấy danh mục";
        else if (category.Kind != CategoryKind.Expense) error = "Hãy chọn danh mục chi";
        else if (category.IsArchived && budget is null) error = "Danh mục đã lưu trữ";
        if (error is not null) throw ApiProblemException.Validation(new Dictionary<string, string[]> { ["categoryId"] = [error] });

        if (budget is null)
        {
            budget = new Budget { UserId = userId, Month = month, CategoryId = categoryId };
            db.Budgets.Add(budget);
        }
        budget.Amount = amount;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 2 request cùng tạo 1 lúc → unique index (user, month, category) chặn bản thứ 2
            throw ApiProblemException.Conflict("Ngân sách này vừa được tạo – hãy tải lại", "categoryId");
        }

        var spent = await SpentAsync(userId, month, [categoryId], ct);
        return ToDto(budget.Id, month, new CategoryRef(category!.Id, category.Name, category.Icon, category.Color),
            amount, spent.GetValueOrDefault(categoryId));
    }

    public async Task DeleteAsync(Guid userId, long id, CancellationToken ct)
    {
        var deleted = await db.Budgets.Where(b => b.Id == id && b.UserId == userId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw ApiProblemException.NotFound(NotFound);
    }

    /// <summary>Chỉ thêm danh mục tháng đích chưa có ngân sách; bỏ qua danh mục đã lưu trữ.</summary>
    public async Task<CopyBudgetsResult> CopyAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var existing = db.Budgets.Where(b => b.UserId == userId && b.Month == to).Select(b => b.CategoryId);
        var sources = await db.Budgets.AsNoTracking()
            .Where(b => b.UserId == userId && b.Month == from && !b.Category.IsArchived && !existing.Contains(b.CategoryId))
            .Select(b => new { b.CategoryId, b.Amount })
            .ToListAsync(ct);

        db.Budgets.AddRange(sources.Select(s => new Budget { UserId = userId, Month = to, CategoryId = s.CategoryId, Amount = s.Amount }));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw ApiProblemException.Conflict("Ngân sách tháng đích vừa thay đổi – hãy tải lại rồi thử lại");
        }
        return new CopyBudgetsResult(sources.Count);
    }

    /// <summary>Σ chi theo danh mục trong tháng – tính cả ví / danh mục đã lưu trữ.</summary>
    private async Task<Dictionary<long, decimal>> SpentAsync(Guid userId, DateOnly month, List<long> categoryIds, CancellationToken ct)
    {
        var monthEnd = month.AddMonths(1).AddDays(-1);
        return await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.Kind == TransactionKind.Expense && t.CategoryId != null
                && categoryIds.Contains(t.CategoryId.Value) && t.OccurredOn >= month && t.OccurredOn <= monthEnd)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Total, ct);
    }

    private static BudgetDto ToDto(long id, DateOnly month, CategoryRef category, decimal amount, decimal spent)
    {
        var percent = Percent(spent, amount);
        return new BudgetDto(id, month.ToString("yyyy-MM", CultureInfo.InvariantCulture), category, amount, spent, amount - spent, percent, Status(percent));
    }

    /// <summary>Làm tròn 1 chữ số; trạng thái xét trên số đã làm tròn để khớp con số người dùng thấy.</summary>
    internal static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : decimal.Round(part / whole * 100, 1, MidpointRounding.AwayFromZero);

    internal static BudgetStatus Status(decimal percent) => percent switch
    {
        < 80 => BudgetStatus.Ok,
        <= 100 => BudgetStatus.Warning,
        _ => BudgetStatus.Over,
    };
}
