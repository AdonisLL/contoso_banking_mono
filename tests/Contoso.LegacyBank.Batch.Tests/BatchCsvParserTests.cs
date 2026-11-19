using System;
using Contoso.LegacyBank.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.LegacyBank.Batch.Tests
{
    [TestClass]
    public class BatchCsvParserTests
    {
        [TestMethod]
        public void Parse_ValidRow_ReturnsExpectedFields()
        {
            var row = BatchCsvParser.Parse("BATCH-0001,100000000003,250.00,Deposit");

            Assert.AreEqual("BATCH-0001", row.ExternalId);
            Assert.AreEqual("100000000003", row.AccountNumber);
            Assert.AreEqual(250.00m, row.Amount);
            Assert.AreEqual("Deposit", row.TransactionType);
        }

        [TestMethod]
        public void Parse_WrongColumnCount_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() => BatchCsvParser.Parse("this is not a valid row"));
        }

        [TestMethod]
        public void Parse_InvalidAmount_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() => BatchCsvParser.Parse("BATCH-0002,100000000003,not-a-number,Deposit"));
        }
    }
}
