using System.Text.Json;
using System.Text.RegularExpressions;
using Insights.Models;

namespace Insights;

public class InsightsGenerator
{
    public static PortfolioInsights Generate(SwissquoteStatement statement)
    {
        var insights = new PortfolioInsights
        {
            AccountNumber = statement.AccountNumber,
            AccountHolder = statement.AccountHolder,
            PeriodStart = statement.PeriodStart,
            PeriodEnd = statement.PeriodEnd
        };

        // Group transactions by ISIN, or by extracted ticker for dividends without ISIN
        var transactionsByIdentifier = statement.Transactions
            .Where(t => !string.IsNullOrEmpty(t.Isin) || 
                       (t.TransactionType == "Dividende" && !string.IsNullOrEmpty(t.Description)))
            .Select(t => new 
            { 
                Transaction = t, 
                Identifier = GetSecurityIdentifier(t)
            })
            .Where(x => x.Identifier != null)
            .GroupBy(x => x.Identifier!);

        foreach (var group in transactionsByIdentifier)
        {
            var transactions = group.Select(x => x.Transaction).ToList();
            var securityInsight = GenerateSecurityInsights(group.Key, transactions);
            insights.Securities.Add(securityInsight);
        }

        // Transaction type statistics
        var typeGroups = statement.Transactions
            .Where(t => !string.IsNullOrEmpty(t.TransactionType))
            .GroupBy(t => t.TransactionType!);

        foreach (var group in typeGroups)
        {
            insights.TransactionsByType[group.Key] = group.Count();
        }

        // Amount by currency
        var currencyGroups = statement.Transactions
            .GroupBy(t => t.Currency);

        foreach (var group in currencyGroups)
        {
            insights.AmountByCurrency[group.Key] = group.Sum(t => Math.Abs(t.Amount));
        }

        // Extract total commissions and taxes from descriptions
        ExtractFeesFromTransactions(statement.Transactions, insights);

        return insights;
    }

    private static string? GetSecurityIdentifier(Transaction transaction)
    {
        // If ISIN exists, use it
        if (!string.IsNullOrEmpty(transaction.Isin))
            return transaction.Isin;
        
        // For dividends without ISIN, extract ticker from description
        if (transaction.TransactionType == "Dividende" && !string.IsNullOrEmpty(transaction.Description))
        {
            // Pattern: "NAME (TICKER) Dividende" or "NAME ORD (TICKER) Dividende"
            var match = Regex.Match(transaction.Description, @"([A-Z][A-Za-z0-9\s]+)\s*\(([A-Z]{2,6})\)\s*Dividende");
            if (match.Success)
            {
                return $"TICKER:{match.Groups[2].Value}"; // Use ticker as identifier
            }
        }
        
        return null;
    }

