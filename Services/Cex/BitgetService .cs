using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class BitgetService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<BitgetService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("bitget");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BitgetService> _logger = logger;

        private async Task GetBitgetSpotPrices()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, "/api/v2/spot/market/tickers");
                HttpResponseMessage? response = await _client.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);

                    if (doc.RootElement.TryGetProperty("data", out JsonElement result))
                    {
                        _cache.Set("bitget_spot", responseData, TimeSpan.FromSeconds(30));
                    }
                    else
                    {
                        _logger.LogWarning("No result or list available in Bitget spot response.");
                    }
                }
                else
                {
                    _logger.LogWarning("Commited Bitget spot prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/BitgetPrices.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning("Commited Bitget spot prices is null or empty.");
            }
        }

        private async Task GetBitgetFuturesPrices()
        {
            HttpRequestMessage request = new(HttpMethod.Get, $"/api/v2/mix/market/tickers?productType=USDT-FUTURES");
            HttpResponseMessage? response = await _client.SendAsync(request);
            string? responseData = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrWhiteSpace(responseData))
            {
                JsonDocument doc = JsonDocument.Parse(responseData);

                if (doc.RootElement.TryGetProperty("data", out JsonElement result))
                {
                    _cache.Set("bitget_futures", responseData, TimeSpan.FromSeconds(30));
                }
                else
                {
                    _logger.LogWarning("No result or list available in Bitget futures response.");
                }
            }
            else
            {
                _logger.LogWarning("Commited Bitget futures prices is null or empty.");
            }

            // COMMENT THIS
            // File.WriteAllText(@$"./Data/BitgetFutures.json", await response.Content.ReadAsStringAsync());

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetBitgetSpotPrices();
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

                await GetBitgetFuturesPrices();
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}