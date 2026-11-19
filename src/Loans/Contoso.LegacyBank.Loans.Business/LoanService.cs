using System;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using Contoso.LegacyBank.Loans.Contracts;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Loans.Business
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class LoanService : ILoanService
    {
        private readonly LoansRepository _repository;
        private readonly SimpleLogger _logger;

        public LoanService() : this(new LoansRepository(), new SimpleLogger("Loans")) { }

        public LoanService(LoansRepository repository, SimpleLogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public LoanProductDto[] ListLoanProducts() => Execute(() => _repository.ListLoanProducts().ToArray());

        public LoanDto GetLoanById(int loanId)
        {
            return Execute(() =>
            {
                var loan = _repository.GetById(loanId);
                if (loan == null)
                    throw Fault(LoanFaultCodes.NotFound, $"Loan {loanId} was not found.");
                return loan;
            });
        }

        public LoanDto GetLoanByNumber(string loanNumber)
        {
            return Execute(() =>
            {
                var loan = _repository.GetByNumber(loanNumber);
                if (loan == null)
                    throw Fault(LoanFaultCodes.NotFound, $"Loan '{loanNumber}' was not found.");
                return loan;
            });
        }

        public LoanDto[] SearchLoans(string searchTerm) => Execute(() => _repository.Search(searchTerm).ToArray());

        public LoanScheduleEntryDto[] GetLoanSchedule(int loanId) => Execute(() => _repository.GetSchedule(loanId).ToArray());

        public LoanOriginationResultDto OriginateLoan(int customerId, int loanProductId, decimal principalAmount, int termMonths)
        {
            return Execute(() =>
            {
                var (loanId, loanNumber) = _repository.Originate(customerId, loanProductId, principalAmount, termMonths);
                var schedule = _repository.GetSchedule(loanId).ToArray();
                return new LoanOriginationResultDto
                {
                    LoanId = loanId,
                    LoanNumber = loanNumber,
                    Schedule = schedule
                };
            });
        }

        public LoanPaymentResultDto ApplyPayment(int loanId, decimal amountPaid)
        {
            return Execute(() => _repository.ApplyPayment(loanId, amountPaid));
        }

        private T Execute<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (FaultException<LoanFault>)
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
                _logger.Error("Unexpected error in LoanService.", ex);
                throw Fault(LoanFaultCodes.Unexpected, "An unexpected error occurred processing the request.");
            }
        }

        private static string ClassifySqlError(SqlException ex)
        {
            var message = ex.Message ?? string.Empty;
            if (message.IndexOf("was not found", StringComparison.OrdinalIgnoreCase) >= 0)
                return LoanFaultCodes.NotFound;
            if (message.IndexOf("outside the allowed range", StringComparison.OrdinalIgnoreCase) >= 0)
                return LoanFaultCodes.OutOfRange;
            if (message.IndexOf("must be greater than zero", StringComparison.OrdinalIgnoreCase) >= 0)
                return LoanFaultCodes.InvalidRequest;
            return LoanFaultCodes.Unexpected;
        }

        private static FaultException<LoanFault> Fault(string code, string message)
        {
            return new FaultException<LoanFault>(new LoanFault(code, message), new FaultReason(message));
        }
    }
}
