using System.ServiceModel;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Loans.Contracts;

namespace Contoso.LegacyBank.Portal.Services
{
    /// <summary>
    /// Creates per-call WCF channels to Accounts and Loans, exactly like Statements.Api does -
    /// the Portal is just another synchronous SOAP client, same as the real teller desktop app.
    /// </summary>
    public static class ServiceClients
    {
        public static IAccountService CreateAccountClient()
        {
            return new ChannelFactory<IAccountService>("AccountServiceClient").CreateChannel();
        }

        public static ILoanService CreateLoanClient()
        {
            return new ChannelFactory<ILoanService>("LoanServiceClient").CreateChannel();
        }
    }
}
