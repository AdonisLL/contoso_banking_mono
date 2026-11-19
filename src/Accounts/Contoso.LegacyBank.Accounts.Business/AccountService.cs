using System;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Accounts.Data;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Accounts.Business
{
    /// <summary>
    /// WCF service implementation. Translates stored-procedure RAISERROR business errors into
    /// typed SOAP faults and otherwise just forwards calls to <see cref="AccountsRepository"/>.
    /// No account/transaction rule is duplicated here - see database\Scripts for the rules.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class AccountService : IAccountService
    {
        private readonly AccountsRepository _repository;
        private readonly SimpleLogger _logger;

        public AccountService() : this(new AccountsRepository(), new SimpleLogger("Accounts")) { }

        public AccountService(AccountsRepository repository, SimpleLogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public AccountDto GetAccountByNumber(string accountNumber)
        {
            return Execute(() =>
            {
                var account = _repository.GetByNumber(accountNumber);
                if (account == null)
                    throw Fault(AccountFaultCodes.NotFound, $"Account '{accountNumber}' was not found.");
                return account;
            });
        }

        public AccountDto GetAccountById(int accountId)
        {
            return Execute(() =>
            {
                var account = _repository.GetById(accountId);
                if (account == null)
                    throw Fault(AccountFaultCodes.NotFound, $"Account {accountId} was not found.");
                return account;
            });
        }

        public AccountDto[] SearchAccounts(string searchTerm)
        {
            return Execute(() => _repository.Search(searchTerm).ToArray());
        }

        public decimal GetBalance(int accountId)
        {
            return Execute(() => _repository.GetBalance(accountId));
        }

        public TransactionDto[] GetTransactionHistory(int accountId, DateTime? fromUtc, DateTime? toUtc)
        {
            return Execute(() => _repository.GetTransactionHistory(accountId, fromUtc, toUtc).ToArray());
        }

        public TransactionDto PostDeposit(int accountId, decimal amount, string description, string externalId)
        {
            return Execute(() => _repository.PostDeposit(accountId, amount, description, externalId));
        }

        public TransactionDto PostWithdrawal(int accountId, decimal amount, string description, string externalId)
        {
            return Execute(() => _repository.PostWithdrawal(accountId, amount, description, externalId));
        }

        public TransferResultDto TransferFunds(int fromAccountId, int toAccountId, decimal amount, string description)
        {
            return Execute(() => _repository.TransferFunds(fromAccountId, toAccountId, amount, description));
        }

        public void ApplyMonthlyInterest(int? accountId)
        {
            Execute<object>(() => { _repository.ApplyMonthlyInterest(accountId); return null; });
        }

        public StatementDataDto GetStatementData(int accountId, DateTime periodStart, DateTime periodEnd)
        {
            return Execute(() => _repository.GetStatementData(accountId, periodStart, periodEnd));
        }

        private T Execute<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (FaultException<AccountFault>)
            {
                throw;
            }
            catch (SqlException ex)
            {
                _logger.Error("Stored procedure reported a business error.", ex);
                throw Fault(ClassifySqlError(ex), ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Unexpected error in AccountService.", ex);
                throw Fault(AccountFaultCodes.Unexpected, "An unexpected error occurred processing the request.");
            }
        }

        private static string ClassifySqlError(SqlException ex)
        {
            var message = ex.Message ?? string.Empty;
            if (message.IndexOf("was not found", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccountFaultCodes.NotFound;
            if (message.IndexOf("Insufficient funds", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccountFaultCodes.InsufficientFunds;
            if (message.IndexOf("is not active", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccountFaultCodes.AccountNotActive;
            if (message.IndexOf("must be greater than zero", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccountFaultCodes.InvalidRequest;
            return AccountFaultCodes.Unexpected;
        }

        private static FaultException<AccountFault> Fault(string code, string message)
        {
            return new FaultException<AccountFault>(new AccountFault(code, message), new FaultReason(message));
        }
    }
}
