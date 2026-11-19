using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Statements.Api.Data
{
    public class StatementRecord
    {
        public int StatementId { get; set; }
        public int AccountId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Status { get; set; }
        public DateTime RequestedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string FilePath { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>ADO.NET access to the Statements status table via the usp_Statement_* procedures.</summary>
    public class StatementsRepository
    {
        private readonly string _connectionString;

        public StatementsRepository() : this(ResolveConnectionString()) { }

        public StatementsRepository(string connectionString)
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

        public int Create(int accountId, DateTime periodStart, DateTime periodEnd)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_Create", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@AccountId", accountId);
                command.Parameters.AddWithValue("@PeriodStart", periodStart.Date);
                command.Parameters.AddWithValue("@PeriodEnd", periodEnd.Date);
                var idParam = command.Parameters.Add("@StatementId", SqlDbType.Int);
                idParam.Direction = ParameterDirection.Output;
                command.ExecuteNonQuery();
                return (int)idParam.Value;
            }
        }

        public StatementRecord GetById(int statementId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_GetById", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@StatementId", statementId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new StatementRecord
                    {
                        StatementId = (int)reader["StatementId"],
                        AccountId = (int)reader["AccountId"],
                        PeriodStart = (DateTime)reader["PeriodStart"],
                        PeriodEnd = (DateTime)reader["PeriodEnd"],
                        Status = (string)reader["Status"],
                        RequestedUtc = (DateTime)reader["RequestedUtc"],
                        CompletedUtc = reader["CompletedUtc"] as DateTime?,
                        FilePath = reader["FilePath"] as string,
                        FailureReason = reader["FailureReason"] as string
                    };
                }
            }
        }

        public void MarkProcessing(int statementId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_MarkProcessing", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@StatementId", statementId);
                command.ExecuteNonQuery();
            }
        }

        public void MarkCompleted(int statementId, string filePath)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_MarkCompleted", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@StatementId", statementId);
                command.Parameters.AddWithValue("@FilePath", filePath);
                command.ExecuteNonQuery();
            }
        }

        public void MarkFailed(int statementId, string failureReason)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Statement_MarkFailed", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@StatementId", statementId);
                command.Parameters.AddWithValue("@FailureReason", failureReason);
                command.ExecuteNonQuery();
            }
        }
    }
}
