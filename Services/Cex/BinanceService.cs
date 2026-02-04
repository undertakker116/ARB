using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class BinanceService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<BinanceService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("binance");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BinanceService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/api/v3/ticker/price");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            _cache.Set("binance_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("Binance response is not an array.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Binance prices response is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/BinancePrices.json", await response.Content.ReadAsStringAsync());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error fetching binance prices: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
