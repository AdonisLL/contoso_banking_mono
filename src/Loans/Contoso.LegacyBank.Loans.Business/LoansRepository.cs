using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Contoso.LegacyBank.Loans.Contracts;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Loans.Business
{
    /// <summary>
    /// ADO.NET data access for Loans, calling the stored procedures in
    /// database\Scripts\03_StoredProcedures_Loans.sql. Amortization math and payment
    /// allocation rules live entirely in those procedures.
    /// </summary>
    public class LoansRepository
    {
        private readonly string _connectionString;

        public LoansRepository() : this(ResolveConnectionString()) { }

        public LoansRepository(string connectionString)
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

        public List<LoanProductDto> ListLoanProducts()
        {
            var results = new List<LoanProductDto>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_LoanProduct_List", connection) { CommandType = CommandType.StoredProcedure })
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    results.Add(new LoanProductDto
                    {
                        LoanProductId = (int)reader["LoanProductId"],
                        ProductCode = (string)reader["ProductCode"],
                        Name = (string)reader["Name"],
                        AnnualInterestRate = (decimal)reader["AnnualInterestRate"],
                        MinTermMonths = (int)reader["MinTermMonths"],
                        MaxTermMonths = (int)reader["MaxTermMonths"],
                        MinAmount = (decimal)reader["MinAmount"],
                        MaxAmount = (decimal)reader["MaxAmount"]
                    });
                }
            }
            return results;
        }

        public LoanDto GetById(int loanId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Loan_GetById", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@LoanId", loanId);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? MapLoan(reader) : null;
            }
        }

        public LoanDto GetByNumber(string loanNumber)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Loan_GetByNumber", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@LoanNumber", loanNumber);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? MapLoan(reader) : null;
            }
        }

        public List<LoanDto> Search(string searchTerm)
        {
            var results = new List<LoanDto>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Loan_Search", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@SearchTerm", searchTerm);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapLoan(reader));
                }
            }
            return results;
        }

        public List<LoanScheduleEntryDto> GetSchedule(int loanId)
        {
            var results = new List<LoanScheduleEntryDto>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Loan_GetSchedule", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@LoanId", loanId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(MapScheduleEntry(reader));
                }
            }
            return results;
        }

        public (int LoanId, string LoanNumber) Originate(int customerId, int loanProductId, decimal principalAmount, int termMonths)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("dbo.usp_Loan_Originate", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@CustomerId", customerId);
                command.Parameters.AddWithValue("@LoanProductId", loanProductId);
                command.Parameters.AddWithValue("@PrincipalAmount", principalAmount);
                command.Parameters.AddWithValue("@TermMonths", termMonths);
                var loanIdParam = command.Parameters.Add("@LoanId", SqlDbType.Int);
                loanIdParam.Direction = ParameterDirection.Output;
                var loanNumberParam = command.Parameters.Add("@LoanNumber", SqlDbType.VarChar, 12);
                loanNumberParam.Direction = ParameterDirection.Output;

                command.ExecuteNonQuery();
                return ((int)loanIdParam.Value, (string)loanNumberParam.Value);
            }
        }

        public LoanPaymentResultDto ApplyPayment(int loanId, decimal amountPaid)
        {
            using (var connection = OpenConnection())
            {
                using (var command = new SqlCommand("dbo.usp_Loan_ApplyPayment", connection) { CommandType = CommandType.StoredProcedure })
                {
                    command.Parameters.AddWithValue("@LoanId", loanId);
                    command.Parameters.AddWithValue("@AmountPaid", amountPaid);
                    var paymentIdParam = command.Parameters.Add("@LoanPaymentId", SqlDbType.BigInt);
                    paymentIdParam.Direction = ParameterDirection.Output;

                    command.ExecuteNonQuery();
                    var paymentId = (long)paymentIdParam.Value;

                    decimal amountPaidResult, principalApplied, interestApplied, lateFeeApplied;
                    using (var paymentCommand = new SqlCommand(
                        "SELECT AmountPaid, PrincipalApplied, InterestApplied, LateFeeApplied FROM dbo.LoanPayments WHERE LoanPaymentId = @Id", connection))
                    {
                        paymentCommand.Parameters.AddWithValue("@Id", paymentId);
                        using (var reader = paymentCommand.ExecuteReader())
                        {
                            reader.Read();
                            amountPaidResult = (decimal)reader["AmountPaid"];
                            principalApplied = (decimal)reader["PrincipalApplied"];
                            interestApplied = (decimal)reader["InterestApplied"];
                            lateFeeApplied = (decimal)reader["LateFeeApplied"];
                        }
                    }

                    var loan = GetByIdOnConnection(connection, loanId);
                    return new LoanPaymentResultDto
                    {
                        LoanPaymentId = paymentId,
                        AmountPaid = amountPaidResult,
                        PrincipalApplied = principalApplied,
                        InterestApplied = interestApplied,
                        LateFeeApplied = lateFeeApplied,
                        RemainingOutstandingPrincipal = loan.OutstandingPrincipal,
                        LoanStatus = loan.Status
                    };
                }
            }
        }

        private static LoanDto GetByIdOnConnection(SqlConnection connection, int loanId)
        {
            using (var command = new SqlCommand("dbo.usp_Loan_GetById", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@LoanId", loanId);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? MapLoan(reader) : null;
            }
        }

        private static LoanDto MapLoan(SqlDataReader reader)
        {
            return new LoanDto
            {
                LoanId = (int)reader["LoanId"],
                LoanNumber = (string)reader["LoanNumber"],
                CustomerId = (int)reader["CustomerId"],
                CustomerNumber = (string)reader["CustomerNumber"],
                FirstName = (string)reader["FirstName"],
                LastName = (string)reader["LastName"],
                LoanProductId = (int)reader["LoanProductId"],
                ProductCode = (string)reader["ProductCode"],
                ProductName = (string)reader["ProductName"],
                PrincipalAmount = (decimal)reader["PrincipalAmount"],
                AnnualInterestRate = (decimal)reader["AnnualInterestRate"],
                TermMonths = (int)reader["TermMonths"],
                Status = (string)reader["Status"],
                OriginatedUtc = (DateTime)reader["OriginatedUtc"],
                MaturityDate = (DateTime)reader["MaturityDate"],
                OutstandingPrincipal = (decimal)reader["OutstandingPrincipal"]
            };
        }

        private static LoanScheduleEntryDto MapScheduleEntry(SqlDataReader reader)
        {
            return new LoanScheduleEntryDto
            {
                LoanScheduleId = (long)reader["LoanScheduleId"],
                LoanId = (int)reader["LoanId"],
                InstallmentNumber = (int)reader["InstallmentNumber"],
                DueDate = (DateTime)reader["DueDate"],
                PrincipalDue = (decimal)reader["PrincipalDue"],
                InterestDue = (decimal)reader["InterestDue"],
                TotalDue = (decimal)reader["TotalDue"],
                Status = (string)reader["Status"]
            };
        }
    }
}
