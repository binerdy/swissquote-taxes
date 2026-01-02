# Swissquote Analyzer

A .NET CLI tool for extracting tax-relevant data from Swissquote bank statements. Provide your annual account statement PDF and get a comprehensive report for tax filing purposes.

## Overview

This tool helps you prepare your tax filing by:
- Extracting dividend income with gross amounts and taxes withheld
- Calculating trading fees and commissions
- Grouping dividends by currency for easy tax form completion
- Generating a clean PDF report with all tax-relevant information

## Features

- **PDF Extraction**: Extracts transaction data from Swissquote annual account statements (Kontoauszug) to JSON
- **Tax-Focused Reports**: Generates PDF reports with dividend income, withheld taxes, and trading fees
- **Currency Grouping**: Groups dividends by currency (CHF, USD, EUR) for tax form sections
- **Accurate Parsing**: Position-based extraction ensures precise data from PDF tables
- **ISIN Mapping**: Automatically maps ISINs to dividend transactions for complete security information

## Requirements

- .NET 10.0 SDK
- Swissquote PDF bank statements (Kontoauszug)

## Installation

```bash
git clone <repository-url>
cd swissquote-analyzer
dotnet restore
```

## Usage

### Extract Transactions from PDF

Extract all transactions from a Swissquote PDF statement to JSON:

```bash
dotnet run --project Swissquote.Analyzer.Cli extract --input kontoauszug_2024.pdf --output output.json
```

**Extracted Data:**
- Date and Value Date
- Transaction Type (Kauf, Verkauf, Dividende, etc.)
- Description
- ISIN (when available)
- Amount (gross for dividends)
- Tax (for dividends)
- Currency

### Generate Tax Report

Create a PDF report with all tax-relevant information:

```bash
dotnet run --project Swissquote.Analyzer.Cli insights --input output.json --output tax_report.pdf
```

**Report Contents:**
- Account summary (holder, IBAN, period)
- Securities with dividends grouped by currency (CHF, USD, EUR)
- Gross dividend amounts (Betrag) per security
- Taxes withheld (Taxen) per security
- Total dividends and taxes per currency
- Transaction fees and commissions
- Transaction type breakdown

**Use Case:** Use this report to fill in your annual tax return with accurate dividend income and withholding tax information.

## Project Structure

```
swissquote-analyzer/
├── Swissquote.Analyzer.Cli/    # CLI entry point
├── Extractor/                   # PDF extraction logic
│   ├── Pdf.cs                  # Row-boundary aware PDF parser
│   └── Models/                 # Data models
├── Insights/                    # Report generation
│   ├── InsightsGenerator.cs    # Analytics engine
│   ├── PdfReportGenerator.cs   # QuestPDF report builder
│   └── Models/                 # Data models
└── README.md
```

## Data Model

### Transaction
```json
{
  "date": "2024-03-18T00:00:00",
  "valueDate": "2024-03-18T00:00:00",
  "transactionType": "Dividende",
  "description": "... Dividende",
  "reference": "...",
  "isin": "...",
  "amount": 1.00,
  "currency": "CHF",
  "tax": 0.10
}
```

**Field Descriptions:**
- `amount`: Gross amount (for dividends: Betrag before taxes)
- `tax`: Tax amount deducted (for dividends: Taxen)
- `currency`: Transaction currency (CHF, USD, EUR)
- Net dividend = amount - tax

## Technical Details

### PDF Parsing Strategy

The extractor uses a **row-boundary aware** approach:
1. Identifies all date positions in the first column
2. Calculates row boundaries (90% distance to next row)
3. Extracts words within each row's Y-coordinate range
4. Maps words to columns by X-position

### Dividend Amount Extraction

For dividend transactions, the tool extracts:
- **Betrag** (gross dividend) → `amount` field
- **Taxen** (taxes) → `tax` field
- Net dividend can be calculated as: `amount - tax`

### ISIN Mapping

Dividends without ISINs are mapped by:
1. Extracting ticker symbols from buy/sell transactions
2. Matching ticker in dividend descriptions
3. Assigning corresponding ISIN to the dividend

## Dependencies

- **UglyToad.PdfPig** (0.1.9): PDF text extraction with positioning
- **QuestPDF** (2024.12.3): PDF report generation
- **System.CommandLine** (2.0.1): CLI framework

## Examples

### Complete Tax Report Workflow

```bash
# Step 1: Extract transactions from your annual Swissquote statement
dotnet run --project Swissquote.Analyzer.Cli extract --input kontoauszug_2024.pdf --output transactions.json

# Step 2: Generate tax report PDF
dotnet run --project Swissquote.Analyzer.Cli insights --input transactions.json --output tax_report_2024.pdf
```

The generated PDF contains all dividend income and tax information grouped by currency, ready for your tax filing.

### Sample Output

```
Summary:
  Account Holder: [Account Holder Name]
  IBAN: [IBAN Number]
  Period: 01.01.2024 - 31.12.2024
  Securities analyzed: 37

Dividends:
  Securities with dividends: 11
  Total dividends: 2,132.38

Fees & Taxes:
  Total commissions: 191,044.00
  Total taxes: 64,251.00

Transaction types:
  Kauf: 72
  Dividende: 23
  Verkauf: 22
```

## License

GNU General Public License v3.0
