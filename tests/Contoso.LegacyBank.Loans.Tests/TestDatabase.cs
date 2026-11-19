using System;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Loans.Tests
{
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
                    command.Parameters.AddWithValue("@Num", "L" + suffix.Substring(0, Math.Min(9, suffix.Length)));
                    command.Parameters.AddWithValue("@First", "Test");
                    command.Parameters.AddWithValue("@Last", "LoanCustomer" + suffix);
                    command.Parameters.AddWithValue("@Email", $"loantest.{suffix}@example.com");
                    command.Parameters.AddWithValue("@Phone", "555-0000");
                    command.Parameters.AddWithValue("@Dob", new DateTime(1990, 1, 1));
                    return (int)command.ExecuteScalar();
                }
            }
        }

        public static int GetLoanProductId(string productCode)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT LoanProductId FROM dbo.LoanProducts WHERE ProductCode = @Code", connection))
                {
                    command.Parameters.AddWithValue("@Code", productCode);
                    return (int)command.ExecuteScalar();
                }
            }
        }

        public static void MakeInstallmentOverdue(int loanId, int installmentNumber)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "UPDATE dbo.LoanSchedules SET DueDate = DATEADD(DAY, -5, CAST(SYSUTCDATETIME() AS DATE)) " +
                    "WHERE LoanId = @LoanId AND InstallmentNumber = @InstallmentNumber", connection))
                {
                    command.Parameters.AddWithValue("@LoanId", loanId);
                    command.Parameters.AddWithValue("@InstallmentNumber", installmentNumber);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
