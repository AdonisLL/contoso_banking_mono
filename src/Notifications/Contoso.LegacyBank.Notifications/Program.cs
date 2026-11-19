using System;
using System.IO;
using System.Threading;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Notifications
{
    /// <summary>
    /// Polls the Notifications queue (populated both by explicit enqueue calls and by
    /// trg_Transactions_NotifyLargeTransaction) and simulates delivery by rendering the
    /// template and writing it to an outbox file + the log, instead of a real email/SMS
    /// gateway. The modernization target replaces this polling loop with an event-driven
    /// subscriber (Service Bus / Logic Apps).
    /// </summary>
    public static class Program
    {
        private static volatile bool _stopRequested;

        public static void Main()
        {
            var logger = new SimpleLogger("Notifications");
            AppPaths.EnsureAll();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _stopRequested = true;
            };

            Console.WriteLine("Contoso Legacy Bank - Notifications worker running. Press Ctrl+C to stop.");
            logger.Info("Notifications worker started.");

            var repository = new NotificationsRepository();

            while (!_stopRequested)
            {
                try
                {
                    ProcessBatch(repository, logger);
                }
                catch (Exception ex)
                {
                    logger.Error("Unexpected error in Notifications worker loop.", ex);
                }

                Thread.Sleep(2000);
            }

            logger.Info("Notifications worker stopped.");
        }

        private static void ProcessBatch(NotificationsRepository repository, SimpleLogger logger)
        {
            var claimed = repository.ClaimPending(batchSize: 20);
            foreach (var notification in claimed)
            {
                try
                {
                    var body = TemplateRenderer.Render(notification.BodyTemplate, notification.Payload);
                    var outboxPath = Path.Combine(AppPaths.NotificationsOutboxRoot, $"{notification.NotificationId}.txt");
                    File.WriteAllText(outboxPath,
                        $"To: {notification.FirstName} {notification.LastName} <{notification.Email}>\r\n" +
                        $"Channel: {notification.Channel}\r\n" +
                        $"Subject: {notification.Subject}\r\n\r\n{body}\r\n");

                    repository.MarkSent(notification.NotificationId);
                    logger.Info($"Sent notification {notification.NotificationId} ({notification.TemplateCode}) to {notification.Email}");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to send notification {notification.NotificationId}.", ex);
                    repository.MarkFailed(notification.NotificationId, ex.Message);
                }
            }
        }
    }
}
