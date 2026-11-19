using System.Runtime.Serialization;

namespace Contoso.LegacyBank.Accounts.Contracts
{
    /// <summary>
    /// SOAP fault payload surfaced to clients when a stored procedure raises a business-rule
    /// error (insufficient funds, unknown account, inactive account, etc). The service layer
    /// maps SqlException business errors (state 1, severity 16 from RAISERROR) onto this fault
    /// so the underlying database error text/number is never leaked to callers.
    /// </summary>
    [DataContract]
    public class AccountFault
    {
        [DataMember] public string Code { get; set; }
        [DataMember] public string Message { get; set; }

        public AccountFault() { }

        public AccountFault(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    public static class AccountFaultCodes
    {
        public const string NotFound = "AccountNotFound";
        public const string InsufficientFunds = "InsufficientFunds";
        public const string AccountNotActive = "AccountNotActive";
        public const string InvalidRequest = "InvalidRequest";
        public const string Unexpected = "Unexpected";
    }
}
