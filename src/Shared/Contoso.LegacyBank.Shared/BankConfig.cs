using System;

namespace Contoso.LegacyBank.Shared
{
    /// <summary>
    /// Central place for the configuration defaults that make a fresh clone of this repo
    /// runnable with no external infrastructure: LocalDB connection string and the fixed
    /// local ports each self-hosted service listens on. Every value here can be overridden
    /// by an App.config/Web.config appSetting of the same name in a real deployment.
    /// </summary>
    public static class BankConfig
    {
        public const string DefaultConnectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ContosoLegacyBank;Integrated Security=True;Connect Timeout=30;";

        public const string AccountsServiceEndpoint = "http://localhost:8090/AccountService";
        public const string LoansServiceEndpoint = "http://localhost:8092/LoanService";
        public const string StatementsApiBaseUrl = "http://localhost:8091/";
        public const string NotificationsStatusPort = "8093";

        public const decimal LargeTransactionThreshold = 5000.00m;
        public const decimal OverdraftFeeAmount = 35.00m;
        public const decimal LoanLateFeeAmount = 25.00m;

        public static string GetConnectionString(string appSettingValue)
        {
            return string.IsNullOrWhiteSpace(appSettingValue) ? DefaultConnectionString : appSettingValue;
        }

        public static string GetSetting(string appSettingValue, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(appSettingValue) ? defaultValue : appSettingValue;
        }
    }
}
