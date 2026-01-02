using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Insights.Models;

namespace Insights;

public class PdfReportGenerator
{
    public static void GeneratePdfReport(PortfolioInsights insights, string outputPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text("Swissquote Kontoauszug Analyse")
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(10);

                        // Account Information
                        column.Item().Element(container => ComposeAccountInfo(container, insights));

                        // Securities grouped by ISIN
                        column.Item().Element(container => ComposeSecuritiesTable(container, insights));
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Seite ");
                        x.CurrentPageNumber();
                        x.Span(" von ");
                        x.TotalPages();
                    });
            });
        })
        .GeneratePdf(outputPath);
    }

    private static void ComposeAccountInfo(IContainer container, PortfolioInsights insights)
    {
        container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
        {
            column.Spacing(5);
            
            column.Item().Text("Kontoinformationen").SemiBold().FontSize(14);
            column.Item().Text($"Kontoinhaber: {insights.AccountHolder}");
            column.Item().Text($"IBAN: {insights.AccountNumber}");
            column.Item().Text($"Periode: {insights.PeriodStart:dd.MM.yyyy} - {insights.PeriodEnd:dd.MM.yyyy}");
        });
    }

    private static void ComposeSecuritiesTable(IContainer container, PortfolioInsights insights)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            
            column.Item().Text("Dividenden-Übersicht").SemiBold().FontSize(14);

            // Group securities with dividends by currency
            var securitiesWithDividends = insights.Securities
                .Where(s => s.TotalDividends > 0)
                .GroupBy(s => s.Currency)
                .OrderBy(g => g.Key);

            foreach (var currencyGroup in securitiesWithDividends)
            {
                var currency = currencyGroup.Key;
                var securities = currencyGroup.OrderByDescending(s => s.TotalDividends).ToList();

                // Currency section header
                column.Item().PaddingTop(15).Text($"Dividenden in {currency}").SemiBold().FontSize(12);
                
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // ISIN
                        columns.RelativeColumn(4); // Name
                        columns.RelativeColumn(2); // Div. Anzahl
                        columns.RelativeColumn(3); // Dividenden
                        columns.RelativeColumn(3); // Taxen
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("ISIN").SemiBold();
                        header.Cell().Element(CellStyle).Text("Wertpapier").SemiBold();
                        header.Cell().Element(CellStyle).AlignRight().Text("Div. Anz.").SemiBold();
                        header.Cell().Element(CellStyle).AlignRight().Text("Dividenden").SemiBold();
                        header.Cell().Element(CellStyle).AlignRight().Text("Taxen").SemiBold();
                    });

                    // Rows
                    foreach (var security in securities)
                    {
                        table.Cell().Element(CellStyle).Text(security.Isin).FontSize(8);
                        table.Cell().Element(CellStyle).Text(security.SecurityName);
                        table.Cell().Element(CellStyle).AlignRight().Text(security.DividendTransactions.ToString());
                        table.Cell().Element(CellStyle).AlignRight().Text($"{security.TotalDividends:N2}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{security.TotalTaxes:N2}");
                    }

                    // Total row for this currency
                    var totalDividends = securities.Sum(s => s.TotalDividends);
                    var totalTaxes = securities.Sum(s => s.TotalTaxes);
                    table.Cell().ColumnSpan(2).Element(CellStyle).Text($"Gesamt {currency}").SemiBold();
                    table.Cell().Element(CellStyle).Text("");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{totalDividends:N2}").SemiBold();
                    table.Cell().Element(CellStyle).AlignRight().Text($"{totalTaxes:N2}").SemiBold();
                });
            }
        });
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
    }
}
