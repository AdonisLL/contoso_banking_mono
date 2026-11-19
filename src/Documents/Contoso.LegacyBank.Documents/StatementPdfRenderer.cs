using System;
using System.IO;
using System.Linq;
using Contoso.LegacyBank.Shared;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Contoso.LegacyBank.Documents
{
    /// <summary>
    /// Renders a one-page PDF statement from a <see cref="StatementJob"/>. Uses PdfSharp
    /// (matching the original Contoso Legacy Bank Documents worker), a stand-in for whatever
    /// in-house or commercial PDF library a real legacy batch/worker process would have used.
    /// </summary>
    public static class StatementPdfRenderer
    {
        public static void Render(StatementJob job, string outputPath)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var titleFont = new XFont("Verdana", 16, XFontStyleEx.Bold);
            var headerFont = new XFont("Verdana", 10, XFontStyleEx.Bold);
            var bodyFont = new XFont("Verdana", 9, XFontStyleEx.Regular);

            double y = 40;
            gfx.DrawString("Contoso Legacy Bank - Account Statement", titleFont, XBrushes.Black, new XPoint(40, y));
            y += 30;

            gfx.DrawString($"Customer: {job.CustomerName} ({job.CustomerNumber})", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 16;
            gfx.DrawString($"Account: {job.AccountNumber} ({job.AccountType})", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 16;
            gfx.DrawString($"Period: {job.PeriodStart:yyyy-MM-dd} to {job.PeriodEnd:yyyy-MM-dd}", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 16;
            gfx.DrawString($"Closing balance: {job.ClosingBalance:C}", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 26;

            gfx.DrawString("Date", headerFont, XBrushes.Black, new XPoint(40, y));
            gfx.DrawString("Type", headerFont, XBrushes.Black, new XPoint(140, y));
            gfx.DrawString("Description", headerFont, XBrushes.Black, new XPoint(250, y));
            gfx.DrawString("Amount", headerFont, XBrushes.Black, new XPoint(420, y));
            gfx.DrawString("Balance", headerFont, XBrushes.Black, new XPoint(490, y));
            y += 16;

            foreach (var tx in job.Transactions.OrderBy(t => t.CreatedUtc))
            {
                if (y > page.Height.Point - 60)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 40;
                }

                gfx.DrawString(tx.CreatedUtc.ToString("yyyy-MM-dd"), bodyFont, XBrushes.Black, new XPoint(40, y));
                gfx.DrawString(tx.TransactionType, bodyFont, XBrushes.Black, new XPoint(140, y));
                gfx.DrawString(tx.Description ?? string.Empty, bodyFont, XBrushes.Black, new XPoint(250, y));
                gfx.DrawString(tx.Amount.ToString("C"), bodyFont, XBrushes.Black, new XPoint(420, y));
                gfx.DrawString(tx.BalanceAfter.ToString("C"), bodyFont, XBrushes.Black, new XPoint(490, y));
                y += 14;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppPaths.DocumentsOutputRoot);
            document.Save(outputPath);
        }
    }
}
