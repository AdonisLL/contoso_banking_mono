using System;
using Contoso.LegacyBank.Loans.Business;
using Contoso.LegacyBank.Shared;

namespace Contoso.LegacyBank.Loans.ServiceHost
{
    /// <summary>
    /// Self-hosted WCF console host for the Loans service, listening at
    /// net.tcp://localhost:8092/LoanService (see Accounts.ServiceHost for why net.tcp is used
    /// instead of basicHttpBinding).
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            var logger = new SimpleLogger("Loans");
            AppPaths.EnsureAll();

            using (var host = new System.ServiceModel.ServiceHost(typeof(LoanService)))
            {
                host.Opened += (s, e) => logger.Info("Loans service is open.");
                host.Faulted += (s, e) => logger.Error("Loans service host faulted.");

                host.Open();
                Console.WriteLine("Contoso Legacy Bank - Loans service host running at net.tcp://localhost:8092/LoanService");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
                host.Close();
            }
        }
    }
}
