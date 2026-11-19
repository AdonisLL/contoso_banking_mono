using System;
using System.Configuration;
using Contoso.LegacyBank.Shared;
using Microsoft.Owin.Hosting;

namespace Contoso.LegacyBank.Statements.Api
{
    /// <summary>
    /// Self-hosted OWIN console host for the Statements Web API 2 service, listening at
    /// http://localhost:8091/. OWIN's HttpListener-based self-host binds the explicit
    /// "localhost" hostname (not a "+" wildcard), so - unlike the WCF HTTP self-host used by
    /// Accounts/Loans - it does not require an elevated "netsh http add urlacl" reservation.
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            var logger = new SimpleLogger("Statements");
            AppPaths.EnsureAll();

            var baseUrl = BankConfig.GetSetting(ConfigurationManager.AppSettings["StatementsApiBaseUrl"], BankConfig.StatementsApiBaseUrl);

            using (WebApp.Start<Startup>(baseUrl))
            {
                logger.Info($"Statements API is open at {baseUrl}");
                Console.WriteLine($"Contoso Legacy Bank - Statements API running at {baseUrl}");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
            }
        }
    }
}
