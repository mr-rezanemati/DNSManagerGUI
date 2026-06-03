using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DnsClient;

namespace DNSManagerGUI.Services;

public class DnsResolver
{
    public async Task<bool> TestDnsAsync(string dnsServer, string testDomain = "google.com")
    {
        try
        {
            var options = new LookupClientOptions(IPAddress.Parse(dnsServer))
            {
                Timeout = TimeSpan.FromSeconds(5),
                UseCache = false,
                Retries = 1
            };

            var client = new LookupClient(options);
            var result = await client.QueryAsync(testDomain, QueryType.A);
            return !result.HasError && result.Answers.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task<double> MeasureLatencyAsync(string dnsServer, string testDomain = "google.com")
    {
        try
        {
            var options = new LookupClientOptions(IPAddress.Parse(dnsServer))
            {
                Timeout = TimeSpan.FromSeconds(5),
                UseCache = false,
                Retries = 0
            };

            var client = new LookupClient(options);
            var sw = Stopwatch.StartNew();
            var result = await client.QueryAsync(testDomain, QueryType.A);
            sw.Stop();

            return result.HasError ? -1 : sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    public async Task<List<IPAddress>> ResolveAsync(string domain, string? dnsServer = null)
    {
        try
        {
            LookupClient client;
            if (dnsServer != null)
            {
                var options = new LookupClientOptions(IPAddress.Parse(dnsServer))
                {
                    Timeout = TimeSpan.FromSeconds(5),
                    UseCache = false
                };
                client = new LookupClient(options);
            }
            else
            {
                client = new LookupClient();
            }

            var result = await client.QueryAsync(domain, QueryType.A);
            return result.Answers.ARecords().Select(r => r.Address).ToList();
        }
        catch
        {
            return new List<IPAddress>();
        }
    }
}
