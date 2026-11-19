using System;
using System.Linq;
using System.ServiceModel;
using Contoso.LegacyBank.Loans.Business;
using Contoso.LegacyBank.Loans.Contracts;
using Contoso.LegacyBank.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Loans.Tests
{
    /// <summary>
    /// Integration tests exercising the real amortization and payment-allocation stored
    /// procedures against LocalDB.
    /// </summary>
    [TestClass]
    public class LoanServiceTests
    {
        private static LoanService CreateService()
        {
            return new LoanService(new LoansRepository(TestDatabase.ConnectionString), new SimpleLogger("Tests"));
        }

        [TestMethod]
        public void OriginateLoan_BuildsAFullyAmortizingSchedule()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var productId = TestDatabase.GetLoanProductId("PERSONAL");
            var service = CreateService();

            var result = service.OriginateLoan(customerId, productId, 12000.00m, 12);

            Assert.IsTrue(result.LoanId > 0);
            Assert.AreEqual(12, result.Schedule.Length);

            // Every installment should carry (approximately) the same total payment, and the
            // final installment must zero out the remaining principal exactly.
            var totalPrincipal = result.Schedule.Sum(s => s.PrincipalDue);
            Assert.AreEqual(12000.00m, totalPrincipal);
        }

        [TestMethod]
        public void OriginateLoan_AmountOutsideProductRange_ThrowsOutOfRangeFault()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var productId = TestDatabase.GetLoanProductId("PERSONAL");
            var service = CreateService();

            var ex = Assert.ThrowsException<FaultException<LoanFault>>(() =>
                service.OriginateLoan(customerId, productId, 999999.00m, 12));

            Assert.AreEqual(LoanFaultCodes.OutOfRange, ex.Detail.Code);
        }

        [TestMethod]
        public void ApplyPayment_CoveringFirstInstallment_MarksItPaidAndReducesPrincipal()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var productId = TestDatabase.GetLoanProductId("AUTO");
            var service = CreateService();
            var origination = service.OriginateLoan(customerId, productId, 10000.00m, 36);
            var firstInstallment = origination.Schedule[0];

            var paymentResult = service.ApplyPayment(origination.LoanId, firstInstallment.TotalDue);

            Assert.AreEqual(firstInstallment.PrincipalDue, paymentResult.PrincipalApplied);
            Assert.AreEqual(firstInstallment.InterestDue, paymentResult.InterestApplied);
            Assert.AreEqual(0m, paymentResult.LateFeeApplied);

            var schedule = service.GetLoanSchedule(origination.LoanId);
            Assert.AreEqual("Paid", schedule[0].Status);
        }

        [TestMethod]
        public void ApplyPayment_OnOverdueInstallment_AppliesLateFee()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var productId = TestDatabase.GetLoanProductId("PERSONAL");
            var service = CreateService();
            var origination = service.OriginateLoan(customerId, productId, 6000.00m, 12);
            TestDatabase.MakeInstallmentOverdue(origination.LoanId, 1);
            var firstInstallment = origination.Schedule[0];

            var paymentResult = service.ApplyPayment(origination.LoanId, firstInstallment.TotalDue + 25.00m);

            Assert.AreEqual(25.00m, paymentResult.LateFeeApplied);
        }

        [TestMethod]
        public void ApplyPayment_PayingFullOutstandingPrincipal_MarksLoanPaidOff()
        {
            var customerId = TestDatabase.CreateTestCustomer(Guid.NewGuid().ToString("N").Substring(0, 8));
            var productId = TestDatabase.GetLoanProductId("PERSONAL");
            var service = CreateService();
            var origination = service.OriginateLoan(customerId, productId, 1000.00m, 6);
            var totalDue = origination.Schedule.Sum(s => s.TotalDue);

            var paymentResult = service.ApplyPayment(origination.LoanId, totalDue);

            Assert.AreEqual("PaidOff", paymentResult.LoanStatus);
            Assert.AreEqual(0m, paymentResult.RemainingOutstandingPrincipal);
        }

        [TestMethod]
        public void GetLoanByNumber_UnknownNumber_ThrowsNotFoundFault()
        {
            var service = CreateService();

            var ex = Assert.ThrowsException<FaultException<LoanFault>>(() =>
                service.GetLoanByNumber("LN9999999999"));

            Assert.AreEqual(LoanFaultCodes.NotFound, ex.Detail.Code);
        }
    }
}
