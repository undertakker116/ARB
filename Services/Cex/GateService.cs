using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class GateService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<GateService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("gate");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<GateService> _logger = logger;

        private async Task GetGateSpotPrices()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, "/api/v4/spot/tickers");
                HttpResponseMessage? response = await _client.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);

                    if (doc.RootElement.EnumerateArray().Any())
                    {
                        _cache.Set("gate_spot", responseData, TimeSpan.FromSeconds(30));
                    }
                    else
                    {
                        _logger.LogWarning("No result or list available in Gate response.");
                    }
                }
                else
                {
                    _logger.LogWarning("Commited Gate spot prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/GatePrices.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning("Commited Gate spot prices is null or empty.");
            }
        }

        private async Task GetGateFuturesPrices()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, $"/api/v4/futures/usdt/contracts");
                HttpResponseMessage? response = await _client.SendAsync(request);
                string? responseData = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseData))
                {
                    JsonDocument doc = JsonDocument.Parse(responseData);

                    if (doc.RootElement.EnumerateArray().Any())
                    {
                        _cache.Set("gate_futures", responseData, TimeSpan.FromSeconds(30));
                    }
                    else
                    {
                        _logger.LogWarning("No result or list available in Gate futures response.");
                    }
                }
                else
                {
                    _logger.LogWarning("Commited Gate futures prices is null or empty.");
                }

                // COMMENT THIS
                // File.WriteAllText(@$"./Data/GateFutures.json", await response.Content.ReadAsStringAsync());
            }
            catch
            {
                _logger.LogWarning("Commited Gate spot prices is null or empty.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetGateSpotPrices();
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

                //await GetGateFuturesPrices();
                //await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}