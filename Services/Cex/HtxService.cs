using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class HtxService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<HtxService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("htx");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<HtxService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/market/tickers");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("data", out JsonElement result))
                        {
                            _cache.Set("htx_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in HTX response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited HTX prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/HtxPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited HTX prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}