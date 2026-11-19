using System;
using System.Runtime.Serialization;

namespace Contoso.LegacyBank.Accounts.Contracts
{
    [DataContract]
    public class CustomerDto
    {
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public string CustomerNumber { get; set; }
        [DataMember] public string FirstName { get; set; }
        [DataMember] public string LastName { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Phone { get; set; }
    }

    [DataContract]
    public class AccountDto
    {
        [DataMember] public int AccountId { get; set; }
        [DataMember] public string AccountNumber { get; set; }
        [DataMember] public string AccountType { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public decimal Balance { get; set; }
        [DataMember] public decimal OverdraftLimit { get; set; }
        [DataMember] public decimal InterestRate { get; set; }
        [DataMember] public DateTime OpenedUtc { get; set; }
        [DataMember] public DateTime? ClosedUtc { get; set; }
        [DataMember] public CustomerDto Customer { get; set; }
    }

    [DataContract]
    public class TransactionDto
    {
        [DataMember] public long TransactionId { get; set; }
        [DataMember] public int AccountId { get; set; }
        [DataMember] public string TransactionType { get; set; }
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public decimal BalanceAfter { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string ExternalId { get; set; }
        [DataMember] public long? RelatedTransactionId { get; set; }
        [DataMember] public DateTime CreatedUtc { get; set; }
    }

    [DataContract]
    public class TransferResultDto
    {
        [DataMember] public long FromTransactionId { get; set; }
        [DataMember] public long ToTransactionId { get; set; }
        [DataMember] public decimal FromAccountBalance { get; set; }
        [DataMember] public decimal ToAccountBalance { get; set; }
    }

    [DataContract]
    public class StatementDataDto
    {
        [DataMember] public AccountDto Account { get; set; }
        [DataMember] public TransactionDto[] Transactions { get; set; }
        [DataMember] public DateTime PeriodStart { get; set; }
        [DataMember] public DateTime PeriodEnd { get; set; }
    }
}
