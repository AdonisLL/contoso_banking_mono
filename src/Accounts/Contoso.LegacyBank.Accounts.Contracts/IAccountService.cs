using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Contoso.LegacyBank.Accounts.Contracts
{
    /// <summary>
    /// The Accounts service contract: customer/account lookup, balance, transfers, deposits,
    /// withdrawals, interest accrual, and statement transaction data. Every operation that
    /// moves money ultimately executes a T-SQL stored procedure - this service is a thin SOAP
    /// façade over the real business rules, not a reimplementation of them.
    /// </summary>
    [ServiceContract(Namespace = "http://contoso.com/legacybank/accounts/2024")]
    public interface IAccountService
    {
        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        AccountDto GetAccountByNumber(string accountNumber);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        AccountDto GetAccountById(int accountId);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        AccountDto[] SearchAccounts(string searchTerm);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        decimal GetBalance(int accountId);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        TransactionDto[] GetTransactionHistory(int accountId, DateTime? fromUtc, DateTime? toUtc);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        TransactionDto PostDeposit(int accountId, decimal amount, string description, string externalId);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        TransactionDto PostWithdrawal(int accountId, decimal amount, string description, string externalId);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        TransferResultDto TransferFunds(int fromAccountId, int toAccountId, decimal amount, string description);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        void ApplyMonthlyInterest(int? accountId);

        [OperationContract]
        [FaultContract(typeof(AccountFault))]
        StatementDataDto GetStatementData(int accountId, DateTime periodStart, DateTime periodEnd);
    }
}
