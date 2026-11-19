using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Accounts.Data
{
    /// <summary>
    /// Thin ADO.NET data-access layer. Every method maps 1:1 onto a stored procedure in
    /// database\Scripts - this class does not contain business rules, only parameter
    /// marshaling and result-set mapping. The rules (overdraft checks, fees, interest,
    /// transfer atomicity) live entirely in the database.
    /// </summary>
    public class AccountsRepository
    {
        private readonly string _connectionString;

        public AccountsRepository() : this(ResolveConnectionString()) { }

        public AccountsRepository(string connectionString)
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

        public AccountDto GetByNumber(string accountNumber)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_GetByNumber", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountNumber", accountNumber);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapAccount(reader) : null;
                }
            }
        }

        public AccountDto GetById(int accountId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_GetById", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapAccount(reader) : null;
                }
            }
        }

        public List<AccountDto> Search(string searchTerm)
        {
            var results = new List<AccountDto>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_Search", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@SearchTerm", searchTerm);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapAccount(reader));
                }
            }
            return results;
        }

        public decimal GetBalance(int accountId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_GetBalance", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                var balanceParam = command.Parameters.Add("@Balance", SqlDbType.Decimal, 18);
                balanceParam.Precision = 18;
                balanceParam.Scale = 2;
                balanceParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();
                return balanceParam.Value == DBNull.Value ? 0m : (decimal)balanceParam.Value;
            }
        }

        public List<TransactionDto> GetTransactionHistory(int accountId, DateTime? fromUtc, DateTime? toUtc)
        {
            var results = new List<TransactionDto>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_GetTransactionHistory", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                command.Parameters.AddWithValue("@FromUtc", (object)fromUtc ?? DBNull.Value);
                command.Parameters.AddWithValue("@ToUtc", (object)toUtc ?? DBNull.Value);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapTransaction(reader));
                }
            }
            return results;
        }

        public TransactionDto PostDeposit(int accountId, decimal amount, string description, string externalId, string transactionType = "Deposit")
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Transaction_PostDeposit", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                command.Parameters.AddWithValue("@Amount", amount);
                command.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                command.Parameters.AddWithValue("@ExternalId", (object)externalId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TransactionType", transactionType);
                var txIdParam = command.Parameters.Add("@TransactionId", SqlDbType.BigInt);
                txIdParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();
                var transactionId = (long)txIdParam.Value;
                return GetTransactionById(connection, transactionId);
            }
        }

        public TransactionDto PostWithdrawal(int accountId, decimal amount, string description, string externalId, string transactionType = "Withdrawal")
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Transaction_PostWithdrawal", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                command.Parameters.AddWithValue("@Amount", amount);
                command.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                command.Parameters.AddWithValue("@ExternalId", (object)externalId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TransactionType", transactionType);
                var txIdParam = command.Parameters.Add("@TransactionId", SqlDbType.BigInt);
                txIdParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();
                var transactionId = (long)txIdParam.Value;
                return GetTransactionById(connection, transactionId);
            }
        }

        public TransferResultDto TransferFunds(int fromAccountId, int toAccountId, decimal amount, string description)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_TransferFunds", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@FromAccountId", fromAccountId);
                command.Parameters.AddWithValue("@ToAccountId", toAccountId);
                command.Parameters.AddWithValue("@Amount", amount);
                command.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                var fromTxParam = command.Parameters.Add("@FromTransactionId", SqlDbType.BigInt);
                fromTxParam.Direction = ParameterDirection.Output;
                var toTxParam = command.Parameters.Add("@ToTransactionId", SqlDbType.BigInt);
                toTxParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();

                var fromTransactionId = (long)fromTxParam.Value;
                var toTransactionId = (long)toTxParam.Value;
                var fromTransaction = GetTransactionById(connection, fromTransactionId);
                var toTransaction = GetTransactionById(connection, toTransactionId);

                return new TransferResultDto
                {
                    FromTransactionId = fromTransactionId,
                    ToTransactionId = toTransactionId,
                    FromAccountBalance = fromTransaction.BalanceAfter,
                    ToAccountBalance = toTransaction.BalanceAfter
                };
            }
        }

        public void ApplyMonthlyInterest(int? accountId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Account_ApplyMonthlyInterest", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", (object)accountId ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public StatementDataDto GetStatementData(int accountId, DateTime periodStart, DateTime periodEnd)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_GetTransactionData", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                command.Parameters.AddWithValue("@PeriodStart", periodStart.Date);
                command.Parameters.AddWithValue("@PeriodEnd", periodEnd.Date);

                using (var reader = command.ExecuteReader())
                {
                    AccountDto account = null;
                    if (reader.Read())
                    {
                        account = new AccountDto
                        {
                            AccountNumber = (string)reader["AccountNumber"],
                            AccountType = (string)reader["AccountType"],
                            Balance = (decimal)reader["CurrentBalance"],
                            Customer = new CustomerDto
                            {
                                CustomerNumber = (string)reader["CustomerNumber"],
                                FirstName = (string)reader["FirstName"],
                                LastName = (string)reader["LastName"]
                            }
                        };
                    }

                    var transactions = new List<TransactionDto>();
                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            transactions.Add(new TransactionDto
                            {
                                TransactionId = (long)reader["TransactionId"],
                                TransactionType = (string)reader["TransactionType"],
                                Amount = (decimal)reader["Amount"],
                                BalanceAfter = (decimal)reader["BalanceAfter"],
                                Description = reader["Description"] as string,
                                CreatedUtc = (DateTime)reader["CreatedUtc"]
                            });
                        }
                    }

                    return new StatementDataDto
                    {
                        Account = account,
                        Transactions = transactions.ToArray(),
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd
                    };
                }
            }
        }

        private static TransactionDto GetTransactionById(SqlConnection connection, long transactionId)
        {
            using (var command = new SqlCommand(
                "SELECT TransactionId, AccountId, TransactionType, Amount, BalanceAfter, Description, ExternalId, RelatedTransactionId, CreatedUtc " +
                "FROM dbo.Transactions WHERE TransactionId = @TransactionId", connection))
            {
                command.Parameters.AddWithValue("@TransactionId", transactionId);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapTransaction(reader) : null;
                }
            }
        }

        private static AccountDto MapAccount(SqlDataReader reader)
        {
            return new AccountDto
            {
                AccountId = (int)reader["AccountId"],
                AccountNumber = (string)reader["AccountNumber"],
                AccountType = (string)reader["AccountType"],
                Status = (string)reader["Status"],
                Balance = (decimal)reader["Balance"],
                OverdraftLimit = (decimal)reader["OverdraftLimit"],
                InterestRate = (decimal)reader["InterestRate"],
                OpenedUtc = (DateTime)reader["OpenedUtc"],
                ClosedUtc = reader["ClosedUtc"] as DateTime?,
                Customer = new CustomerDto
                {
                    CustomerId = (int)reader["CustomerId"],
                    CustomerNumber = (string)reader["CustomerNumber"],
                    FirstName = (string)reader["FirstName"],
                    LastName = (string)reader["LastName"],
                    Email = (string)reader["Email"],
                    Phone = reader["Phone"] as string
                }
            };
        }

        private static TransactionDto MapTransaction(SqlDataReader reader)
        {
            return new TransactionDto
            {
                TransactionId = (long)reader["TransactionId"],
                AccountId = (int)reader["AccountId"],
                TransactionType = (string)reader["TransactionType"],
                Amount = (decimal)reader["Amount"],
                BalanceAfter = (decimal)reader["BalanceAfter"],
                Description = reader["Description"] as string,
                ExternalId = reader["ExternalId"] as string,
                RelatedTransactionId = reader["RelatedTransactionId"] as long?,
                CreatedUtc = (DateTime)reader["CreatedUtc"]
            };
        }
    }
}
