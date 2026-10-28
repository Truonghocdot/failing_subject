using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Contracts;

namespace backend.Tests;

/// <summary>Gọi API ví / danh mục / giao dịch cho gọn trong test.</summary>
public static class LedgerHelpers
{
    // Giống cấu hình server: enum chữ thường
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<WalletListDto> WalletsAsync(this HttpClient client, bool includeArchived = false) =>
        (await client.GetFromJsonAsync<WalletListDto>($"/api/wallets?includeArchived={includeArchived}", Json))!;

    public static async Task<WalletDto> CreateWalletAsync(this HttpClient client, string name, decimal opening = 0, string type = "bank")
    {
        var response = await client.PostAsJsonAsync("/api/wallets", new { name, type, openingBalance = opening });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<WalletDto>();
    }

    public static async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(this HttpClient client, string? kind = null) =>
        (await client.GetFromJsonAsync<List<CategoryDto>>($"/api/categories?kind={kind}", Json))!;

    public static async Task<CategoryDto> CategoryAsync(this HttpClient client, string name) =>
        (await client.CategoriesAsync()).Single(c => c.Name == name);

    public static Task<HttpResponseMessage> PostTransactionAsync(this HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/transactions", body);

    public static async Task<TransactionDto> AddTransactionAsync(this HttpClient client, object body)
    {
        var response = await client.PostTransactionAsync(body);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<TransactionDto>();
    }

    public static async Task<TransactionDto> ExpenseAsync(this HttpClient client, long walletId, long categoryId, decimal amount,
        string date = "2026-10-15", string? note = null) =>
        await client.AddTransactionAsync(new { kind = "expense", walletId, categoryId, amount, occurredOn = date, note });

    public static async Task<TransactionDto> IncomeAsync(this HttpClient client, long walletId, long categoryId, decimal amount,
        string date = "2026-10-15", string? note = null) =>
        await client.AddTransactionAsync(new { kind = "income", walletId, categoryId, amount, occurredOn = date, note });

    public static async Task<TransactionDto> TransferAsync(this HttpClient client, long walletId, long toWalletId, decimal amount,
        string date = "2026-10-15") =>
        await client.AddTransactionAsync(new { kind = "transfer", walletId, toWalletId, amount, occurredOn = date });

    public static async Task<PagedTransactions> ListAsync(this HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PagedTransactions>($"/api/transactions?{query}", Json))!;

    public static decimal Balance(this WalletListDto list, long walletId) => list.Items.Single(w => w.Id == walletId).Balance;
}
