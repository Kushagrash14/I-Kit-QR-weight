using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

public class ReportService : IReportService
{
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> ExportToExcelAsync(List<WeighRecord> records)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Weigh Records");

        string[] headers = { "Kit Number", "Product", "Qty", "Weight (kg)", "Result", "Fail Reason", "Date", "Time", "Operator", "QR ID", "Printer Status", "Remarks" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x4E, 0x79);
        headerRow.Style.Font.FontColor = XLColor.White;

        var row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.KitNumber;
            ws.Cell(row, 2).Value = r.ProductName;
            ws.Cell(row, 3).Value = r.Quantity;
            ws.Cell(row, 4).Value = r.WeightKg;
            ws.Cell(row, 5).Value = r.Result.ToString();
            ws.Cell(row, 6).Value = r.FailReason == FailReason.None ? "" : r.FailReason.ToString();
            ws.Cell(row, 7).Value = r.DateText;
            ws.Cell(row, 8).Value = r.TimeText;
            ws.Cell(row, 9).Value = r.OperatorName;
            ws.Cell(row, 10).Value = r.QrId;
            ws.Cell(row, 11).Value = r.PrinterStatus;
            ws.Cell(row, 12).Value = r.Remarks;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    public Task<byte[]> ExportToPdfAsync(List<WeighRecord> records, string reportTitle)
    {
        var passCount = records.Count(r => r.Result == WeighResult.Pass);
        var failCount = records.Count(r => r.Result == WeighResult.Fail);
        var total = records.Count;
        var passPct = total == 0 ? 0 : Math.Round(passCount * 100.0 / total, 1);
        var failPct = total == 0 ? 0 : Math.Round(failCount * 100.0 / total, 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(reportTitle).FontSize(16).Bold();
                    col.Item().Text($"Generated: {DateTime.Now:dd-MM-yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).Text($"Total: {total}   PASS: {passCount} ({passPct}%)   FAIL: {failCount} ({failPct}%)").Bold();
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.4f); // Kit Number
                        columns.RelativeColumn(2f);   // Product
                        columns.RelativeColumn(0.9f);  // Weight
                        columns.RelativeColumn(0.8f);  // Result
                        columns.RelativeColumn(1.2f);  // Reason
                        columns.RelativeColumn(0.9f);  // Date
                        columns.RelativeColumn(0.7f);  // Time
                        columns.RelativeColumn(1.1f);  // Operator
                        columns.RelativeColumn(1f);    // Printer
                    });

                    table.Header(header =>
                    {
                        foreach (var text in new[] { "Kit Number", "Product", "Weight", "Result", "Reason", "Date", "Time", "Operator", "Printer" })
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text(text).FontColor(Colors.White).Bold();
                    });

                    foreach (var r in records)
                    {
                        var bg = r.Result == WeighResult.Fail ? Colors.Red.Lighten4 : Colors.White;
                        table.Cell().Background(bg).Padding(3).Text(r.KitNumber);
                        table.Cell().Background(bg).Padding(3).Text(r.ProductName);
                        table.Cell().Background(bg).Padding(3).Text($"{r.WeightKg:0.000}");
                        table.Cell().Background(bg).Padding(3).Text(r.Result.ToString());
                        table.Cell().Background(bg).Padding(3).Text(r.FailReason == FailReason.None ? "-" : r.FailReason.ToString());
                        table.Cell().Background(bg).Padding(3).Text(r.DateText);
                        table.Cell().Background(bg).Padding(3).Text(r.TimeText);
                        table.Cell().Background(bg).Padding(3).Text(r.OperatorName);
                        table.Cell().Background(bg).Padding(3).Text(r.PrinterStatus);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
