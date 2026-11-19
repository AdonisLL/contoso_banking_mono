using System;
using System.ServiceModel;
using Contoso.LegacyBank.Accounts.Business;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Accounts.ServiceHost
{
    /// <summary>
    /// Self-hosted WCF console host for the Accounts service. Run directly (or via
    /// scripts\Start-Demo.ps1); the service listens at http://localhost:8090/AccountService
    /// with a basicHttpBinding endpoint plus mex metadata, matching legacy intranet SOAP hosting.
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            var logger = new SimpleLogger("Accounts");
            AppPaths.EnsureAll();

            using (var host = new System.ServiceModel.ServiceHost(typeof(AccountService)))
            {
                host.Opened += (s, e) => logger.Info("Accounts service is open.");
                host.Faulted += (s, e) => logger.Error("Accounts service host faulted.");

                host.Open();
                Console.WriteLine("Contoso Legacy Bank - Accounts service host running at net.tcp://localhost:8090/AccountService");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
                host.Close();
            }
        }
    }
}
