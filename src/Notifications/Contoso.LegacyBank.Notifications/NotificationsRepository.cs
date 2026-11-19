using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Notifications
{
    public class ClaimedNotification
    {
        public long NotificationId { get; set; }
        public int CustomerId { get; set; }
        public string TemplateCode { get; set; }
        public string Payload { get; set; }
        public string Channel { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Subject { get; set; }
        public string BodyTemplate { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    /// <summary>
    /// ADO.NET access to the Notifications queue table via usp_Notification_*. Rows are
    /// enqueued both explicitly (by application code) and implicitly by
    /// trg_Transactions_NotifyLargeTransaction - this worker does not care which.
    /// </summary>
    public class NotificationsRepository
    {
        private readonly string _connectionString;

        public NotificationsRepository() : this(ResolveConnectionString()) { }

        public NotificationsRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private static string ResolveConnectionString()
        {
            var setting = ConfigurationManager.ConnectionStrings["ContosoLegacyBank"];
            return BankConfig.GetConnectionString(setting?.ConnectionString);
        }

        private SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public List<ClaimedNotification> ClaimPending(int batchSize)
        {
            var results = new List<ClaimedNotification>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Notification_ClaimPending", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@BatchSize", batchSize);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ClaimedNotification
                        {
                            NotificationId = (long)reader["NotificationId"],
                            CustomerId = (int)reader["CustomerId"],
                            TemplateCode = (string)reader["TemplateCode"],
                            Payload = (string)reader["Payload"],
                            Channel = (string)reader["Channel"],
                            CreatedUtc = (DateTime)reader["CreatedUtc"],
                            Subject = (string)reader["Subject"],
                            BodyTemplate = (string)reader["BodyTemplate"],
                            FirstName = (string)reader["FirstName"],
                            LastName = (string)reader["LastName"],
                            Email = (string)reader["Email"],
                            Phone = reader["Phone"] as string
                        });
                    }
                }
            }
            return results;
        }

        public void MarkSent(long notificationId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Notification_MarkSent", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@NotificationId", notificationId);
                command.ExecuteNonQuery();
            }
        }

        public void MarkFailed(long notificationId, string failureReason)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Notification_MarkFailed", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@NotificationId", notificationId);
                command.Parameters.AddWithValue("@FailureReason", failureReason);
                command.ExecuteNonQuery();
            }
        }
    }
}
