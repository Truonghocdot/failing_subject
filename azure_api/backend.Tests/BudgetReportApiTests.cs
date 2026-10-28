using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class BudgetReportApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Ctx(HttpClient Client, long Cash, long Food, long Transport, long Salary);

    private async Task<Ctx> SetupAsync()
    {
        var c = (await factory.CreateUserAsync()).Client;
        return new Ctx(c, (await c.WalletsAsync()).Items.Single().Id,
            (await c.CategoryAsync("Ăn uống")).Id, (await c.CategoryAsync("Đi lại")).Id, (await c.CategoryAsync("Lương")).Id);
    }

    private static Task<HttpResponseMessage> PutBudgetAsync(HttpClient client, string month, long categoryId, decimal amount) =>
        client.PutAsJsonAsync("/api/budgets", new { month, categoryId, amount });

    private static async Task<BudgetDto> SetBudgetAsync(HttpClient client, string month, long categoryId, decimal amount)
    {
        var response = await PutBudgetAsync(client, month, categoryId, amount);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<BudgetDto>();
    }

    private static async Task<List<BudgetDto>> BudgetsAsync(HttpClient client, string? month = null) =>
        (await client.GetFromJsonAsync<List<BudgetDto>>($"/api/budgets?month={month}", LedgerHelpers.Json))!;

    private static async Task<T> GetAsync<T>(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, LedgerHelpers.Json))!;

    // ---------- Ngân sách ----------

    [Fact]
    public async Task DoDScenario_Budget85Percent_IsWarning()
    {
        // docs/05 Phase 2 DoD: ngân sách Ăn uống 1 000 000, chi 850 000 → percent 85, status warning
        var ctx = await SetupAsync();
        await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 1_000_000);

        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 600_000, "2026-09-01");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 250_000, "2026-09-30");
        // không tính: tháng khác, danh mục khác, khoản thu
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 999_000, "2026-08-31");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 999_000, "2026-10-01");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Transport, 70_000, "2026-09-15");
        await ctx.Client.IncomeAsync(ctx.Cash, ctx.Salary, 15_000_000, "2026-09-15");

        var budget = Assert.Single(await BudgetsAsync(ctx.Client, "2026-09"));
        Assert.Equal("2026-09", budget.Month);
        Assert.Equal("Ăn uống", budget.Category.Name);
        Assert.Equal(850_000m, budget.Spent);
        Assert.Equal(150_000m, budget.Remaining);
        Assert.Equal(85.0m, budget.Percent);
        Assert.Equal(BudgetStatus.Warning, budget.Status);
    }

    [Fact]
    public async Task Status_FollowsThresholds_AndJsonIsLowercase()
    {
        var ctx = await SetupAsync();
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 1_200_000, "2026-09-10");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Transport, 79_940, "2026-09-10");

        var over = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 1_000_000);
        var ok = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Transport, 100_000);

        Assert.Equal((120.0m, BudgetStatus.Over, -200_000m), (over.Percent, over.Status, over.Remaining));
        Assert.Equal((79.9m, BudgetStatus.Ok), (ok.Percent, ok.Status));
        // 80% đúng ngưỡng → warning
        var edge = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Transport, 99_925); // 79 940 / 99 925 = đúng 80%
        Assert.Equal((80.0m, BudgetStatus.Warning), (edge.Percent, edge.Status));

        var json = await ctx.Client.GetStringAsync("/api/budgets?month=2026-09");
        Assert.Contains("\"status\":\"over\"", json);
        Assert.Contains("\"month\":\"2026-09\"", json);
    }

    [Fact]
    public async Task Put_IsUpsert_PerMonthAndCategory()
    {
        var ctx = await SetupAsync();

        var first = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 1_000_000);
        var second = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 2_500_000);
        var otherMonth = await SetBudgetAsync(ctx.Client, "2026-10", ctx.Food, 500_000);

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, otherMonth.Id);
        var september = Assert.Single(await BudgetsAsync(ctx.Client, "2026-09"));
        Assert.Equal(2_500_000m, september.Amount);
    }

    [Fact]
    public async Task List_DefaultsToCurrentMonth()
    {
        var ctx = await SetupAsync();
        var month = factory.Services.GetRequiredService<AppClock>().CurrentMonth.From.ToString("yyyy-MM");
        await SetBudgetAsync(ctx.Client, month, ctx.Food, 1_000_000);

        Assert.Equal(month, Assert.Single(await BudgetsAsync(ctx.Client)).Month);
    }

    [Fact]
    public async Task Put_Invalid_Returns400PerField()
    {
        var ctx = await SetupAsync();
        var stranger = await factory.CreateUserAsync();
        var strangerFood = (await stranger.Client.CategoryAsync("Ăn uống")).Id;

        var badShape = await ctx.Client.PutAsJsonAsync("/api/budgets", new { month = "2026-13", amount = 0 });
        var income = await PutBudgetAsync(ctx.Client, "2026-09", ctx.Salary, 1_000);
        var notMine = await PutBudgetAsync(ctx.Client, "2026-09", strangerFood, 1_000);
        var badQuery = await ctx.Client.GetAsync("/api/budgets?month=09-2026");

        var e = (await badShape.ReadProblemAsync()).GetProperty("errors");
        Assert.StartsWith("Tháng phải có dạng yyyy-MM", e.GetProperty("month")[0].GetString());
        Assert.Equal("Vui lòng chọn danh mục", e.GetProperty("categoryId")[0].GetString());
        Assert.Equal("Số tiền ngân sách phải lớn hơn 0", e.GetProperty("amount")[0].GetString());
        Assert.Equal("Hãy chọn danh mục chi",
            (await income.ReadProblemAsync()).GetProperty("errors").GetProperty("categoryId")[0].GetString());
        Assert.Equal("Không tìm thấy danh mục",
            (await notMine.ReadProblemAsync()).GetProperty("errors").GetProperty("categoryId")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);
    }

    [Fact]
    public async Task ArchivedCategory_CannotGetNewBudget_ButExistingStillEditable()
    {
        var ctx = await SetupAsync();
        await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 1_000_000);
        await ctx.Client.PutAsJsonAsync($"/api/categories/{ctx.Food}",
            new { name = "Ăn uống", icon = "utensils", color = "#f97316", isArchived = true });

        var edit = await PutBudgetAsync(ctx.Client, "2026-09", ctx.Food, 1_200_000);
        var create = await PutBudgetAsync(ctx.Client, "2026-10", ctx.Food, 1_000_000);

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Danh mục đã lưu trữ", (await create.ReadProblemAsync()).GetProperty("errors").GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task Delete_OwnBudget_204_OthersBudget_404_AndCategoryWithBudgetIsInUse()
    {
        var ctx = await SetupAsync();
        var stranger = await factory.CreateUserAsync();
        var budget = await SetBudgetAsync(ctx.Client, "2026-09", ctx.Transport, 300_000);

        // danh mục có ngân sách (dù chưa có giao dịch) không xoá được
        var deleteCategory = await ctx.Client.DeleteAsync($"/api/categories/{ctx.Transport}");
        Assert.Equal(HttpStatusCode.Conflict, deleteCategory.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.DeleteAsync($"/api/budgets/{budget.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ctx.Client.DeleteAsync($"/api/budgets/{budget.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ctx.Client.DeleteAsync($"/api/budgets/{budget.Id}")).StatusCode);
        Assert.Empty(await BudgetsAsync(ctx.Client, "2026-09"));
        Assert.Equal(HttpStatusCode.NoContent, (await ctx.Client.DeleteAsync($"/api/categories/{ctx.Transport}")).StatusCode);
    }

    [Fact]
    public async Task Copy_AddsOnlyMissingCategories()
    {
        var ctx = await SetupAsync();
        await SetBudgetAsync(ctx.Client, "2026-08", ctx.Food, 3_000_000);
        await SetBudgetAsync(ctx.Client, "2026-08", ctx.Transport, 500_000);
        await SetBudgetAsync(ctx.Client, "2026-09", ctx.Food, 2_000_000); // tháng đích đã có Ăn uống

        var response = await ctx.Client.PostAsJsonAsync("/api/budgets/copy", new { from = "2026-08", to = "2026-09" });
        var again = await ctx.Client.PostAsJsonAsync("/api/budgets/copy", new { from = "2026-08", to = "2026-09" });
        var same = await ctx.Client.PostAsJsonAsync("/api/budgets/copy", new { from = "2026-09", to = "2026-09" });

        Assert.Equal(1, (await response.ReadAsync<CopyBudgetsResult>()).Created);
        Assert.Equal(0, (await again.ReadAsync<CopyBudgetsResult>()).Created);
        Assert.Equal("Tháng đích phải khác tháng nguồn", (await same.ReadProblemAsync()).GetProperty("errors").GetProperty("to")[0].GetString());

        var september = await BudgetsAsync(ctx.Client, "2026-09");
        Assert.Equal(2_000_000m, september.Single(b => b.Category.Id == ctx.Food).Amount); // không bị ghi đè
        Assert.Equal(500_000m, september.Single(b => b.Category.Id == ctx.Transport).Amount);
    }

    // ---------- Báo cáo ----------

    [Fact]
    public async Task Summary_ExcludesTransfers_AndAveragesOverDays()
    {
        var ctx = await SetupAsync();
        var bank = await ctx.Client.CreateWalletAsync("Ngân hàng", 0);
        await ctx.Client.IncomeAsync(bank.Id, ctx.Salary, 15_000_000, "2026-09-05");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 200_000, "2026-09-01");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Transport, 100_000, "2026-09-30");
        await ctx.Client.TransferAsync(bank.Id, ctx.Cash, 2_000_000, "2026-09-06");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 50_000, "2026-10-01"); // ngoài khoảng

        var s = await GetAsync<ReportSummaryDto>(ctx.Client, "/api/reports/summary?from=2026-09-01&to=2026-09-30");

        Assert.Equal((15_000_000m, 300_000m, 14_700_000m, 3), (s.Income, s.Expense, s.Net, s.TransactionCount));
        Assert.Equal(10_000m, s.DailyAverageExpense); // 300 000 / 30 ngày

        // user khác không thấy gì
        var stranger = await factory.CreateUserAsync();
        var empty = await GetAsync<ReportSummaryDto>(stranger.Client, "/api/reports/summary?from=2026-09-01&to=2026-09-30");
        Assert.Equal((0m, 0m, 0), (empty.Income, empty.Expense, empty.TransactionCount));
    }

    [Fact]
    public async Task Summary_CurrentMonth_AveragesOnlyDaysSoFar()
    {
        var ctx = await SetupAsync();
        var today = factory.Services.GetRequiredService<AppClock>().Today;
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 300_000m * today.Day, today.ToString("yyyy-MM-dd"));

        var s = await GetAsync<ReportSummaryDto>(ctx.Client, "/api/reports/summary");

        Assert.Equal(new DateOnly(today.Year, today.Month, 1), s.From);
        Assert.Equal(300_000m, s.DailyAverageExpense); // chia cho số ngày đã qua, không phải cả tháng
    }

    [Fact]
    public async Task ByCategory_SortedByTotal_WithPercentAndCount()
    {
        var ctx = await SetupAsync();
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 150_000, "2026-09-01");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 50_000, "2026-09-02");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Transport, 100_000, "2026-09-03");
        await ctx.Client.IncomeAsync(ctx.Cash, ctx.Salary, 9_000_000, "2026-09-05");

        var expense = await GetAsync<List<CategoryReportItem>>(ctx.Client, "/api/reports/by-category?from=2026-09-01&to=2026-09-30");
        var income = await GetAsync<List<CategoryReportItem>>(ctx.Client, "/api/reports/by-category?from=2026-09-01&to=2026-09-30&kind=income");
        var transfer = await ctx.Client.GetAsync("/api/reports/by-category?kind=transfer");

        Assert.Equal(["Ăn uống", "Đi lại"], expense.Select(x => x.Category.Name));
        Assert.Equal((200_000m, 66.7m, 2), (expense[0].Total, expense[0].Percent, expense[0].Count));
        Assert.Equal((100_000m, 33.3m, 1), (expense[1].Total, expense[1].Percent, expense[1].Count));
        Assert.Equal("utensils", expense[0].Category.Icon);
        Assert.Equal((9_000_000m, 100.0m), (Assert.Single(income).Total, income[0].Percent));
        Assert.Equal(HttpStatusCode.BadRequest, transfer.StatusCode);
    }

    [Fact]
    public async Task Trend_ReturnsEveryPeriod_EmptyOnesAreZero()
    {
        var ctx = await SetupAsync();
        var bank = await ctx.Client.CreateWalletAsync("Ngân hàng", 0);
        await ctx.Client.IncomeAsync(bank.Id, ctx.Salary, 10_000_000, "2026-07-05");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 200_000, "2026-07-20");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 300_000, "2026-09-02");
        await ctx.Client.TransferAsync(bank.Id, ctx.Cash, 1_000_000, "2026-08-10"); // không tính

        var months = await GetAsync<List<TrendPoint>>(ctx.Client, "/api/reports/trend?from=2026-07-15&to=2026-09-30&groupBy=month");
        var days = await GetAsync<List<TrendPoint>>(ctx.Client, "/api/reports/trend?from=2026-09-01&to=2026-09-03&groupBy=day");

        // kỳ đầu tính từ from (15/07) → khoản thu ngày 05/07 không vào
        Assert.Equal(
            [new TrendPoint("2026-07", 0, 200_000), new TrendPoint("2026-08", 0, 0), new TrendPoint("2026-09", 0, 300_000)],
            months);
        Assert.Equal(
            [new TrendPoint("2026-09-01", 0, 0), new TrendPoint("2026-09-02", 0, 300_000), new TrendPoint("2026-09-03", 0, 0)],
            days);
    }

    [Fact]
    public async Task Reports_InvalidRange_Returns400()
    {
        var ctx = await SetupAsync();

        var reversed = await ctx.Client.GetAsync("/api/reports/summary?from=2026-09-30&to=2026-09-01");
        var tooLong = await ctx.Client.GetAsync("/api/reports/summary?from=2025-01-01&to=2026-09-30");
        var dailyTooLong = await ctx.Client.GetAsync("/api/reports/trend?from=2026-01-01&to=2026-09-30&groupBy=day");
        var badGroup = await ctx.Client.GetAsync("/api/reports/trend?groupBy=week");

        Assert.Equal("Ngày bắt đầu phải trước ngày kết thúc",
            (await reversed.ReadProblemAsync()).GetProperty("errors").GetProperty("from")[0].GetString());
        Assert.Equal("Khoảng ngày tối đa 366 ngày", (await tooLong.ReadProblemAsync()).GetProperty("errors").GetProperty("to")[0].GetString());
        Assert.Equal("Khoảng ngày tối đa 92 ngày", (await dailyTooLong.ReadProblemAsync()).GetProperty("errors").GetProperty("to")[0].GetString());
        Assert.True((await badGroup.ReadProblemAsync()).GetProperty("errors").TryGetProperty("groupBy", out _));
    }
}
