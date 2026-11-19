using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Portal.Services
{
    public class NotificationRow
    {
        public long NotificationId { get; set; }
        public string CustomerName { get; set; }
        public string TemplateCode { get; set; }
        public string Status { get; set; }
        public string Channel { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SentUtc { get; set; }
    }

    /// <summary>
    /// Reads the Notifications table directly - there is no Notifications WCF/REST service,
    /// so the Portal's "recent notifications" view bypasses the service layer entirely. This
    /// direct-to-database reporting shortcut is itself a documented modernization seam.
    /// </summary>
    public class NotificationsReadRepository
    {
        private readonly string _connectionString;

        public NotificationsReadRepository()
        {
            var setting = ConfigurationManager.ConnectionStrings["ContosoLegacyBank"];
            _connectionString = BankConfig.GetConnectionString(setting?.ConnectionString);
        }

        public List<NotificationRow> GetRecent(int take = 50)
        {
            var results = new List<NotificationRow>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    $"SELECT TOP (@Take) n.NotificationId, c.FirstName, c.LastName, n.TemplateCode, n.Status, n.Channel, n.CreatedUtc, n.SentUtc " +
                    "FROM dbo.Notifications n JOIN dbo.Customers c ON c.CustomerId = n.CustomerId " +
                    "ORDER BY n.CreatedUtc DESC", connection))
                {
                    command.Parameters.AddWithValue("@Take", take);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new NotificationRow
                            {
                                NotificationId = (long)reader["NotificationId"],
                                CustomerName = $"{reader["FirstName"]} {reader["LastName"]}",
                                TemplateCode = (string)reader["TemplateCode"],
                                Status = (string)reader["Status"],
                                Channel = (string)reader["Channel"],
                                CreatedUtc = (DateTime)reader["CreatedUtc"],
                                SentUtc = reader["SentUtc"] as DateTime?
                            });
                        }
                    }
                }
            }
            return results;
        }
    }
}
