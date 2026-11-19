using System.ServiceModel;
using Contoso.LegacyBank.Accounts.Contracts;

namespace Contoso.LegacyBank.Statements.Api.Services
{
    /// <summary>
    /// Creates short-lived WCF channels to the Accounts service for each call, matching the
    /// legacy "new channel per request" pattern (no shared ChannelFactory caching/reuse,
    /// which is itself a performance seam the modernization target would fix with pooled or
    /// gRPC clients).
    /// </summary>
    public static class AccountServiceClientFactory
    {
        public static IAccountService CreateClient()
        {
            var factory = new ChannelFactory<IAccountService>("AccountServiceClient");
            return factory.CreateChannel();
        }
    }
}
