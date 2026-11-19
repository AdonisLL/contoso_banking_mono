using System;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Accounts.Tests
{
    /// <summary>
    /// Creates isolated customer/account fixtures directly against LocalDB so tests never
    /// depend on (or corrupt) the shared seed data, and can run in any order.
    /// </summary>
    internal static class TestDatabase
    {
        public static string ConnectionString => BankConfig.DefaultConnectionString;

        public static int CreateTestCustomer(string suffix)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "INSERT INTO dbo.Customers (CustomerNumber, FirstName, LastName, Email, Phone, DateOfBirth) " +
                    "OUTPUT inserted.CustomerId " +
                    "VALUES (@Num, @First, @Last, @Email, @Phone, @Dob)", connection))
                {
                    command.Parameters.AddWithValue("@Num", "T" + suffix.Substring(0, Math.Min(9, suffix.Length)));
                    command.Parameters.AddWithValue("@First", "Test");
                    command.Parameters.AddWithValue("@Last", "Customer" + suffix);
                    command.Parameters.AddWithValue("@Email", $"test.{suffix}@example.com");
                    command.Parameters.AddWithValue("@Phone", "555-0000");
                    command.Parameters.AddWithValue("@Dob", new DateTime(1990, 1, 1));
                    return (int)command.ExecuteScalar();
                }
            }
        }

        public static int CreateTestAccount(int customerId, string accountNumberSuffix, decimal balance, decimal overdraftLimit = 0m, decimal interestRate = 0m)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "INSERT INTO dbo.Accounts (AccountNumber, CustomerId, AccountType, Balance, OverdraftLimit, InterestRate) " +
                    "OUTPUT inserted.AccountId " +
                    "VALUES (@AccountNumber, @CustomerId, N'Checking', @Balance, @OverdraftLimit, @InterestRate)", connection))
                {
                    command.Parameters.AddWithValue("@AccountNumber", "9" + accountNumberSuffix.PadLeft(11, '0').Substring(0, 11));
                    command.Parameters.AddWithValue("@CustomerId", customerId);
                    command.Parameters.AddWithValue("@Balance", balance);
                    command.Parameters.AddWithValue("@OverdraftLimit", overdraftLimit);
                    command.Parameters.AddWithValue("@InterestRate", interestRate);
                    return (int)command.ExecuteScalar();
                }
            }
        }

        public static int CountNotificationsForCustomer(int customerId, string templateCode)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.Notifications WHERE CustomerId = @CustomerId AND TemplateCode = @TemplateCode", connection))
                {
                    command.Parameters.AddWithValue("@CustomerId", customerId);
                    command.Parameters.AddWithValue("@TemplateCode", templateCode);
                    return (int)command.ExecuteScalar();
                }
            }
        }
    }
}
