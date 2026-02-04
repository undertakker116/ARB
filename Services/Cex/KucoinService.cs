using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class KucoinService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<KucoinService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("kucoin");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<KucoinService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/market/allTickers");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("data", out JsonElement data)
                            && data.TryGetProperty("ticker", out JsonElement ticker))
                        {
                            _cache.Set("kucoin_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in Kucoin response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited Kucoin prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/KucoinPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited Kucoin prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}