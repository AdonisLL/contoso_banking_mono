using System;
using System.IO;
using System.Threading;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Documents
{
    /// <summary>
    /// Polls the Statements "Pending" folder for job files, renders each into a PDF, and drops
    /// completion/failure metadata for the Statements API to pick up on its next status poll.
    /// This file-queue hand-off (rather than a managed queue) is a documented modernization seam.
    /// </summary>
    public static class Program
    {
        private static volatile bool _stopRequested;

        public static void Main()
        {
            var logger = new SimpleLogger("Documents");
            AppPaths.EnsureAll();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _stopRequested = true;
            };

            Console.WriteLine("Contoso Legacy Bank - Documents worker running. Press Ctrl+C to stop.");
            logger.Info("Documents worker started.");

            while (!_stopRequested)
            {
                try
                {
                    ProcessPendingJobs(logger);
                }
                catch (Exception ex)
                {
                    logger.Error("Unexpected error in Documents worker loop.", ex);
                }

                Thread.Sleep(2000);
            }

            logger.Info("Documents worker stopped.");
        }

        private static void ProcessPendingJobs(SimpleLogger logger)
        {
            foreach (var pendingFile in Directory.GetFiles(AppPaths.StatementsPendingRoot, "*.json"))
            {
                var fileName = Path.GetFileName(pendingFile);
                var processingFile = Path.Combine(AppPaths.StatementsProcessingRoot, fileName);

                try
                {
                    // Claim the job by moving it out of Pending; File.Move is atomic within a
                    // volume, so two worker instances racing on the same file never both succeed.
                    File.Move(pendingFile, processingFile);
                }
                catch (IOException)
                {
                    continue; // another worker instance (or a half-written temp file) won the race
                }

                ProcessJob(processingFile, logger);
            }
        }

        private static void ProcessJob(string processingFile, SimpleLogger logger)
        {
            var job = JsonFile.Read<StatementJob>(processingFile);
            try
            {
                var outputPath = Path.Combine(AppPaths.DocumentsOutputRoot, $"{job.StatementId}.pdf");
                StatementPdfRenderer.Render(job, outputPath);

                var completion = new StatementCompletion
                {
                    StatementId = job.StatementId,
                    Success = true,
                    FilePath = outputPath,
                    CompletedUtc = DateTime.UtcNow
                };
                JsonFile.WriteAtomic(Path.Combine(AppPaths.StatementsCompletedRoot, $"{job.StatementId}.json"), completion);
                logger.Info($"Statement {job.StatementId} rendered to {outputPath}");

                File.Delete(processingFile);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to render statement {job.StatementId}.", ex);

                var completion = new StatementCompletion
                {
                    StatementId = job.StatementId,
                    Success = false,
                    FailureReason = ex.Message,
                    CompletedUtc = DateTime.UtcNow
                };
                JsonFile.WriteAtomic(Path.Combine(AppPaths.StatementsFailedRoot, $"{job.StatementId}.json"), completion);
                File.Delete(processingFile);
            }
        }
    }
}
