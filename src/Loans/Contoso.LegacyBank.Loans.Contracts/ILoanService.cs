using System.ServiceModel;

namespace Contoso.LegacyBank.Loans.Contracts
{
    /// <summary>
    /// The Loans service contract: product catalog, origination (which builds an amortization
    /// schedule), schedule lookup, and payment application. As with Accounts, the amortization
    /// math and payment-allocation rules live in stored procedures - this is a façade.
    /// </summary>
    [ServiceContract(Namespace = "http://contoso.com/legacybank/loans/2024")]
    public interface ILoanService
    {
        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanProductDto[] ListLoanProducts();

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanDto GetLoanById(int loanId);

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanDto GetLoanByNumber(string loanNumber);

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanDto[] SearchLoans(string searchTerm);

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanScheduleEntryDto[] GetLoanSchedule(int loanId);

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanOriginationResultDto OriginateLoan(int customerId, int loanProductId, decimal principalAmount, int termMonths);

        [OperationContract]
        [FaultContract(typeof(LoanFault))]
        LoanPaymentResultDto ApplyPayment(int loanId, decimal amountPaid);
    }
}
