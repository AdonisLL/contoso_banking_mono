using System;
using System.Globalization;

namespace Contoso.LegacyBank.Batch
{
    public class BatchCsvRow
    {
        public string ExternalId { get; set; }
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
    }

    /// <summary>
    /// Parses the simple "ExternalId,AccountNumber,Amount,TransactionType" CSV format the
    /// nightly feed arrives in. No quoting/escaping support - matches a real legacy mainframe
    /// extract, which is exactly why malformed rows are routed to the error report instead of
    /// crashing the whole batch.
    /// </summary>
    public static class BatchCsvParser
    {
        public static BatchCsvRow Parse(string rawLine)
        {
            var fields = rawLine.Split(',');
            if (fields.Length != 4)
                throw new FormatException($"Expected 4 columns, found {fields.Length}.");

            if (!decimal.TryParse(fields[2].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                throw new FormatException($"'{fields[2]}' is not a valid amount.");

            return new BatchCsvRow
            {
                ExternalId = fields[0].Trim(),
                AccountNumber = fields[1].Trim(),
                Amount = amount,
                TransactionType = fields[3].Trim()
            };
        }
    }
}
