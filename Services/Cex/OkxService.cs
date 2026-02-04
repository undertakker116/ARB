using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class OkxService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<OkxService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("okx");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<OkxService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/api/v5/market/tickers?instType=SPOT");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("data", out JsonElement data))
                        {
                            _cache.Set("okx_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in Okx response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited Okx prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/OkxPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited Okx prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}