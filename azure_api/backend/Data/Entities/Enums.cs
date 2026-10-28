namespace backend.Data.Entities;

// Lưu xuống DB và trả JSON dạng chữ thường: "cash", "expense"... (docs/06-database.md §1)

public enum WalletType
{
    Cash,
    Bank,
    Ewallet,
    Credit,
}

public enum CategoryKind
{
    Expense,
    Income,
}

public enum TransactionKind
{
    Expense,
    Income,
    Transfer,
}
