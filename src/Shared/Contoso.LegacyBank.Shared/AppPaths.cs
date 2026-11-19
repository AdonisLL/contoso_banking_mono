using System;
using System.IO;

namespace Contoso.LegacyBank.Shared
{
    /// <summary>
    /// Resolves the local, machine-bound folders the legacy file-queue integrations use
    /// (statement job hand-off, generated PDFs, batch CSV input/archive). This machine-bound
    /// storage is itself one of the documented modernization seams - the target architecture
    /// replaces it with Blob Storage and a managed queue.
    /// </summary>
    public static class AppPaths
    {
        public static string Root =>
            Environment.ExpandEnvironmentVariables(Path.Combine("%LOCALAPPDATA%", "ContosoLegacyBank"));

        public static string DocumentsRoot => Path.Combine(Root, "Documents");
        public static string DocumentsOutputRoot => Path.Combine(DocumentsRoot, "Output");
        public static string StatementsPendingRoot => Path.Combine(Root, "Statements", "Pending");
        public static string StatementsProcessingRoot => Path.Combine(Root, "Statements", "Processing");
        public static string StatementsCompletedRoot => Path.Combine(Root, "Statements", "Completed");
        public static string StatementsFailedRoot => Path.Combine(Root, "Statements", "Failed");

        public static string BatchRoot => Path.Combine(Root, "Batch");
        public static string BatchInputRoot => Path.Combine(BatchRoot, "Input");
        public static string BatchArchiveRoot => Path.Combine(BatchRoot, "Archive");
        public static string BatchErrorRoot => Path.Combine(BatchRoot, "Error");
        public static string BatchReportsRoot => Path.Combine(BatchRoot, "Reports");

        public static string LogsRoot => Path.Combine(Root, "Logs");
        public static string NotificationsOutboxRoot => Path.Combine(Root, "Notifications", "Outbox");

        public static void EnsureAll()
        {
            foreach (var dir in new[]
            {
                DocumentsRoot, DocumentsOutputRoot, StatementsPendingRoot, StatementsProcessingRoot, StatementsCompletedRoot,
                StatementsFailedRoot, BatchInputRoot, BatchArchiveRoot, BatchErrorRoot, BatchReportsRoot, LogsRoot,
                NotificationsOutboxRoot
            })
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
