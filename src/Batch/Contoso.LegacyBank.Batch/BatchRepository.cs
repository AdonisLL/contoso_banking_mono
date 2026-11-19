using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Batch
{
    /// <summary>
    /// Direct ADO.NET access to the batch stored procedures. This worker deliberately
    /// bypasses the Accounts WCF service and writes straight to the shared database - a
    /// classic "integration database" coupling that a real decomposition effort must resolve
    /// (see docs/architecture.md "intentional modernization seams").
    /// </summary>
    public class BatchRepository
    {
        private readonly string _connectionString;

        public BatchRepository() : this(ResolveConnectionString()) { }

        public BatchRepository(string connectionString)
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

        public int CreateBatch(string fileName)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Batch_CreateBatch", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@FileName", fileName);
                var idParam = command.Parameters.Add("@BatchId", SqlDbType.Int);
                idParam.Direction = ParameterDirection.Output;
                command.ExecuteNonQuery();
                return (int)idParam.Value;
            }
        }

        public (string Status, string ErrorMessage) ReconcileRow(int batchId, int rowNumber, string externalId, string accountNumber, decimal amount, string transactionType)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Batch_ReconcileRow", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@BatchId", batchId);
                command.Parameters.AddWithValue("@RowNumber", rowNumber);
                command.Parameters.AddWithValue("@ExternalId", externalId);
                command.Parameters.AddWithValue("@AccountNumber", accountNumber);
                command.Parameters.AddWithValue("@Amount", amount);
                command.Parameters.AddWithValue("@TransactionType", transactionType);
                var statusParam = command.Parameters.Add("@Status", SqlDbType.VarChar, 20);
                statusParam.Direction = ParameterDirection.Output;
                var errorParam = command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 400);
                errorParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();
                return ((string)statusParam.Value, errorParam.Value as string);
            }
        }

        /// <summary>Records a row that could not even be parsed (wrong column count, bad number, etc).</summary>
        public void RecordUnparsableRow(int batchId, int rowNumber, string rawLine, string errorMessage)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "INSERT INTO dbo.BatchImportRows (BatchId, RowNumber, ExternalId, AccountNumber, Amount, TransactionType, Status, ErrorMessage) " +
                "VALUES (@BatchId, @RowNumber, @ExternalId, N'', 0, N'Unknown', N'Error', @ErrorMessage)", connection))
            {
                command.Parameters.AddWithValue("@BatchId", batchId);
                command.Parameters.AddWithValue("@RowNumber", rowNumber);
                command.Parameters.AddWithValue("@ExternalId", $"UNPARSABLE-ROW-{rowNumber}");
                command.Parameters.AddWithValue("@ErrorMessage", errorMessage + " | raw: " + rawLine);
                command.ExecuteNonQuery();
            }
        }

        public void CompleteBatch(int batchId, string reportPath)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Batch_CompleteBatch", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@BatchId", batchId);
                command.Parameters.AddWithValue("@ReportPath", (object)reportPath ?? System.DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public (int Total, int Success, int Duplicate, int Error) GetBatchSummary(int batchId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT TotalRows, SuccessCount, DuplicateCount, ErrorCount FROM dbo.BatchImportBatches WHERE BatchId = @BatchId", connection))
            {
                command.Parameters.AddWithValue("@BatchId", batchId);
                using (var reader = command.ExecuteReader())
                {
                    reader.Read();
                    return ((int)reader["TotalRows"], (int)reader["SuccessCount"], (int)reader["DuplicateCount"], (int)reader["ErrorCount"]);
                }
            }
        }
    }
}
