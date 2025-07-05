using System.Text;
using Blocky.Data;
using Blocky.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class PacFileProvider(ILogger<PacFileProvider> logger, ICachedBlockyRuleRepo ruleRepo, ISettingsService settingsService) : IPacFileProvider
{
    public async Task<string> GetAsync()
    {
        var settings = await settingsService.GetSettingsAsync();
        var rules = (await ruleRepo.GetAllRulesAsync())
            .Where(r => r.IsEnabled && !string.IsNullOrWhiteSpace(r.Domain))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("function FindProxyForURL(url, host) {");
        sb.AppendLine("    var now = new Date();");
        sb.AppendLine("    var h = now.getHours();");
        sb.AppendLine("    var m = now.getMinutes();");

        foreach (var rule in rules)
        {
            if (rule is { HasTimeRestriction: true, StartTime: not null, EndTime: not null })
            {
                var startH = rule.StartTime.Value.Hours;
                var startM = rule.StartTime.Value.Minutes;
                var endH = rule.EndTime.Value.Hours;
                var endM = rule.EndTime.Value.Minutes;

                sb.AppendLine($"    if (dnsDomainIs(host, \"{rule.Domain}\")) {{");
                sb.AppendLine($"        if ((h > {startH} || (h == {startH} && m >= {startM})) && (h < {endH} || (h == {endH} && m < {endM}))) return \"PROXY 127.0.0.1:{settings.ProxyPort}\";");
                sb.AppendLine($"        else return \"DIRECT\";");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    if (dnsDomainIs(host, \"{rule.Domain}\")) return \"PROXY 127.0.0.1:{settings.ProxyPort}\";");
            }
        }

        sb.AppendLine("    return \"DIRECT\";");
        sb.AppendLine("}");
        var pac = sb.ToString();
        logger.LogInformation("Generated PAC file with {ruleCount} rules", rules.Count);
        logger.LogInformation("PAC file content:\n{pacContent}", pac);
        return pac;
    }
}