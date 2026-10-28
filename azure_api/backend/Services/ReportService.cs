using System.Globalization;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public enum TrendGroup { Day, Month }

/// <summary>Báo cáo thu / chi (docs/02-nghiep-vu.md §6) – chuyển tiền không tính vào thu / chi.</summary>
public sealed class ReportService(AppDbContext db, AppClock clock)
{
    public async Task<ReportSummaryDto> SummaryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var byKind = await InRange(userId, from, to)
            .GroupBy(t => t.Kind)
            .Select(g => new { Kind = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var income = byKind.Where(x => x.Kind == TransactionKind.Income).Sum(x => x.Total);
        var expense = byKind.Where(x => x.Kind == TransactionKind.Expense).Sum(x => x.Total);
        var average = decimal.Round(expense / ElapsedDays(from, to), 2, MidpointRounding.AwayFromZero);
        return new ReportSummaryDto(from, to, income, expense, income - expense, byKind.Sum(x => x.Count), average);
    }

    /// <summary>Sắp xếp total giảm dần; percent trên tổng của kind trong khoảng.</summary>
    public async Task<IReadOnlyList<CategoryReportItem>> ByCategoryAsync(Guid userId, DateOnly from, DateOnly to, CategoryKind kind,
        CancellationToken ct)
    {
        var transactionKind = kind == CategoryKind.Income ? TransactionKind.Income : TransactionKind.Expense;
        var rows = await InRange(userId, from, to)
            .Where(t => t.Kind == transactionKind && t.CategoryId != null)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.CategoryId).ToList();
        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.UserId == userId && ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new CategoryRef(c.Id, c.Name, c.Icon, c.Color), ct);

        var sum = rows.Sum(r => r.Total);
        return rows
            .Select(r => new CategoryReportItem(categories[r.CategoryId], r.Total, BudgetService.Percent(r.Total, sum), r.Count))
            .OrderByDescending(x => x.Total).ThenBy(x => x.Category.Name)
            .ToList();
    }

    /// <summary>Đủ mọi kỳ trong khoảng (kỳ không có giao dịch = 0) để biểu đồ không bị hụt cột.</summary>
    public async Task<IReadOnlyList<TrendPoint>> TrendAsync(Guid userId, DateOnly from, DateOnly to, TrendGroup groupBy,
        CancellationToken ct)
    {
        // Gom theo ngày bằng SQL (≤ 366 ngày × 2 loại), gộp tháng trong bộ nhớ – tránh dịch YEAR()/MONTH() qua converter DateOnly
        var daily = await InRange(userId, from, to)
            .GroupBy(t => new { t.OccurredOn, t.Kind })
            .Select(g => new { g.Key.OccurredOn, g.Key.Kind, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var points = new Dictionary<DateOnly, (decimal Income, decimal Expense)>();
        for (var d = PeriodStart(from, groupBy); d <= to; d = groupBy == TrendGroup.Day ? d.AddDays(1) : d.AddMonths(1))
            points[d] = (0, 0);
        foreach (var row in daily)
        {
            var key = PeriodStart(row.OccurredOn, groupBy);
            var (income, expense) = points[key];
            points[key] = row.Kind == TransactionKind.Income ? (income + row.Total, expense) : (income, expense + row.Total);
        }

        var format = groupBy == TrendGroup.Day ? "yyyy-MM-dd" : "yyyy-MM";
        return points.Select(p => new TrendPoint(p.Key.ToString(format, CultureInfo.InvariantCulture), p.Value.Income, p.Value.Expense))
            .ToList();
    }

    private IQueryable<Transaction> InRange(Guid userId, DateOnly from, DateOnly to) =>
        db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Kind != TransactionKind.Transfer
            && t.OccurredOn >= from && t.OccurredOn <= to);

    private static DateOnly PeriodStart(DateOnly date, TrendGroup groupBy) =>
        groupBy == TrendGroup.Day ? date : new DateOnly(date.Year, date.Month, 1);

    /// <summary>
    /// Số ngày để chia trung bình: khoảng đang diễn ra (VD "tháng này" ngày 28) chỉ tính tới hôm nay,
    /// không thì chia cho cả ngày chưa tới → trung bình thấp hơn thực tế.
    /// </summary>
    private int ElapsedDays(DateOnly from, DateOnly to)
    {
        var today = clock.Today;
        var end = from <= today && today < to ? today : to;
        return end.DayNumber - from.DayNumber + 1;
    }
}
