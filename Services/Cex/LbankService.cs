using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class LbankService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<LbankService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("lbank");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<LbankService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/v2/supplement/ticker/price.do"); 
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("data", out JsonElement data)
                            && data.ValueKind == JsonValueKind.Array)
                        {
                            _cache.Set("lbank_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in lbank response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited lbank prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/LbankPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited lbank prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}