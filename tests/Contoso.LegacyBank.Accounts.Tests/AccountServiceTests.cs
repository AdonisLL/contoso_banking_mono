using System;
using System.ServiceModel;
using Contoso.LegacyBank.Accounts.Business;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Accounts.Data;
using Contoso.LegacyBank.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Accounts.Tests
{
    /// <summary>
    /// Integration tests that exercise the real stored procedures against LocalDB through the
    /// business/service layer. These validate the business rules that live in the database
    /// (overdraft, fees, transfers, interest, the large-transaction notification trigger), not
    /// just the C# plumbing around them.
    /// </summary>
    [TestClass]
    public class AccountServiceTests
    {
        private static AccountService CreateService()
        {
            return new AccountService(new AccountsRepository(TestDatabase.ConnectionString), new SimpleLogger("Tests"));
        }

        [TestMethod]
        public void PostDeposit_IncreasesBalance()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m);
            var service = CreateService();

            var tx = service.PostDeposit(accountId, 50.00m, "Test deposit", null);

            Assert.AreEqual(150.00m, tx.BalanceAfter);
            Assert.AreEqual(150.00m, service.GetBalance(accountId));
        }

        [TestMethod]
        public void PostWithdrawal_WithinOverdraftLimit_Succeeds()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m, overdraftLimit: 200.00m);
            var service = CreateService();

            var tx = service.PostWithdrawal(accountId, 250.00m, "Over-balance withdrawal", null);

            Assert.AreEqual(-150.00m, tx.BalanceAfter);
        }

        [TestMethod]
        public void PostWithdrawal_CrossingIntoOverdraft_AppliesOverdraftFee()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m, overdraftLimit: 200.00m);
            var service = CreateService();

            service.PostWithdrawal(accountId, 150.00m, "Crosses into overdraft", null);

            // Balance should reflect -50.00 withdrawal result minus the 35.00 overdraft fee.
            var balance = service.GetBalance(accountId);
            Assert.AreEqual(-85.00m, balance);

            var history = service.GetTransactionHistory(accountId, null, null);
            Assert.IsTrue(Array.Exists(history, t => t.TransactionType == "Fee"));
        }

        [TestMethod]
        public void PostWithdrawal_BeyondOverdraftLimit_ThrowsInsufficientFundsFault()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m, overdraftLimit: 50.00m);
            var service = CreateService();

            var ex = Assert.ThrowsException<FaultException<AccountFault>>(() =>
                service.PostWithdrawal(accountId, 500.00m, "Should fail", null));

            Assert.AreEqual(AccountFaultCodes.InsufficientFunds, ex.Detail.Code);
            Assert.AreEqual(100.00m, service.GetBalance(accountId), "Balance must be unchanged after a rejected withdrawal.");
        }

        [TestMethod]
        public void TransferFunds_MovesMoneyBetweenAccountsAtomically()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var fromAccountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 500.00m);
            var toAccountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m);
            var service = CreateService();

            var result = service.TransferFunds(fromAccountId, toAccountId, 200.00m, "Test transfer");

            Assert.AreEqual(300.00m, result.FromAccountBalance);
            Assert.AreEqual(300.00m, result.ToAccountBalance);
        }

        [TestMethod]
        public void ApplyMonthlyInterest_PostsInterestForPositiveBalanceAccount()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 1200.00m, interestRate: 0.12m);
            var service = CreateService();

            service.ApplyMonthlyInterest(accountId);

            // 1200 * (0.12/12) = 12.00
            Assert.AreEqual(1212.00m, service.GetBalance(accountId));
        }

        [TestMethod]
        public void PostDeposit_AboveLargeTransactionThreshold_EnqueuesNotificationViaTrigger()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var accountId = TestDatabase.CreateTestAccount(customerId, Guid.NewGuid().ToString("N").Substring(0, 8), 100.00m);
            var service = CreateService();

            service.PostDeposit(accountId, 9000.00m, "Large deposit", null);

            Assert.AreEqual(1, TestDatabase.CountNotificationsForCustomer(customerId, "LargeTransaction"));
        }

        [TestMethod]
        public void GetAccountByNumber_UnknownNumber_ThrowsNotFoundFault()
        {
            var service = CreateService();

            var ex = Assert.ThrowsException<FaultException<AccountFault>>(() =>
                service.GetAccountByNumber("000000000000"));

            Assert.AreEqual(AccountFaultCodes.NotFound, ex.Detail.Code);
        }
    }
}
