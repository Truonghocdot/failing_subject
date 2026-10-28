using backend.Data.Entities;

namespace backend.Data;

/// <summary>Ví + danh mục tạo sẵn khi đăng ký (docs/02-nghiep-vu.md §1, §3.2).</summary>
public static class DefaultData
{
    public static readonly IReadOnlyDictionary<WalletType, string> WalletColors = new Dictionary<WalletType, string>
    {
        [WalletType.Cash] = "#16a34a",
        [WalletType.Bank] = "#2563eb",
        [WalletType.Ewallet] = "#db2777",
        [WalletType.Credit] = "#7c3aed",
    };

    private static readonly (string Name, string Icon, string Color)[] ExpenseCategories =
    [
        ("Ăn uống", "utensils", "#f97316"),
        ("Đi lại", "bus", "#0ea5e9"),
        ("Nhà ở", "house", "#8b5cf6"),
        ("Hoá đơn & tiện ích", "receipt", "#eab308"),
        ("Mua sắm", "shopping-bag", "#ec4899"),
        ("Giải trí", "clapperboard", "#6366f1"),
        ("Sức khoẻ", "heart-pulse", "#ef4444"),
        ("Giáo dục", "graduation-cap", "#14b8a6"),
        ("Gia đình & con cái", "baby", "#f43f5e"),
        ("Chi khác", "circle-ellipsis", "#64748b"),
    ];

    private static readonly (string Name, string Icon, string Color)[] IncomeCategories =
    [
        ("Lương", "banknote", "#16a34a"),
        ("Thưởng", "gift", "#22c55e"),
        ("Kinh doanh", "store", "#0d9488"),
        ("Đầu tư", "trending-up", "#0284c7"),
        ("Được cho / tặng", "hand-heart", "#d946ef"),
        ("Thu khác", "circle-plus", "#64748b"),
    ];

    public static int CategoryCount => ExpenseCategories.Length + IncomeCategories.Length;

    public static void AddTo(User user)
    {
        user.Wallets.Add(new Wallet { Name = "Tiền mặt", Type = WalletType.Cash, Color = WalletColors[WalletType.Cash] });

        user.Categories.AddRange(ExpenseCategories.Select((c, i) => Create(CategoryKind.Expense, c, i)));
        user.Categories.AddRange(IncomeCategories.Select((c, i) => Create(CategoryKind.Income, c, i)));
    }

    private static Category Create(CategoryKind kind, (string Name, string Icon, string Color) c, int order) =>
        new() { Kind = kind, Name = c.Name, Icon = c.Icon, Color = c.Color, SortOrder = order };
}
