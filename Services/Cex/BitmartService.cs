using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class BitmartService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<BitmartService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("bitmart");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BitmartService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/spot/v1/ticker");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("data", out JsonElement data)
                            && data.TryGetProperty("tickers", out JsonElement tickers))
                        {
                            _cache.Set("bitmart_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in Bitmart response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited Bitmart prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/BitmartPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited Bitmart prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}