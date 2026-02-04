using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ARB
{
    public class XtService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<XtService> logger) : BackgroundService
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("xt");
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<XtService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "/v4/public/ticker");
                    HttpResponseMessage? response = await _client.SendAsync(request);
                    string? responseData = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(responseData))
                    {
                        JsonDocument doc = JsonDocument.Parse(responseData);

                        if (doc.RootElement.TryGetProperty("result", out JsonElement result))
                        {
                            _cache.Set("xt_spot", responseData, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            _logger.LogWarning("No result or list available in Xt response.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Commited Xt prices is null or empty.");
                    }

                    // COMMENT THIS
                    // File.WriteAllText(@$"./Data/XtPrices.json", await response.Content.ReadAsStringAsync());
                }
                catch
                {
                    _logger.LogWarning("Commited Xt prices is null or empty.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}