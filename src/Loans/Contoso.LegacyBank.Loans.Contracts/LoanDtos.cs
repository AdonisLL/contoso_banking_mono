using System;
using System.Runtime.Serialization;

namespace Contoso.LegacyBank.Loans.Contracts
{
    [DataContract]
    public class LoanProductDto
    {
        [DataMember] public int LoanProductId { get; set; }
        [DataMember] public string ProductCode { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public decimal AnnualInterestRate { get; set; }
        [DataMember] public int MinTermMonths { get; set; }
        [DataMember] public int MaxTermMonths { get; set; }
        [DataMember] public decimal MinAmount { get; set; }
        [DataMember] public decimal MaxAmount { get; set; }
    }

    [DataContract]
    public class LoanDto
    {
        [DataMember] public int LoanId { get; set; }
        [DataMember] public string LoanNumber { get; set; }
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public string CustomerNumber { get; set; }
        [DataMember] public string FirstName { get; set; }
        [DataMember] public string LastName { get; set; }
        [DataMember] public int LoanProductId { get; set; }
        [DataMember] public string ProductCode { get; set; }
        [DataMember] public string ProductName { get; set; }
        [DataMember] public decimal PrincipalAmount { get; set; }
        [DataMember] public decimal AnnualInterestRate { get; set; }
        [DataMember] public int TermMonths { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public DateTime OriginatedUtc { get; set; }
        [DataMember] public DateTime MaturityDate { get; set; }
        [DataMember] public decimal OutstandingPrincipal { get; set; }
    }

    [DataContract]
    public class LoanScheduleEntryDto
    {
        [DataMember] public long LoanScheduleId { get; set; }
        [DataMember] public int LoanId { get; set; }
        [DataMember] public int InstallmentNumber { get; set; }
        [DataMember] public DateTime DueDate { get; set; }
        [DataMember] public decimal PrincipalDue { get; set; }
        [DataMember] public decimal InterestDue { get; set; }
        [DataMember] public decimal TotalDue { get; set; }
        [DataMember] public string Status { get; set; }
    }

    [DataContract]
    public class LoanOriginationResultDto
    {
        [DataMember] public int LoanId { get; set; }
        [DataMember] public string LoanNumber { get; set; }
        [DataMember] public LoanScheduleEntryDto[] Schedule { get; set; }
    }

    [DataContract]
    public class LoanPaymentResultDto
    {
        [DataMember] public long LoanPaymentId { get; set; }
        [DataMember] public decimal AmountPaid { get; set; }
        [DataMember] public decimal PrincipalApplied { get; set; }
        [DataMember] public decimal InterestApplied { get; set; }
        [DataMember] public decimal LateFeeApplied { get; set; }
        [DataMember] public decimal RemainingOutstandingPrincipal { get; set; }
        [DataMember] public string LoanStatus { get; set; }
    }
}
