using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class MexcService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<MexcService> logger) : BackgroundService
    {
        private readonly HttpClient _spotClient = httpClientFactory.CreateClient("mexc");
        private readonly HttpClient _futuresClient = httpClientFactory.CreateClient("mexc_futures");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<MexcService> _logger = logger;

        private async Task GetMexcSpotPrices()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, "/api/v3/ticker/24hr");
                HttpResponseMessage? response = await _spotClient.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);

                    if (doc.RootElement.EnumerateArray().Any())
                    {
                        _cache.Set("mexc_spot", responseData, TimeSpan.FromSeconds(30));
                    }
                    else
                    {
                        _logger.LogWarning("No result or list available in Mexc spot response.");
                    }
                }
                else
                {
                    _logger.LogWarning("Commited Mexc spot prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/MexcPrices.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning("Commited Mexc spot prices is null or empty.");
            }
        }

        private async Task GetMexcFuturesPrices()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/contract/ticker");
                HttpResponseMessage? response = await _futuresClient.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("data", out JsonElement dataArray) &&
                        dataArray.ValueKind == JsonValueKind.Array)
                    {
                        _cache.Set("mexc_futures", responseData, TimeSpan.FromSeconds(30));
                    }
                    else
                    {
                        _logger.LogWarning("No result or list available in Mexc futures response.");
                    }
                }
                else
                {
                    _logger.LogWarning("Commited Mexc futures prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/MexcFutures.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning("Commited Mexc futures prices is null or empty.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetMexcSpotPrices();
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

                //await GetMexcFuturesPrices();
                //await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}