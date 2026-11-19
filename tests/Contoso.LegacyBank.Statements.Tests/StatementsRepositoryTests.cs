using System;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;
using Contoso.LegacyBank.Statements.Api.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Statements.Tests
{
    [TestClass]
    public class StatementsRepositoryTests
    {
        private static int CreateTestAccount(string suffix)
        {
            using (var connection = new SqlConnection(BankConfig.DefaultConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "INSERT INTO dbo.Customers (CustomerNumber, FirstName, LastName, Email, Phone, DateOfBirth) " +
                    "OUTPUT inserted.CustomerId VALUES (@Num, N'Stmt', N'Test', @Email, N'555-0000', '1990-01-01')", connection))
                {
                    command.Parameters.AddWithValue("@Num", "S" + suffix.Substring(0, Math.Min(9, suffix.Length)));
                    command.Parameters.AddWithValue("@Email", $"stmt.{suffix}@example.com");
                    var customerId = (int)command.ExecuteScalar();

                    using (var acctCommand = new SqlCommand(
                        "INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance) " +
                        "OUTPUT inserted.AccountId VALUES (@AccountNumber, @CustomerId, N'Checking', 500.00)", connection))
                    {
                        acctCommand.Parameters.AddWithValue("@AccountNumber", "7" + suffix.PadLeft(11, '0').Substring(0, 11));
                        acctCommand.Parameters.AddWithValue("@CustomerId", customerId);
                        return (int)acctCommand.ExecuteScalar();
                    }
                }
            }
        }

        [TestMethod]
        public void CreateAndGetById_RoundTripsRequestedStatus()
        {
            var accountId = CreateTestAccount(Guid.NewGuid().ToString("N").Substring(0, 8));
            var repository = new StatementsRepository(BankConfig.DefaultConnectionString);

            var statementId = repository.Create(accountId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
            var record = repository.GetById(statementId);

            Assert.IsNotNull(record);
            Assert.AreEqual("Requested", record.Status);
            Assert.AreEqual(accountId, record.AccountId);
        }

        [TestMethod]
        public void MarkProcessingThenCompleted_UpdatesStatusAndFilePath()
        {
            var accountId = CreateTestAccount(Guid.NewGuid().ToString("N").Substring(0, 8));
            var repository = new StatementsRepository(BankConfig.DefaultConnectionString);
            var statementId = repository.Create(accountId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

            repository.MarkProcessing(statementId);
            Assert.AreEqual("Processing", repository.GetById(statementId).Status);

            repository.MarkCompleted(statementId, @"C:\fake\path\statement.pdf");
            var completed = repository.GetById(statementId);

            Assert.AreEqual("Completed", completed.Status);
            Assert.AreEqual(@"C:\fake\path\statement.pdf", completed.FilePath);
            Assert.IsNotNull(completed.CompletedUtc);
        }

        [TestMethod]
        public void MarkFailed_RecordsFailureReason()
        {
            var accountId = CreateTestAccount(Guid.NewGuid().ToString("N").Substring(0, 8));
            var repository = new StatementsRepository(BankConfig.DefaultConnectionString);
            var statementId = repository.Create(accountId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

            repository.MarkFailed(statementId, "PDF rendering failed.");
            var record = repository.GetById(statementId);

            Assert.AreEqual("Failed", record.Status);
            Assert.AreEqual("PDF rendering failed.", record.FailureReason);
        }
    }
}
