using System;
using System.Data.SqlClient;
using Contoso.LegacyBank.Batch;
using Contoso.LegacyBank.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Batch.Tests
{
    [TestClass]
    public class BatchRepositoryTests
    {
        private static int CreateTestAccount(string suffix)
        {
            using (var connection = new SqlConnection(BankConfig.DefaultConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "INSERT INTO dbo.Customers (CustomerNumber, FirstName, LastName, Email, Phone, DateOfBirth) " +
                    "OUTPUT inserted.CustomerId VALUES (@Num, N'Batch', N'Test', @Email, N'555-0000', '1990-01-01')", connection))
                {
                    command.Parameters.AddWithValue("@Num", "B" + suffix.Substring(0, Math.Min(9, suffix.Length)));
                    command.Parameters.AddWithValue("@Email", $"batch.{suffix}@example.com");
                    var customerId = (int)command.ExecuteScalar();

                    using (var acctCommand = new SqlCommand(
                        "INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance) " +
                        "OUTPUT inserted.AccountId VALUES (@AccountNumber, @CustomerId, N'Checking', 500.00)", connection))
                    {
                        acctCommand.Parameters.AddWithValue("@AccountNumber", "8" + suffix.PadLeft(11, '0').Substring(0, 11));
                        acctCommand.Parameters.AddWithValue("@CustomerId", customerId);
                        return (int)acctCommand.ExecuteScalar();
                    }
                }
            }
        }

        private static string GetAccountNumber(int accountId)
        {
            using (var connection = new SqlConnection(BankConfig.DefaultConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT AccountNumber FROM dbo.Accounts WHERE AccountId = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", accountId);
                    return (string)command.ExecuteScalar();
                }
            }
        }

        [TestMethod]
        public void ReconcileRow_ValidDeposit_PostsSuccessfully()
        {
            var accountId = CreateTestAccount(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountNumber = GetAccountNumber(accountId);
            var repository = new BatchRepository(BankConfig.DefaultConnectionString);
            var batchId = repository.CreateBatch("test.csv");

            var (status, errorMessage) = repository.ReconcileRow(batchId, 1, Guid.NewGuid().ToString("N"), accountNumber, 100.00m, "Deposit");

            Assert.AreEqual("Posted", status);
            Assert.IsNull(errorMessage);
        }

        [TestMethod]
        public void ReconcileRow_DuplicateExternalId_ReturnsDuplicateStatus()
        {
            var accountId = CreateTestAccount(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountNumber = GetAccountNumber(accountId);
            var repository = new BatchRepository(BankConfig.DefaultConnectionString);
            var batchId = repository.CreateBatch("test.csv");
            var externalId = Guid.NewGuid().ToString("N");

            repository.ReconcileRow(batchId, 1, externalId, accountNumber, 100.00m, "Deposit");
            var (status, errorMessage) = repository.ReconcileRow(batchId, 2, externalId, accountNumber, 100.00m, "Deposit");

            Assert.AreEqual("Duplicate", status);
            StringAssert.Contains(errorMessage, "already imported");
        }

        [TestMethod]
        public void ReconcileRow_UnknownAccount_ReturnsErrorStatus()
        {
            var repository = new BatchRepository(BankConfig.DefaultConnectionString);
            var batchId = repository.CreateBatch("test.csv");

            var (status, errorMessage) = repository.ReconcileRow(batchId, 1, Guid.NewGuid().ToString("N"), "000000000000", 100.00m, "Deposit");

            Assert.AreEqual("Error", status);
            StringAssert.Contains(errorMessage, "Unknown account");
        }
    }
}
