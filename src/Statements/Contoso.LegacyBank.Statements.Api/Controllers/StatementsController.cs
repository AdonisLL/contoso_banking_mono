using System;
using System.Linq;
using System.ServiceModel;
using System.Web.Http;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Shared;
using Contoso.LegacyBank.Statements.Api.Data;
using Contoso.LegacyBank.Statements.Api.Services;

namespace Contoso.LegacyBank.Statements.Api.Controllers
{
    public class StatementRequest
    {
        public string AccountNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class StatementStatusResponse
    {
        public int StatementId { get; set; }
        public string Status { get; set; }
        public DateTime RequestedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string FilePath { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Statement orchestration: validates the account exists (via Accounts over WCF), writes a
    /// self-contained job file for the Documents worker, and reports status by reconciling the
    /// local Statements table against the worker's completion/failure metadata files.
    /// </summary>
    public class StatementsController : ApiController
    {
        private readonly StatementsRepository _repository;
        private readonly SimpleLogger _logger;

        public StatementsController() : this(new StatementsRepository(), new SimpleLogger("Statements")) { }

        public StatementsController(StatementsRepository repository, SimpleLogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpPost]
        [Route("api/statements")]
        public IHttpActionResult RequestStatement([FromBody] StatementRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccountNumber))
                return BadRequest("accountNumber is required.");
            if (request.PeriodEnd < request.PeriodStart)
                return BadRequest("periodEnd must not be before periodStart.");

            IAccountService accountService = AccountServiceClientFactory.CreateClient();
            AccountDto account;
            StatementDataDto statementData;
            try
            {
                account = accountService.GetAccountByNumber(request.AccountNumber);
                statementData = accountService.GetStatementData(account.AccountId, request.PeriodStart, request.PeriodEnd);
            }
            catch (FaultException<AccountFault> ex) when (ex.Detail.Code == AccountFaultCodes.NotFound)
            {
                return NotFound();
            }

            var statementId = _repository.Create(account.AccountId, request.PeriodStart, request.PeriodEnd);
            _repository.MarkProcessing(statementId);

            var job = new StatementJob
            {
                StatementId = statementId,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                CustomerNumber = account.Customer.CustomerNumber,
                CustomerName = $"{account.Customer.FirstName} {account.Customer.LastName}",
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                ClosingBalance = account.Balance,
                GeneratedUtc = DateTime.UtcNow,
                Transactions = statementData.Transactions.Select(t => new StatementJobTransaction
                {
                    TransactionId = t.TransactionId,
                    TransactionType = t.TransactionType,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description,
                    CreatedUtc = t.CreatedUtc
                }).ToArray()
            };

            AppPaths.EnsureAll();
            var jobPath = System.IO.Path.Combine(AppPaths.StatementsPendingRoot, $"{statementId}.json");
            JsonFile.WriteAtomic(jobPath, job);
            _logger.Info($"Wrote statement job {statementId} for account {account.AccountNumber} to {jobPath}");

            return Content(System.Net.HttpStatusCode.Accepted, new StatementStatusResponse
            {
                StatementId = statementId,
                Status = "Processing",
                RequestedUtc = DateTime.UtcNow
            });
        }

        [HttpGet]
        [Route("api/statements/{id:int}")]
        public IHttpActionResult GetStatus(int id)
        {
            var record = _repository.GetById(id);
            if (record == null)
                return NotFound();

            if (record.Status == "Processing")
                record = ReconcileWithWorkerOutput(record);

            return Ok(new StatementStatusResponse
            {
                StatementId = record.StatementId,
                Status = record.Status,
                RequestedUtc = record.RequestedUtc,
                CompletedUtc = record.CompletedUtc,
                FilePath = record.FilePath,
                FailureReason = record.FailureReason
            });
        }

        private StatementRecord ReconcileWithWorkerOutput(StatementRecord record)
        {
            var completedMetadataPath = System.IO.Path.Combine(AppPaths.StatementsCompletedRoot, $"{record.StatementId}.json");
            var failedMetadataPath = System.IO.Path.Combine(AppPaths.StatementsFailedRoot, $"{record.StatementId}.json");

            if (System.IO.File.Exists(completedMetadataPath))
            {
                var completion = JsonFile.Read<StatementCompletion>(completedMetadataPath);
                _repository.MarkCompleted(record.StatementId, completion.FilePath);
                return _repository.GetById(record.StatementId);
            }

            if (System.IO.File.Exists(failedMetadataPath))
            {
                var completion = JsonFile.Read<StatementCompletion>(failedMetadataPath);
                _repository.MarkFailed(record.StatementId, completion.FailureReason);
                return _repository.GetById(record.StatementId);
            }

            return record;
        }
    }
}
