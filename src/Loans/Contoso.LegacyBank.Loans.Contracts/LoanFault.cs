using System.Runtime.Serialization;

namespace Contoso.LegacyBank.Loans.Contracts
{
    [DataContract]
    public class LoanFault
    {
        [DataMember] public string Code { get; set; }
        [DataMember] public string Message { get; set; }

        public LoanFault() { }

        public LoanFault(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    public static class LoanFaultCodes
    {
        public const string NotFound = "LoanNotFound";
        public const string InvalidProduct = "InvalidProduct";
        public const string OutOfRange = "OutOfRange";
        public const string InvalidRequest = "InvalidRequest";
        public const string Unexpected = "Unexpected";
    }
}
