namespace Insights.Models;

public record SecurityInsights
{
    public string Isin { get; set; } = string.Empty;
    public string SecurityName { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int BuyTransactions { get; set; }
    public int SellTransactions { get; set; }
    public int DividendTransactions { get; set; }
    public decimal TotalBuyAmount { get; set; }
    public decimal TotalSellAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TotalDividends { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal TotalTaxes { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<DateTime> TransactionDates { get; set; } = new();
}

public record PortfolioInsights
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<SecurityInsights> Securities { get; set; } = new();
    public Dictionary<string, int> TransactionsByType { get; set; } = new();
    public Dictionary<string, decimal> AmountByCurrency { get; set; } = new();
    public decimal TotalCommissions { get; set; }
    public decimal TotalTaxes { get; set; }
}
