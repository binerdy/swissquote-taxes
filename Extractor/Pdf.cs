using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Encodings.Web;
using Extractor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Extractor;

public class Pdf
{
    public static void Extract(FileInfo inputFile, FileInfo outputFile)
    {
        try
        {
            Console.WriteLine($"Extracting data from: {inputFile.FullName}");

            var pdf = new Pdf();
            var statement = pdf.ExtractStatement(inputFile.FullName);

            pdf.SaveAsJson(statement, outputFile.FullName);

            Console.WriteLine($"Successfully saved to: {outputFile.FullName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static void AnalyzeStructure(FileInfo inputFile)
    {
        try
        {
            Console.WriteLine($"Analyzing PDF structure: {inputFile.FullName}");
            Console.WriteLine(new string('=', 100));

            using var document = PdfDocument.Open(inputFile.FullName);

            Console.WriteLine($"Total Pages: {document.NumberOfPages}\n");

            foreach (var page in document.GetPages())
            {
                Console.WriteLine($"\n{'=',-100}");
                Console.WriteLine($"PAGE {page.Number} (Size: {page.Width:F1} x {page.Height:F1})");
                Console.WriteLine($"{'=',-100}\n");

                // Get full text
                Console.WriteLine("--- FULL TEXT ---");
                Console.WriteLine(page.Text);

                Console.WriteLine("\n--- LINE BY LINE ---");
                var lines = page.Text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Console.WriteLine($"[{i,3}] {line}");
                    }
                }

                Console.WriteLine("\n--- WORDS WITH POSITIONS (First 100) ---");
                var words = page.GetWords().Take(100);
                foreach (var word in words)
                {
                    Console.WriteLine($"[Y:{word.BoundingBox.Bottom,6:F1} X:{word.BoundingBox.Left,6:F1}] '{word.Text}'");
                }

                Console.WriteLine($"\n{'-',-100}\n");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    public SwissquoteStatement ExtractStatement(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        using var document = PdfDocument.Open(pdfPath);
        var statement = new SwissquoteStatement();

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // TODO: Parse the Swissquote-specific format
            // This is a placeholder - you'll need to adapt this to your specific PDF format
            ParsePage(page, statement);
        }

        // Post-process: Map ISINs to dividend transactions based on ticker
        MapIsinsToDividends(statement);

        return statement;
    }

    private void ParsePage(Page page, SwissquoteStatement statement)
    {
        var text = page.Text;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // Extract account info from first pages
        if (page.Number <= 2)
        {
            ExtractAccountInfo(lines, statement);
        }

        // Extract transactions
        ExtractTransactions(page, statement);
    }

    private void ExtractAccountInfo(List<string> lines, SwissquoteStatement statement)
    {
        var fullText = string.Join(" ", lines);

        // Extract IBAN
        var ibanMatch = System.Text.RegularExpressions.Regex.Match(fullText, @"IBAN\s*:\s*([A-Z]{2}\d{2}\s*\d{4}\s*\d{4}\s*\d{4}\s*\d{4}\s*\d)");
        if (ibanMatch.Success)
        {
            statement.AccountNumber = ibanMatch.Groups[1].Value.Replace(" ", "");
        }

        // Extract account holder
        var holderMatch = System.Text.RegularExpressions.Regex.Match(fullText, @"(Herrn|Frau)\s+([A-ZÄÖÜa-zäöü\s]+?)(?=IBAN|Vom)");
        if (holderMatch.Success)
        {
            statement.AccountHolder = holderMatch.Value.Trim();
        }

        // Extract period
        var periodMatch = System.Text.RegularExpressions.Regex.Match(fullText, @"Vom\s+(\d{2}\.\d{2}\.\d{4})\s+bis\s+(\d{2}\.\d{2}\.\d{4})");
        if (periodMatch.Success)
        {
            if (DateTime.TryParseExact(periodMatch.Groups[1].Value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var start))
            {
                statement.PeriodStart = start;
            }
            if (DateTime.TryParseExact(periodMatch.Groups[2].Value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var end))
            {
                statement.PeriodEnd = end;
                statement.StatementDate = end;
            }
        }
    }

    private void ExtractTransactions(Page page, SwissquoteStatement statement)
    {
        var words = page.GetWords().ToList();

        // Find transaction rows by looking for dates in the first column (X around 31)
        var datePattern = new System.Text.RegularExpressions.Regex(@"^\d{2}\.\d{2}\.\d{4}$");

        // First, find all date positions to determine row boundaries
        var dateIndices = new List<int>();
        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (word.BoundingBox.Left >= 30 && word.BoundingBox.Left <= 35 && datePattern.IsMatch(word.Text))
            {
                dateIndices.Add(i);
            }
        }

        // Parse each transaction using row boundaries
        for (int i = 0; i < dateIndices.Count; i++)
        {
            int currentDateIndex = dateIndices[i];
            int? nextDateIndex = i < dateIndices.Count - 1 ? dateIndices[i + 1] : null;
            
            var transaction = ParseTransaction(words, currentDateIndex, nextDateIndex);
            if (transaction != null)
            {
                statement.Transactions.Add(transaction);
            }
        }
    }

    private Transaction? ParseTransaction(List<UglyToad.PdfPig.Content.Word> words, int dateIndex, int? nextDateIndex)
    {
        var datePattern = new Regex(@"^\d{2}\.\d{2}\.\d{4}$");
        var dateWord = words[dateIndex];
        var yPosition = dateWord.BoundingBox.Bottom;

        // Determine the Y boundary for this row
        // Swissquote tables typically have ~50-60 points per row, but multi-line descriptions can span further
        // Use halfway point to next row, or 40 points if it's the last row
        double maxY = yPosition - 40; // Default: 40 points below for last row
        if (nextDateIndex.HasValue)
        {
            var nextY = words[nextDateIndex.Value].BoundingBox.Bottom;
            var rowHeight = Math.Abs(yPosition - nextY);
            // Use 90% of the distance to next row to avoid overlap
            maxY = yPosition - (rowHeight * 0.9);
        }

        // Get all words for this transaction row only (between current and next date)
        var transactionWords = words
            .Where(w => w.BoundingBox.Bottom <= yPosition + 2 && w.BoundingBox.Bottom >= maxY) // +2 for tolerance
            .OrderBy(w => w.BoundingBox.Bottom) // Top to bottom
            .ThenBy(w => w.BoundingBox.Left) // Left to right
            .ToList();

        var transaction = new Transaction();

        // Parse date
        if (DateTime.TryParseExact(dateWord.Text, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            transaction.Date = date;
        }

        // Extract description and transaction type (second column, X around 78)
        var descWords = transactionWords
            .Where(w => w.BoundingBox.Left >= 75 && w.BoundingBox.Left <= 200)
            .OrderBy(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (descWords.Any())
        {
            transaction.Description = string.Join(" ", descWords.Select(w => w.Text));
            
            // Extract transaction type - find the type word that's on the same line as the date
            // Use very strict Y position matching (within 1 point) to avoid picking up from other rows
            var knownTypes = new[] { "Kauf", "Verkauf", "Währungsumtausch", "Dividende", "Sollzinsen", 
                                      "Automatisierter", "Anfangsbestand", "Schlussbilanz", "Kapitalgewinn" };
            
            // Get words on the exact same Y position as the date (within 0.5 points)
            var sameLine = descWords
                .Where(w => Math.Abs(w.BoundingBox.Bottom - yPosition) < 0.5)
                .OrderBy(w => w.BoundingBox.Left) // Left to right
                .ToList();
            
            // Find the FIRST known transaction type (leftmost on the line)
            var typeWord = sameLine.FirstOrDefault(w => knownTypes.Contains(w.Text));
            if (typeWord != null)
            {
                transaction.TransactionType = typeWord.Text;
            }
        }

        // Extract reference (column around X 226)
        var refWord = transactionWords.FirstOrDefault(w => w.BoundingBox.Left >= 220 && w.BoundingBox.Left <= 270);
        if (refWord != null && long.TryParse(refWord.Text, out _))
        {
            transaction.Reference = refWord.Text;
        }

        // Extract amounts (BELASTUNG around X 279, GUTSCHRIFT around X 351)
        var belastungWord = transactionWords.FirstOrDefault(w => w.BoundingBox.Left >= 275 && w.BoundingBox.Left <= 340);
        var gutschriftWord = transactionWords.FirstOrDefault(w => w.BoundingBox.Left >= 345 && w.BoundingBox.Left <= 420);

        decimal amount = 0;
        if (belastungWord != null && TryParseSwissAmount(belastungWord.Text, out var belastung))
        {
            amount = -belastung; // Debit is negative
        }
        else if (gutschriftWord != null && TryParseSwissAmount(gutschriftWord.Text, out var gutschrift))
        {
            amount = gutschrift; // Credit is positive
        }
        transaction.Amount = amount;

        // Extract value date (column around X 433)
        var valueDateWord = transactionWords.FirstOrDefault(w =>
            w.BoundingBox.Left >= 425 && w.BoundingBox.Left <= 475 &&
            datePattern.IsMatch(w.Text));
        if (valueDateWord != null && DateTime.TryParseExact(valueDateWord.Text, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var valueDate))
        {
            transaction.ValueDate = valueDate;
        }

        // Determine currency from nearby context
        var currencyWords = transactionWords.Where(w => w.Text is "USD" or "CHF" or "EUR").ToList();
        transaction.Currency = currencyWords.FirstOrDefault()?.Text ?? "USD";

        // Extract ISIN (look for "ISIN:" followed by alphanumeric code)
        var isinIndex = transactionWords.FindIndex(w => w.Text == "ISIN:");
        if (isinIndex >= 0 && isinIndex < transactionWords.Count - 1)
        {
            var isinCandidate = transactionWords[isinIndex + 1].Text;
            // ISIN format: 2 letters + 10 alphanumeric
            if (isinCandidate.Length == 12 && char.IsLetter(isinCandidate[0]) && char.IsLetter(isinCandidate[1]))
            {
                transaction.Isin = isinCandidate;
            }
        }
        
        // If no ISIN found via "ISIN:" keyword, try to extract from description using regex
        // This helps with dividends where ISIN might appear without the keyword
        if (string.IsNullOrEmpty(transaction.Isin) && !string.IsNullOrEmpty(transaction.Description))
        {
            var isinMatch = Regex.Match(transaction.Description, @"\b([A-Z]{2}[A-Z0-9]{10})\b");
            if (isinMatch.Success)
            {
                transaction.Isin = isinMatch.Groups[1].Value;
            }
        }

        // For dividends, extract the "Betrag" (gross amount) instead of "Total" (net amount)
        if (transaction.TransactionType == "Dividende" && !string.IsNullOrEmpty(transaction.Description))
        {
            var betragMatch = Regex.Match(transaction.Description, @"Betrag:\s+[A-Z]{3}\s+([\d']+\.?\d*)");
            if (betragMatch.Success)
            {
                var betragStr = betragMatch.Groups[1].Value.Replace("'", "");
                if (decimal.TryParse(betragStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var betragAmount))
                {
                    transaction.Amount = betragAmount;
                }
            }
        }

        // Extract tax amount from description for dividends
        if (!string.IsNullOrEmpty(transaction.Description))
        {
            var taxMatch = Regex.Match(transaction.Description, @"Taxen:\s+[A-Z]{3}\s+([\d']+\.?\d*)");
            if (taxMatch.Success)
            {
                var taxAmountStr = taxMatch.Groups[1].Value.Replace("'", "");
                if (decimal.TryParse(taxAmountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var taxAmount))
                {
                    transaction.Tax = taxAmount;
                }
            }
        }

        return transaction;
    }

    private void MapIsinsToDividends(SwissquoteStatement statement)
    {
        // Build a mapping from security ticker to ISIN from buy/sell transactions
        var tickerToIsin = new Dictionary<string, string>();
        
        foreach (var transaction in statement.Transactions)
        {
            if (!string.IsNullOrEmpty(transaction.Isin) && !string.IsNullOrEmpty(transaction.Description))
            {
                // Extract ticker from description - pattern: "NAME (TICKER)" followed by Kauf/Verkauf
                var tickerMatch = Regex.Match(transaction.Description, @"([A-Z][A-Za-z0-9\s\.-]+)\s*\(([A-Z]{2,6})\)\s*(?:Kauf|Verkauf)");
                if (tickerMatch.Success)
                {
                    var ticker = tickerMatch.Groups[2].Value;
                    if (!tickerToIsin.ContainsKey(ticker))
                    {
                        tickerToIsin[ticker] = transaction.Isin;
                    }
                }
            }
        }
        
        // Apply mapping to dividend transactions without ISINs
        foreach (var transaction in statement.Transactions)
        {
            if (string.IsNullOrEmpty(transaction.Isin) && 
                transaction.TransactionType == "Dividende" && 
                !string.IsNullOrEmpty(transaction.Description))
            {
                // Extract ticker from dividend description - pattern: "NAME (TICKER) Dividende"
                var tickerMatch = Regex.Match(transaction.Description, @"([A-Z][A-Za-z0-9\s\.-]+)\s*\(([A-Z]{2,6})\)\s*Dividende");
                if (tickerMatch.Success)
                {
                    var ticker = tickerMatch.Groups[2].Value;
                    if (tickerToIsin.TryGetValue(ticker, out var isin))
                    {
                        transaction.Isin = isin;
                    }
                }
            }
        }
    }

    private bool TryParseSwissAmount(string text, out decimal amount)
    {
        amount = 0;

        // Swiss format uses ' as thousand separator and . as decimal separator
        // Example: 1'234.56 or -2'658.75
        var cleaned = text.Replace("'", "").Replace(" ", "");

        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out amount);
    }

    public string ConvertToJson(SwissquoteStatement statement, bool indent = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indent,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(statement, options);
    }

    public void SaveAsJson(SwissquoteStatement statement, string outputPath, bool indent = true)
    {
        var json = ConvertToJson(statement, indent);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }
}
