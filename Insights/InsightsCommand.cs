using System.CommandLine;

namespace Insights;

public class InsightsCommand : Command
{
    public InsightsCommand() : base("insights", "Generate insights from extracted transaction data")
    {
        var inputOption = new Option<FileInfo>("--input", "-i")
        {
            Description = "Path to the input JSON file (from extract command)",
            Required = true
        };

        var outputOption = new Option<FileInfo>("--output", "-o")
        {
            Description = "Path to the output PDF file",
            Required = true
        };

        this.Options.Add(inputOption);
        this.Options.Add(outputOption);

        this.SetAction(async parsedOptions =>
        {
            var input = parsedOptions.GetRequiredValue(inputOption);
            var output = parsedOptions.GetRequiredValue(outputOption);

            if (!input.Exists)
            {
                Console.Error.WriteLine($"Input file not found: {input.FullName}");
                Environment.Exit(1);
            }

            Console.WriteLine($"Loading statement from: {input.FullName}");
            var statement = InsightsGenerator.LoadStatement(input.FullName);

            Console.WriteLine("Generating insights...");
            var insights = InsightsGenerator.Generate(statement);

            Console.WriteLine($"Creating PDF report: {output.FullName}");
            PdfReportGenerator.GeneratePdfReport(insights, output.FullName);

            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"  Account Holder: {insights.AccountHolder}");
            Console.WriteLine($"  IBAN: {insights.AccountNumber}");
            Console.WriteLine($"  Period: {insights.PeriodStart:dd.MM.yyyy} - {insights.PeriodEnd:dd.MM.yyyy}");
            Console.WriteLine($"  Securities analyzed: {insights.Securities.Count}");
            
            var totalDividends = insights.Securities.Sum(s => s.TotalDividends);
            var securitiesWithDividends = insights.Securities.Count(s => s.TotalDividends > 0);
            Console.WriteLine($"\nDividends:");
            Console.WriteLine($"  Securities with dividends: {securitiesWithDividends}");
            Console.WriteLine($"  Total dividends: {totalDividends:N2}");
            
            Console.WriteLine($"\nFees & Taxes:");
            Console.WriteLine($"  Total commissions: {insights.TotalCommissions:N2}");
            Console.WriteLine($"  Total taxes: {insights.TotalTaxes:N2}");
            
            Console.WriteLine($"\nTransaction types:");
            foreach (var (type, count) in insights.TransactionsByType.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {type}: {count}");
            }
            
            Console.WriteLine($"\nPDF report created successfully!");
        });
    }
}
