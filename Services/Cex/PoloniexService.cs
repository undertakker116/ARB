using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class PoloniexService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<PoloniexService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("poloniex");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<PoloniexService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/markets/price");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            _cache.Set("poloniex_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("Poloniex response is not an array.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Poloniex prices response is null or empty.");
                    }
                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/PoloniexPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error fetching poloniex prices: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
