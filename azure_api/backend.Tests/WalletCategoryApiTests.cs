using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;

namespace backend.Tests;

public class WalletCategoryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Wallets_ListDefaultCashWallet()
    {
        var user = await factory.CreateUserAsync();

        var list = await user.Client.WalletsAsync();

        var cash = Assert.Single(list.Items);
        Assert.Equal("Tiền mặt", cash.Name);
        Assert.Equal(WalletType.Cash, cash.Type);
        Assert.Equal(0m, list.Total);
    }

    [Fact]
    public async Task CreateWallet_DefaultsColorByType_AndReturnsBalanceEqualOpening()
    {
        var user = await factory.CreateUserAsync();

        var wallet = await user.Client.CreateWalletAsync("Vietcombank", 5_000_000);

        Assert.Equal(WalletType.Bank, wallet.Type);
        Assert.Equal("#2563eb", wallet.Color);
        Assert.Equal(5_000_000m, wallet.Balance);
        Assert.Equal(5_000_000m, (await user.Client.WalletsAsync()).Total);
    }

    [Fact]
    public async Task CreateWallet_DuplicateNameIgnoringCase_Returns409OnNameField()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/wallets", new { name = "tiền MẶT", type = "cash" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal("Tên ví đã tồn tại", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task CreateWallet_InvalidInput_Returns400PerField()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/wallets",
            new { name = "", type = "vang", openingBalance = 1.005m, color = "red" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadProblemAsync()).GetProperty("errors");
        Assert.Equal("Vui lòng nhập tên ví", errors.GetProperty("name")[0].GetString());
        Assert.StartsWith("Loại ví không hợp lệ", errors.GetProperty("type")[0].GetString());
        Assert.Equal("Số dư đầu tối đa 2 chữ số thập phân", errors.GetProperty("openingBalance")[0].GetString());
        Assert.Equal("Màu phải có dạng #RRGGBB", errors.GetProperty("color")[0].GetString());
    }

    [Fact]
    public async Task CreditWallet_AllowsNegativeOpeningBalance()
    {
        var user = await factory.CreateUserAsync();

        var card = await user.Client.CreateWalletAsync("Thẻ tín dụng", -2_500_000, "credit");

        Assert.Equal(-2_500_000m, card.Balance);
    }

    [Fact]
    public async Task ArchiveWallet_HidesFromDefaultList_ExcludedFromTotal()
    {
        var user = await factory.CreateUserAsync();
        var bank = await user.Client.CreateWalletAsync("Techcombank", 1_000_000);

        var response = await user.Client.PutAsJsonAsync($"/api/wallets/{bank.Id}",
            new { name = bank.Name, type = "bank", openingBalance = 1_000_000, color = bank.Color, isArchived = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var active = await user.Client.WalletsAsync();
        var all = await user.Client.WalletsAsync(includeArchived: true);
        Assert.DoesNotContain(active.Items, w => w.Id == bank.Id);
        Assert.Contains(all.Items, w => w.Id == bank.Id && w.IsArchived);
        Assert.Equal(0m, all.Total);
    }

    [Fact]
    public async Task LastActiveWallet_CannotBeArchivedOrDeleted()
    {
        var user = await factory.CreateUserAsync();
        var cash = (await user.Client.WalletsAsync()).Items.Single();

        var archive = await user.Client.PutAsJsonAsync($"/api/wallets/{cash.Id}",
            new { name = cash.Name, type = "cash", openingBalance = 0, color = cash.Color, isArchived = true });
        var delete = await user.Client.DeleteAsync($"/api/wallets/{cash.Id}");

        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);
        Assert.Equal("Không thể lưu trữ ví cuối cùng đang dùng", await archive.DetailAsync());
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteWallet_WithTransactions_Returns409_WithoutTransactions_Returns204()
    {
        var user = await factory.CreateUserAsync();
        var used = await user.Client.CreateWalletAsync("Đã dùng");
        var unused = await user.Client.CreateWalletAsync("Chưa dùng");
        var food = await user.Client.CategoryAsync("Ăn uống");
        await user.Client.ExpenseAsync(used.Id, food.Id, 10_000);

        var conflict = await user.Client.DeleteAsync($"/api/wallets/{used.Id}");
        var ok = await user.Client.DeleteAsync($"/api/wallets/{unused.Id}");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("Ví đã có giao dịch – hãy lưu trữ thay vì xoá", await conflict.DetailAsync());
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }

    [Fact]
    public async Task OtherUsersWallet_Returns404()
    {
        var owner = await factory.CreateUserAsync();
        var intruder = await factory.CreateUserAsync();
        var wallet = (await owner.Client.WalletsAsync()).Items.Single();

        var update = await intruder.Client.PutAsJsonAsync($"/api/wallets/{wallet.Id}", new { name = "Của tôi", type = "cash" });
        var delete = await intruder.Client.DeleteAsync($"/api/wallets/{wallet.Id}");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Categories_FilterByKind_SortedBySortOrder()
    {
        var user = await factory.CreateUserAsync();

        var expense = await user.Client.CategoriesAsync("expense");
        var income = await user.Client.CategoriesAsync("income");

        Assert.Equal(10, expense.Count);
        Assert.Equal("Ăn uống", expense[0].Name);
        Assert.All(expense, c => Assert.Equal(CategoryKind.Expense, c.Kind));
        Assert.Equal(6, income.Count);
        Assert.Equal("Lương", income[0].Name);
    }

    [Fact]
    public async Task CreateCategory_ThenRename_KindStaysSame()
    {
        var user = await factory.CreateUserAsync();

        var created = await user.Client.PostAsJsonAsync("/api/categories",
            new { name = "Cà phê", kind = "expense", icon = "coffee", color = "#92400E" });
        var dto = await created.ReadAsync<CategoryDto>();
        var renamed = await user.Client.PutAsJsonAsync($"/api/categories/{dto.Id}",
            new { name = "Cà phê sáng", kind = "income", icon = "coffee", color = "#92400e" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("#92400e", dto.Color); // chuẩn hoá chữ thường
        var after = await renamed.ReadAsync<CategoryDto>();
        Assert.Equal("Cà phê sáng", after.Name);
        Assert.Equal(CategoryKind.Expense, after.Kind);
    }

    [Fact]
    public async Task SameCategoryName_AllowedAcrossKinds_RejectedWithinKind()
    {
        var user = await factory.CreateUserAsync();

        var otherKind = await user.Client.PostAsJsonAsync("/api/categories",
            new { name = "Ăn uống", kind = "income", icon = "utensils", color = "#f97316" });
        var sameKind = await user.Client.PostAsJsonAsync("/api/categories",
            new { name = "ĂN UỐNG", kind = "expense", icon = "utensils", color = "#f97316" });

        Assert.Equal(HttpStatusCode.Created, otherKind.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameKind.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_InUse409_Unused204()
    {
        var user = await factory.CreateUserAsync();
        var cash = (await user.Client.WalletsAsync()).Items.Single();
        var food = await user.Client.CategoryAsync("Ăn uống");
        var travel = await user.Client.CategoryAsync("Đi lại");
        await user.Client.ExpenseAsync(cash.Id, food.Id, 50_000);

        var conflict = await user.Client.DeleteAsync($"/api/categories/{food.Id}");
        var ok = await user.Client.DeleteAsync($"/api/categories/{travel.Id}");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }
}
