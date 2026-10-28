using backend.Data.Entities;

namespace backend.Contracts;

// Enum trong request nhận chuỗi (string?) để báo lỗi tiếng Việt đúng ô thay vì 400 chung chung khi giá trị lạ.

public sealed record WalletRequest(string? Name, string? Type, decimal? OpeningBalance, string? Color, bool? IsArchived);

public sealed record WalletDto(
    long Id, string Name, WalletType Type, decimal OpeningBalance, decimal Balance, string Color, bool IsArchived, int SortOrder);

/// <summary>Total = tổng số dư các ví đang dùng (không tính ví đã lưu trữ).</summary>
public sealed record WalletListDto(IReadOnlyList<WalletDto> Items, decimal Total);

public sealed record CategoryRequest(string? Name, string? Kind, string? Icon, string? Color, bool? IsArchived);

public sealed record CategoryDto(long Id, CategoryKind Kind, string Name, string Icon, string Color, bool IsArchived, int SortOrder);

public sealed record WalletRef(long Id, string Name, string Color);

public sealed record CategoryRef(long Id, string Name, string Icon, string Color);

public sealed record TransactionRequest(
    string? Kind, long? WalletId, long? ToWalletId, long? CategoryId, decimal? Amount, DateOnly? OccurredOn, string? Note);

public sealed record TransactionDto(
    long Id, TransactionKind Kind, decimal Amount, DateOnly OccurredOn, string? Note,
    WalletRef Wallet, WalletRef? ToWallet, CategoryRef? Category, DateTime CreatedAt);

public sealed record MoneyTotals(decimal Income, decimal Expense);

public sealed record PagedTransactions(
    IReadOnlyList<TransactionDto> Items, int Page, int PageSize, int TotalItems, int TotalPages, MoneyTotals Totals);

/// <summary>Query string của GET /api/transactions và /export.</summary>
public sealed record TransactionQuery(
    DateOnly? From, DateOnly? To, long? WalletId, long? CategoryId, string? Kind, string? Q, int? Page, int? PageSize);

/// <summary>ok: &lt; 80% · warning: 80 – 100% · over: &gt; 100% (docs/02-nghiep-vu.md §5).</summary>
public enum BudgetStatus { Ok, Warning, Over }

/// <summary>Month dạng "yyyy-MM".</summary>
public sealed record BudgetRequest(string? Month, long? CategoryId, decimal? Amount);

public sealed record BudgetDto(
    long Id, string Month, CategoryRef Category, decimal Amount, decimal Spent, decimal Remaining, decimal Percent, BudgetStatus Status);

public sealed record CopyBudgetsRequest(string? From, string? To);

public sealed record CopyBudgetsResult(int Created);

/// <summary>Query string chung của /api/reports/* – kind chỉ dùng cho by-category, groupBy cho trend.</summary>
public sealed record ReportQuery(DateOnly? From, DateOnly? To, string? Kind, string? GroupBy);

public sealed record ReportSummaryDto(
    DateOnly From, DateOnly To, decimal Income, decimal Expense, decimal Net, int TransactionCount, decimal DailyAverageExpense);

public sealed record CategoryReportItem(CategoryRef Category, decimal Total, decimal Percent, int Count);

/// <summary>Period: "2026-10" (groupBy=month) hoặc "2026-10-28" (groupBy=day).</summary>
public sealed record TrendPoint(string Period, decimal Income, decimal Expense);
