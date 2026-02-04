using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class BybitService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<BybitService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("bybit");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BybitService> _logger = logger;

        private async Task GetBybitPrices(string category)
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, $"/v5/market/tickers?category={category}");
                HttpResponseMessage? response = await _client.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);

                    if (doc.RootElement.TryGetProperty("result", out JsonElement result)
                        && result.TryGetProperty("list", out JsonElement list))
                    {
                        if (category == "spot")
                        {
                            _cache.Set("bybit_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else if (category == "linear")
                        {
                            _cache.Set("bybit_futures", responseData, TimeSpan.FromSeconds(30));
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No result or list available in Bybit {category} response.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Commited Bybit {category} prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/BybitPrices.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning($"Commited Bybit {category} prices is null or empty.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetBybitPrices("spot");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

                //await GetBybitPrices("linear");
                //await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}