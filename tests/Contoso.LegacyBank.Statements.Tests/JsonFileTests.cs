using System;
using System.IO;
using Contoso.LegacyBank.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Statements.Tests
{
    [TestClass]
    public class JsonFileTests
    {
        [TestMethod]
        public void WriteAtomicThenRead_RoundTripsStatementJob()
        {
            var job = new StatementJob
            {
                StatementId = 42,
                AccountNumber = "100000000001",
                CustomerName = "Ava Thompson",
                PeriodStart = new DateTime(2026, 1, 1),
                PeriodEnd = new DateTime(2026, 1, 31),
                Transactions = new[]
                {
                    new StatementJobTransaction { TransactionId = 1, TransactionType = "Deposit", Amount = 100m, BalanceAfter = 100m }
                }
            };

            var path = Path.Combine(Path.GetTempPath(), $"job-{Guid.NewGuid():N}.json");
            try
            {
                JsonFile.WriteAtomic(path, job);
                Assert.IsFalse(File.Exists(path + ".tmp"), "Temp file should not remain after an atomic write.");

                var roundTripped = JsonFile.Read<StatementJob>(path);

                Assert.AreEqual(job.StatementId, roundTripped.StatementId);
                Assert.AreEqual(job.AccountNumber, roundTripped.AccountNumber);
                Assert.AreEqual(1, roundTripped.Transactions.Length);
                Assert.AreEqual(job.Transactions[0].Amount, roundTripped.Transactions[0].Amount);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
