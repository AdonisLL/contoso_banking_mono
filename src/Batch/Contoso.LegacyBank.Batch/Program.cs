using System;
using System.IO;
using System.Linq;
using System.Text;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Batch
{
    /// <summary>
    /// Nightly-style batch importer: claims every CSV file in the Input folder, posts valid
    /// rows (duplicate-checked by ExternalId) through the Batch stored procedures, writes a
    /// reconciliation report, and archives or error-files the source. Intended to be run once
    /// per invocation (e.g. by Windows Task Scheduler) rather than as a long-lived process.
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            var logger = new SimpleLogger("Batch");
            AppPaths.EnsureAll();

            var repository = new BatchRepository();
            var inputFiles = Directory.GetFiles(AppPaths.BatchInputRoot, "*.csv");

            if (inputFiles.Length == 0)
            {
                Console.WriteLine($"No CSV files found in {AppPaths.BatchInputRoot}.");
                logger.Info("No CSV files found to import.");
                return;
            }

            foreach (var inputFile in inputFiles)
            {
                ImportFile(inputFile, repository, logger);
            }
        }

        private static void ImportFile(string inputFile, BatchRepository repository, SimpleLogger logger)
        {
            var fileName = Path.GetFileName(inputFile);
            logger.Info($"Starting import of {fileName}");

            var batchId = repository.CreateBatch(fileName);
            var lines = File.ReadAllLines(inputFile)
                .Select((line, index) => (Line: line, RowNumber: index + 1))
                .Skip(1) // header row
                .Where(l => !string.IsNullOrWhiteSpace(l.Line))
                .ToList();

            var report = new StringBuilder();
            report.AppendLine($"Contoso Legacy Bank - Batch import report");
            report.AppendLine($"File: {fileName}");
            report.AppendLine($"Batch ID: {batchId}");
            report.AppendLine($"Imported (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            var hadFatalError = false;

            foreach (var (line, rowNumber) in lines)
            {
                try
                {
                    var row = BatchCsvParser.Parse(line);
                    var (status, errorMessage) = repository.ReconcileRow(
                        batchId, rowNumber, row.ExternalId, row.AccountNumber, row.Amount, row.TransactionType);

                    report.AppendLine(status == "Posted"
                        ? $"Row {rowNumber}: Posted ({row.ExternalId}, {row.AccountNumber}, {row.Amount:C})"
                        : $"Row {rowNumber}: {status} - {errorMessage}");
                }
                catch (FormatException ex)
                {
                    repository.RecordUnparsableRow(batchId, rowNumber, line, ex.Message);
                    report.AppendLine($"Row {rowNumber}: Error - {ex.Message}");
                }
                catch (Exception ex)
                {
                    // A row-level failure that isn't a parse problem (e.g. a transient DB error)
                    // is still recorded, but we stop trusting this file's overall integrity.
                    logger.Error($"Unexpected error processing row {rowNumber} of {fileName}.", ex);
                    repository.RecordUnparsableRow(batchId, rowNumber, line, "Unexpected error: " + ex.Message);
                    hadFatalError = true;
                }
            }

            var reportFileName = $"{batchId}_{Path.GetFileNameWithoutExtension(fileName)}.report.txt";
            var reportPath = Path.Combine(AppPaths.BatchReportsRoot, reportFileName);

            repository.CompleteBatch(batchId, reportPath);
            var (total, success, duplicate, error) = repository.GetBatchSummary(batchId);

            report.AppendLine();
            report.AppendLine($"Total rows: {total}, Posted: {success}, Duplicates: {duplicate}, Errors: {error}");
            File.WriteAllText(reportPath, report.ToString());

            var destinationRoot = hadFatalError || error > 0 ? AppPaths.BatchErrorRoot : AppPaths.BatchArchiveRoot;
            var destinationPath = Path.Combine(destinationRoot, fileName);
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(inputFile, destinationPath);

            logger.Info($"Completed import of {fileName}: {success} posted, {duplicate} duplicates, {error} errors. Report: {reportPath}");
            Console.WriteLine($"{fileName}: {success} posted, {duplicate} duplicates, {error} errors (report: {reportPath})");
        }
    }
}