    private static SecurityInsights GenerateSecurityInsights(string identifier, List<Transaction> transactions)
    {
        // Check if identifier is a ticker (starts with "TICKER:") or an ISIN
        var isin = identifier.StartsWith("TICKER:") ? null : identifier;
        
        var insight = new SecurityInsights
        {
            Isin = isin ?? identifier, // Use identifier if ISIN is null
            TotalTransactions = transactions.Count,
            Currency = transactions.FirstOrDefault()?.Currency ?? "USD",
            TransactionDates = transactions.Select(t => t.Date).OrderBy(d => d).ToList()
        };

        // Extract security name from description
        // Look for pattern like "NESTLE N (NESN)" or company name with ticker
        var firstTransaction = transactions.FirstOrDefault();
        if (firstTransaction != null)
        {
            // Try to find security name with ticker pattern: "NAME (TICKER)"
            // Must start with a letter (not a number) to avoid capturing quantities
            var match = Regex.Match(firstTransaction.Description, @"([A-Z][A-Za-z0-9\s\.-]*\s+\([A-Z]{2,6}\))");
            if (match.Success)
            {
                insight.SecurityName = match.Groups[1].Value.Trim();
            }
            else
            {
                // Fallback: use identifier as name
                insight.SecurityName = identifier.StartsWith("TICKER:") 
                    ? identifier.Substring(7) // Remove "TICKER:" prefix
                    : identifier;
            }
        }

        foreach (var transaction in transactions)
        {
            if (transaction.TransactionType == "Kauf")
            {
                insight.BuyTransactions++;
                insight.TotalBuyAmount += Math.Abs(transaction.Amount);
                // Extract fees only from buy/sell transactions to avoid duplicates
                // Only extract if we have an actual ISIN (not a ticker identifier)
                if (isin != null)
                {
                    ExtractFeesFromDescription(transaction.Description, insight, isin);
                }
            }
            else if (transaction.TransactionType == "Verkauf")
            {
                insight.SellTransactions++;
                insight.TotalSellAmount += transaction.Amount;
                // Extract fees only from buy/sell transactions to avoid duplicates
                // Only extract if we have an actual ISIN (not a ticker identifier)
                if (isin != null)
                {
                    ExtractFeesFromDescription(transaction.Description, insight, isin);
                }
            }
            else if (transaction.TransactionType == "Dividende")
            {
                insight.DividendTransactions++;
                // Dividends are recorded as negative debits, so take absolute value
                insight.TotalDividends += Math.Abs(transaction.Amount);
                // Use explicitly extracted tax from transaction
                if (transaction.Tax.HasValue)
                {
                    insight.TotalTaxes += transaction.Tax.Value;
                }
            }
        }

        insight.NetAmount = insight.TotalSellAmount - insight.TotalBuyAmount;

        return insight;
    }

    private static void ExtractFeesFromDescription(string description, SecurityInsights insight, string isin)
    {
        // Extract fees for this specific transaction by finding them near the ISIN
        // Pattern: "ISIN: CH0012032048 ... Taxen: CHF 3.80 Kommission: CHF 30.85"
        var isinPattern = $@"ISIN:\s+{Regex.Escape(isin)}.*?(?:Taxen:\s+[A-Z]{{3}}\s+([\d']+\.?\d*))?.*?(?:Kommission:\s+[A-Z]{{3}}\s+([\d']+\.?\d*))?";
        var match = Regex.Match(description, isinPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            // Extract tax if present
            if (match.Groups[1].Success && decimal.TryParse(match.Groups[1].Value.Replace("'", ""), out var tax))
            {
                insight.TotalTaxes += tax;
            }
            
            // Extract commission if present
            if (match.Groups[2].Success && decimal.TryParse(match.Groups[2].Value.Replace("'", ""), out var commission))
            {
                insight.TotalCommissions += commission;
            }
        }
    }

    private static void ExtractFeesFromTransactions(List<Transaction> transactions, PortfolioInsights insights)
    {
        foreach (var transaction in transactions)
        {
            var commissionMatch = Regex.Match(transaction.Description, @"Kommission:\s+[A-Z]{3}\s+([\d']+\.?\d*)");
            if (commissionMatch.Success && decimal.TryParse(commissionMatch.Groups[1].Value.Replace("'", ""), out var commission))
            {
                insights.TotalCommissions += commission;
            }

            var taxMatch = Regex.Match(transaction.Description, @"Taxen:\s+[A-Z]{3}\s+([\d']+\.?\d*)");
            if (taxMatch.Success && decimal.TryParse(taxMatch.Groups[1].Value.Replace("'", ""), out var tax))
            {
                insights.TotalTaxes += tax;
            }
        }
    }

    public static SwissquoteStatement LoadStatement(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        return JsonSerializer.Deserialize<SwissquoteStatement>(json, options) 
            ?? throw new InvalidOperationException("Failed to deserialize statement");
    }

    public static void SaveInsights(PortfolioInsights insights, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(insights, options);
        File.WriteAllText(outputPath, json, System.Text.Encoding.UTF8);
    }
}
