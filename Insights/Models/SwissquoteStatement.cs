namespace Insights.Models;

public record SwissquoteStatement
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public DateTime StatementDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<Transaction> Transactions { get; set; } = new();
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
}

public record Transaction
{
    public DateTime Date { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? TransactionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Isin { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? Tax { get; set; }
}
