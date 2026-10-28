using System.Net;
using System.Net.Http.Json;
using System.Text;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class TransactionApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Ctx(HttpClient Client, long Cash, long Food, long Salary);

    private async Task<Ctx> SetupAsync()
    {
        var user = await factory.CreateUserAsync();
        var c = user.Client;
        return new Ctx(c, (await c.WalletsAsync()).Items.Single().Id, (await c.CategoryAsync("Ăn uống")).Id, (await c.CategoryAsync("Lương")).Id);
    }

    [Fact]
    public async Task DoDScenario_ExpenseThenTransfer_BalancesAreCorrect()
    {
        // docs/05 Phase 2 DoD: ví A 1 000 000 → chi 200 000 → chuyển 300 000 sang B → A = 500 000, B = 300 000, tổng 800 000
        var ctx = await SetupAsync();
        var a = await ctx.Client.CreateWalletAsync("Ví A", 1_000_000);
        var b = await ctx.Client.CreateWalletAsync("Ví B");

        await ctx.Client.ExpenseAsync(a.Id, ctx.Food, 200_000);
        var transfer = await ctx.Client.TransferAsync(a.Id, b.Id, 300_000);

        var wallets = await ctx.Client.WalletsAsync();
        Assert.Equal(500_000m, wallets.Balance(a.Id));
        Assert.Equal(300_000m, wallets.Balance(b.Id));
        Assert.Equal(800_000m, wallets.Total);
        Assert.Equal("Ví B", transfer.ToWallet!.Name);
        Assert.Null(transfer.Category);
    }

    [Fact]
    public async Task Create_ReturnsDtoWithWalletAndCategoryNames()
    {
        var ctx = await SetupAsync();

        var response = await ctx.Client.PostTransactionAsync(new
        {
            kind = "expense", walletId = ctx.Cash, categoryId = ctx.Food, amount = 45_000, occurredOn = "2026-10-28", note = "  Phở bò  ",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.ReadAsync<TransactionDto>();
        Assert.Equal(TransactionKind.Expense, dto.Kind);
        Assert.Equal(45_000m, dto.Amount);
        Assert.Equal(new DateOnly(2026, 10, 28), dto.OccurredOn);
        Assert.Equal("Phở bò", dto.Note);
        Assert.Equal("Tiền mặt", dto.Wallet.Name);
        Assert.Equal("Ăn uống", dto.Category!.Name);
        Assert.Equal("utensils", dto.Category.Icon);
        Assert.Contains("\"kind\":\"expense\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_InvalidShape_Returns400PerField()
    {
        var ctx = await SetupAsync();

        var noCategory = await ctx.Client.PostTransactionAsync(new { kind = "expense", walletId = ctx.Cash, amount = 0, occurredOn = "1999-12-31" });
        var selfTransfer = await ctx.Client.PostTransactionAsync(new { kind = "transfer", walletId = ctx.Cash, toWalletId = ctx.Cash, amount = 1, occurredOn = "2026-10-01" });
        var badKind = await ctx.Client.PostTransactionAsync(new { kind = "loan", walletId = ctx.Cash, amount = 1, occurredOn = "2026-10-01" });

        var e1 = (await noCategory.ReadProblemAsync()).GetProperty("errors");
        Assert.Equal("Vui lòng chọn danh mục", e1.GetProperty("categoryId")[0].GetString());
        Assert.Equal("Số tiền phải lớn hơn 0", e1.GetProperty("amount")[0].GetString());
        Assert.StartsWith("Ngày phải trong khoảng 01/01/2000", e1.GetProperty("occurredOn")[0].GetString());
        Assert.Equal("Ví nhận phải khác ví nguồn",
            (await selfTransfer.ReadProblemAsync()).GetProperty("errors").GetProperty("toWalletId")[0].GetString());
        Assert.True((await badKind.ReadProblemAsync()).GetProperty("errors").TryGetProperty("kind", out _));
    }

    [Fact]
    public async Task Create_CategoryOfWrongKind_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await ctx.Client.PostTransactionAsync(new { kind = "income", walletId = ctx.Cash, categoryId = ctx.Food, amount = 1, occurredOn = "2026-10-01" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Hãy chọn danh mục thu", (await response.ReadProblemAsync()).GetProperty("errors").GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task Create_WithOtherUsersWalletOrCategory_LooksNotFound()
    {
        var ctx = await SetupAsync();
        var other = await SetupAsync();

        var response = await ctx.Client.PostTransactionAsync(new { kind = "expense", walletId = other.Cash, categoryId = other.Food, amount = 1, occurredOn = "2026-10-01" });

        var errors = (await response.ReadProblemAsync()).GetProperty("errors");
        Assert.Equal("Không tìm thấy ví", errors.GetProperty("walletId")[0].GetString());
        Assert.Equal("Không tìm thấy danh mục", errors.GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task ArchivedWallet_CannotReceiveNewTransactions_ButOldOnesStayEditable()
    {
        var ctx = await SetupAsync();
        var old = await ctx.Client.CreateWalletAsync("Ví cũ");
        var tx = await ctx.Client.ExpenseAsync(old.Id, ctx.Food, 10_000);
        await ctx.Client.PutAsJsonAsync($"/api/wallets/{old.Id}", new { name = "Ví cũ", type = "bank", isArchived = true });

        var create = await ctx.Client.PostTransactionAsync(new { kind = "expense", walletId = old.Id, categoryId = ctx.Food, amount = 1, occurredOn = "2026-10-01" });
        var edit = await ctx.Client.PutAsJsonAsync($"/api/transactions/{tx.Id}",
            new { kind = "expense", walletId = old.Id, categoryId = ctx.Food, amount = 12_000, occurredOn = "2026-10-15", note = "sửa" });

        Assert.Equal("Ví đã lưu trữ", (await create.ReadProblemAsync()).GetProperty("errors").GetProperty("walletId")[0].GetString());
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(12_000m, (await edit.ReadAsync<TransactionDto>()).Amount);
    }

    [Fact]
    public async Task Update_CanChangeKind_FromExpenseToTransfer()
    {
        var ctx = await SetupAsync();
        var bank = await ctx.Client.CreateWalletAsync("Ngân hàng");
        var tx = await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 100_000);

        var response = await ctx.Client.PutAsJsonAsync($"/api/transactions/{tx.Id}",
            new { kind = "transfer", walletId = ctx.Cash, toWalletId = bank.Id, categoryId = ctx.Food, amount = 100_000, occurredOn = "2026-10-15" });

        var dto = await response.ReadAsync<TransactionDto>();
        Assert.Equal(TransactionKind.Transfer, dto.Kind);
        Assert.Null(dto.Category); // danh mục bị bỏ khi đổi sang chuyển tiền
        Assert.Equal(100_000m, (await ctx.Client.WalletsAsync()).Balance(bank.Id));
    }

    [Fact]
    public async Task Delete_RemovesTransaction_AndRestoresBalance()
    {
        var ctx = await SetupAsync();
        var tx = await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 30_000);

        var delete = await ctx.Client.DeleteAsync($"/api/transactions/{tx.Id}");
        var again = await ctx.Client.DeleteAsync($"/api/transactions/{tx.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(0m, (await ctx.Client.WalletsAsync()).Total);
    }

    [Fact]
    public async Task List_FiltersByRange_SortsNewestFirst_TotalsExcludeTransfers()
    {
        var ctx = await SetupAsync();
        var bank = await ctx.Client.CreateWalletAsync("Ngân hàng");
        await ctx.Client.IncomeAsync(ctx.Cash, ctx.Salary, 15_000_000, "2026-10-05");
        var first = await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 50_000, "2026-10-10");
        var second = await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 70_000, "2026-10-10");
        await ctx.Client.TransferAsync(ctx.Cash, bank.Id, 1_000_000, "2026-10-20");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 999, "2026-09-30"); // ngoài khoảng

        var page = await ctx.Client.ListAsync("from=2026-10-01&to=2026-10-31");

        Assert.Equal(4, page.TotalItems);
        Assert.Equal(15_000_000m, page.Totals.Income);
        Assert.Equal(120_000m, page.Totals.Expense);
        Assert.Equal(new DateOnly(2026, 10, 20), page.Items[0].OccurredOn);
        Assert.Equal([second.Id, first.Id], page.Items.Where(t => t.OccurredOn.Day == 10).Select(t => t.Id));
    }

    [Fact]
    public async Task List_FilterByWallet_IncludesIncomingTransfers()
    {
        var ctx = await SetupAsync();
        var bank = await ctx.Client.CreateWalletAsync("Ngân hàng");
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 50_000);
        await ctx.Client.TransferAsync(ctx.Cash, bank.Id, 200_000);

        var page = await ctx.Client.ListAsync($"from=2026-10-01&to=2026-10-31&walletId={bank.Id}");

        var only = Assert.Single(page.Items);
        Assert.Equal(TransactionKind.Transfer, only.Kind);
    }

    [Fact]
    public async Task List_FilterByKindCategoryAndNote_Paginates()
    {
        var ctx = await SetupAsync();
        for (var i = 1; i <= 5; i++) await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, i * 1000, $"2026-10-{i:00}", i % 2 == 0 ? $"Cà PHÊ {i}" : "cơm");
        await ctx.Client.IncomeAsync(ctx.Cash, ctx.Salary, 1, "2026-10-03", "cà phê thưởng");

        var byNote = await ctx.Client.ListAsync("from=2026-10-01&to=2026-10-31&kind=expense&q=cà phê");
        var paged = await ctx.Client.ListAsync($"from=2026-10-01&to=2026-10-31&categoryId={ctx.Food}&pageSize=2&page=3");

        Assert.Equal(2, byNote.TotalItems); // không phân biệt hoa thường, chỉ khoản chi
        Assert.Equal(5, paged.TotalItems);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single(paged.Items).OccurredOn);
    }

    [Fact]
    public async Task List_DefaultsToCurrentMonthInVietnamTime()
    {
        var ctx = await SetupAsync();
        var today = factory.Services.GetRequiredService<AppClock>().Today;
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 1_000, today.ToString("yyyy-MM-dd"));
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 2_000, today.AddMonths(-1).ToString("yyyy-MM-dd"));

        var page = await ctx.Client.ListAsync("");

        Assert.Equal(1_000m, Assert.Single(page.Items).Amount);
    }

    [Theory]
    [InlineData("from=2026-10-31&to=2026-10-01", "from")]
    [InlineData("from=2025-01-01&to=2026-10-01", "to")]
    [InlineData("pageSize=500", "pageSize")]
    [InlineData("kind=abc", "kind")]
    public async Task List_InvalidQuery_Returns400(string query, string field)
    {
        var ctx = await SetupAsync();

        var response = await ctx.Client.GetAsync($"/api/transactions?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task OtherUsersTransaction_Returns404()
    {
        var owner = await SetupAsync();
        var intruder = await SetupAsync();
        var tx = await owner.Client.ExpenseAsync(owner.Cash, owner.Food, 1_000);

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.GetAsync($"/api/transactions/{tx.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.DeleteAsync($"/api/transactions/{tx.Id}")).StatusCode);
        Assert.Equal(0, (await intruder.Client.ListAsync("from=2026-10-01&to=2026-10-31")).TotalItems);
    }

    [Fact]
    public async Task Export_ReturnsUtf8BomCsv_WithInjectionGuard()
    {
        var ctx = await SetupAsync();
        await ctx.Client.ExpenseAsync(ctx.Cash, ctx.Food, 45_000.5m, "2026-10-28", "=HYPERLINK(\"x\"), ăn sáng");

        var response = await ctx.Client.GetAsync("/api/transactions/export?from=2026-10-01&to=2026-10-31");

        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("giao-dich_2026-10-01_2026-10-31.csv", response.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\n')).ToArray();
        Assert.Equal("Ngày,Loại,Ví,Ví nhận,Danh mục,Số tiền,Ghi chú", lines[0].Trim());
        Assert.Equal("2026-10-28,Chi,Tiền mặt,,Ăn uống,45000.5,\"'=HYPERLINK(\"\"x\"\"), ăn sáng\"", lines[1].Trim());
    }

    [Theory]
    [InlineData("bình thường", "bình thường")]
    [InlineData("-1+1", "'-1+1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("a,b", "\"a,b\"")]
    public void CsvCell_EscapesFormulasAndSeparators(string input, string expected) =>
        Assert.Equal(expected, TransactionService.Cell(input));
}
