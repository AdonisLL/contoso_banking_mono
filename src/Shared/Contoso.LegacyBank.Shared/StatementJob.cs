using System;

namespace Contoso.LegacyBank.Shared
{
    /// <summary>
    /// The file-queue "job" contract between the Statements API and the Documents worker.
    /// Statements resolves everything the worker needs (customer/account/transaction data,
    /// fetched from Accounts over WCF) and writes one complete JSON file per statement request;
    /// the worker never calls back into Accounts or the database to do its job. This same
    /// machine-bound file queue is a documented modernization seam (see docs/architecture.md).
    /// </summary>
    public class StatementJob
    {
        public int StatementId { get; set; }
        public string AccountNumber { get; set; }
        public string AccountType { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal OpeningBalanceHint { get; set; }
        public decimal ClosingBalance { get; set; }
        public DateTime GeneratedUtc { get; set; }
        public StatementJobTransaction[] Transactions { get; set; } = Array.Empty<StatementJobTransaction>();
    }

    public class StatementJobTransaction
    {
        public long TransactionId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>Metadata the Documents worker drops once a job finishes (success or failure).</summary>
    public class StatementCompletion
    {
        public int StatementId { get; set; }
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string FailureReason { get; set; }
        public DateTime CompletedUtc { get; set; }
    }
}
