using System;

namespace Contoso.LegacyBank.Portal.Models
{
    public class AccountRow
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerNumber { get; set; }
        public string AccountType { get; set; }
        public string Status { get; set; }
        public decimal Balance { get; set; }
        public decimal OverdraftLimit { get; set; }
    }

    public class TransactionRow
    {
        public long TransactionId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; }
    }

    public class LoanRow
    {
        public int LoanId { get; set; }
        public string LoanNumber { get; set; }
        public string CustomerName { get; set; }
        public string ProductName { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal OutstandingPrincipal { get; set; }
        public string Status { get; set; }
        public DateTime MaturityDate { get; set; }
    }

    public class ScheduleRow
    {
        public int InstallmentNumber { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalDue { get; set; }
        public decimal InterestDue { get; set; }
        public decimal TotalDue { get; set; }
        public string Status { get; set; }
    }
}
