using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Contoso.LegacyBank.Notifications
{
    /// <summary>
    /// Renders a notification body by substituting "{propertyName}" placeholders with values
    /// from the notification's JSON payload - a minimal stand-in for a real templating engine.
    /// </summary>
    public static class TemplateRenderer
    {
        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        public static string Render(string template, string payloadJson)
        {
            var payload = JObject.Parse(payloadJson);
            return PlaceholderPattern.Replace(template, match =>
            {
                var propertyName = match.Groups[1].Value;
                var token = payload.GetValue(propertyName, System.StringComparison.OrdinalIgnoreCase);
                return token?.ToString() ?? match.Value;
            });
        }
    }
}
