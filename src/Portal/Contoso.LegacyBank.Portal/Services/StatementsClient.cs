using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Contoso.LegacyBank.Shared;
using Newtonsoft.Json;

namespace Contoso.LegacyBank.Portal.Services
{
    public class StatementRequestModel
    {
        public string AccountNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class StatementStatusModel
    {
        public int StatementId { get; set; }
        public string Status { get; set; }
        public DateTime RequestedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string FilePath { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>Thin JSON/HTTP client for the Statements Web API 2 service.</summary>
    public class StatementsClient
    {
        private readonly string _baseUrl;

        public StatementsClient()
        {
            _baseUrl = BankConfig.GetSetting(ConfigurationManager.AppSettings["StatementsApiBaseUrl"], BankConfig.StatementsApiBaseUrl).TrimEnd('/');
        }

        public async Task<StatementStatusModel> RequestStatementAsync(StatementRequestModel request)
        {
            using (var client = new HttpClient())
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_baseUrl}/api/statements", content);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StatementStatusModel>(body);
            }
        }

        public async Task<StatementStatusModel> GetStatusAsync(int statementId)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"{_baseUrl}/api/statements/{statementId}");
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StatementStatusModel>(body);
            }
        }
    }
}
