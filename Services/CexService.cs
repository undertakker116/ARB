using ARB.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ARB
{
    public sealed class CexService(IMemoryCache cache, ILogger<CexService> logger) : BackgroundService
    {
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<CexService> _logger = logger;
        private readonly Stopwatch _stopwatch = new();

        private static string? jsonString = File.ReadAllText("./Input/FinalDict.json");
        private static JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
        private List<MarketData>? marketData = JsonSerializer.Deserialize<List<MarketData>>(jsonString ?? "");

        private string? GetCachedPrices(string ex)
        {
            if (_cache.TryGetValue(ex, out string? exPricesJson) && exPricesJson != null)
            {
                return exPricesJson;
            }
            else { return null; }
        }

        private void UpdatePrices()
        {
            if (marketData == null || marketData.Count == 0)
            {
                _logger.LogError("Main dictionary is null or empty");
                return;
            }

            string? binancePricesString = GetCachedPrices("binance_spot");
            string? bitgetPricesString = GetCachedPrices("bitget_spot");
            string? bitmartPricesString = GetCachedPrices("bitmart_spot");
            string? bybitPricesString = GetCachedPrices("bybit_spot");
            string? gatePricesString = GetCachedPrices("gate_spot");
            string? htxPricesString = GetCachedPrices("htx_spot");
            string? kucoinPricesString = GetCachedPrices("kucoin_spot");
            string? lbankPricesString = GetCachedPrices("lbank_spot");
            string? mexcPricesString = GetCachedPrices("mexc_spot");
            string? okxPricesString = GetCachedPrices("okx_spot");
            string? poloniexPricesString = GetCachedPrices("poloniex_spot");
            string? xtPricesString = GetCachedPrices("xt_spot");
            string? okxDexPricesString = GetCachedPrices("okx_dex");

            foreach (var coin in marketData)
            {
                if (coin == null || coin.Exchanges == null) { return; }

                foreach (var ex in coin.Exchanges)
                {
                    if (ex.Name == "binance")
                    {
                        if (string.IsNullOrWhiteSpace(binancePricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(binancePricesString);
                        JsonElement data = doc.RootElement;

                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ticker in data.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("price", out JsonElement lastPriceElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice))
                                    {
                                        currentLastPrice = parsedPrice;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Binance exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "bitget")
                    {
                        if (string.IsNullOrWhiteSpace(bitgetPricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(bitgetPricesString);
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("data", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("lastPr", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("baseVolume", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("quoteVolume", out JsonElement turnoverElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Bitget exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "bitmart")
                    {
                        if (string.IsNullOrWhiteSpace(bitmartPricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(bitmartPricesString);
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("data", out JsonElement dataTickers)
                            && dataTickers.TryGetProperty("tickers", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("last_price", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("base_volume_24h", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("quote_volume_24h", out JsonElement turnoverElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Bitmart exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + "_" + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "bybit" || ex.Name == "bybit_spot")
                    {
                        if (string.IsNullOrWhiteSpace(bybitPricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(bybitPricesString);
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("result", out JsonElement resultElement)
                            && resultElement.TryGetProperty("list", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("lastPrice", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("volume24h", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("turnover24h", out JsonElement turnoverElement)
                                    )
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Bybit exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "gate")
                    {
                        if (string.IsNullOrWhiteSpace(gatePricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(gatePricesString);
                        JsonElement data = doc.RootElement;

                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ticker in data.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("currency_pair", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("last", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("base_volume", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("quote_volume", out JsonElement turnoverElement)
                                    )
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Gate exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + "_" + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "htx" || ex.Name == "huobi")
                    {
                        if (string.IsNullOrWhiteSpace(htxPricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(htxPricesString);
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("data", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("close", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("vol", out JsonElement volumeElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;


                                    if (lastPriceElement.ValueKind == JsonValueKind.Number
                                        && volumeElement.ValueKind == JsonValueKind.Number)
                                    {
                                        currentLastPrice = lastPriceElement.GetDecimal();
                                        currentVolume = volumeElement.GetDecimal();
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Bybit exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "kucoin")
                    {
                        if (string.IsNullOrWhiteSpace(kucoinPricesString)) continue;

                        using JsonDocument doc = JsonDocument.Parse(kucoinPricesString);
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("data", out JsonElement dataElement)
                            && dataElement.TryGetProperty("ticker", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("last", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("vol", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("volValue", out JsonElement turnoverElement)
                                    )
                                {
                                    if (lastPriceElement.ValueKind == JsonValueKind.Null
                                        || volumeElement.ValueKind == JsonValueKind.Null
                                        || turnoverElement.ValueKind == JsonValueKind.Null)
                                    {
                                        continue;
                                    }

                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Kucoin exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + "-" + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "lbank")
                    {
                        using JsonDocument doc = JsonDocument.Parse(lbankPricesString ?? "");
                        JsonElement data = doc.RootElement;

                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ticker in data.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("price", out JsonElement lastPriceElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice))
                                    {
                                        currentLastPrice = parsedPrice;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Lbank exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + "_" + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "mexc" || ex.Name == "mxc")
                    {
                        using JsonDocument doc = JsonDocument.Parse(mexcPricesString ?? "");
                        JsonElement data = doc.RootElement;

                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ticker in data.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("lastPrice", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("volume", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("quoteVolume", out JsonElement turnoverElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Mexc exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "okx" || ex.Name == "okex")
                    {
                        using JsonDocument doc = JsonDocument.Parse(okxPricesString ?? "");
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("data", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("instId", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("last", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("vol24h", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("volCcy24h", out JsonElement turnoverElement)
                                    )
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    string? lastPriceStr = lastPriceElement.GetString();
                                    string? volumeStr = volumeElement.GetString();
                                    string? turnoverStr = turnoverElement.GetString();

                                    // Пропускаем если хотя бы одно значение пустое
                                    if (string.IsNullOrWhiteSpace(lastPriceStr) ||
                                        string.IsNullOrWhiteSpace(volumeStr) ||
                                        string.IsNullOrWhiteSpace(turnoverStr))
                                    {
                                        continue;
                                    }

                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceStr, out decimal parsedPrice)
                                        && decimal.TryParse(volumeStr, out decimal parsedVolume)
                                        && decimal.TryParse(turnoverStr, out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        continue; // Молча пропускаем вместо warning
                                    }

                                    if (currentSymbol.Equals(ex.Base + "-" + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "poloniex")
                    {
                        using JsonDocument doc = JsonDocument.Parse(poloniexPricesString ?? "");
                        JsonElement data = doc.RootElement;

                        if (data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ticker in data.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("price", out JsonElement lastPriceElement))
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice))
                                    {
                                        currentLastPrice = parsedPrice;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from Poloniex exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                    }
                                }
                            }
                        }
                    }

                    else if (ex.Name == "xt")
                    {
                        using JsonDocument doc = JsonDocument.Parse(xtPricesString ?? "");
                        JsonElement data = doc.RootElement;

                        if (data.TryGetProperty("result", out JsonElement dataList))
                        {
                            foreach (JsonElement ticker in dataList.EnumerateArray())
                            {
                                if (ticker.TryGetProperty("s", out JsonElement symbolElement)
                                    && ticker.TryGetProperty("c", out JsonElement lastPriceElement)
                                    && ticker.TryGetProperty("q", out JsonElement volumeElement)
                                    && ticker.TryGetProperty("v", out JsonElement turnoverElement)
                                    )
                                {
                                    string currentSymbol = symbolElement.GetString() ?? string.Empty;
                                    decimal currentLastPrice = 0;
                                    decimal currentVolume = 0;
                                    decimal currentTurnover = 0;

                                    if (decimal.TryParse(lastPriceElement.GetString(), out decimal parsedPrice)
                                        && decimal.TryParse(volumeElement.GetString(), out decimal parsedVolume)
                                        && decimal.TryParse(turnoverElement.GetString(), out decimal parsedTurnover))
                                    {
                                        currentLastPrice = parsedPrice;
                                        currentVolume = parsedVolume;
                                        currentTurnover = parsedTurnover;
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Cannot parse decimal of {currentSymbol} from XT exchange");
                                    }

                                    if (currentSymbol.Equals(ex.Base + ex.Target, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ex.Last = currentLastPrice;
                                        ex.Volume = currentVolume;
                                        ex.Turnover = currentTurnover;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // OKX DEX prices update
            try
            {
                if (!string.IsNullOrWhiteSpace(okxDexPricesString))
                {
                    using JsonDocument doc = JsonDocument.Parse(okxDexPricesString);
                    JsonElement data = doc.RootElement;

                    if (data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var coin in marketData)
                        {
                            if (string.IsNullOrWhiteSpace(coin.Chain) || string.IsNullOrWhiteSpace(coin.ContractAddress))
                                continue;

                            foreach (JsonElement dexItem in data.EnumerateArray())
                            {
                                if (dexItem.TryGetProperty("chainName", out JsonElement chainNameElement) &&
                                    dexItem.TryGetProperty("tokenContractAddress", out JsonElement contractElement))
                                {
                                    string? chainName = chainNameElement.GetString();
                                    string? contractAddress = contractElement.GetString();

                                    if (chainName != null && contractAddress != null &&
                                        chainName.Equals(coin.Chain, StringComparison.OrdinalIgnoreCase) &&
                                        contractAddress.Equals(coin.ContractAddress, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (dexItem.TryGetProperty("price", out JsonElement priceElement))
                                        {
                                            string? priceStr = priceElement.GetString();
                                            if (!string.IsNullOrWhiteSpace(priceStr) &&
                                                decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                                            {
                                                coin.DexPrice = price;
                                            }
                                        }

                                        if (dexItem.TryGetProperty("liquidity", out JsonElement liquidityElement))
                                        {
                                            string? liquidityStr = liquidityElement.GetString();
                                            if (!string.IsNullOrWhiteSpace(liquidityStr) &&
                                                decimal.TryParse(liquidityStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal liquidity))
                                            {
                                                coin.Liquidity = liquidity;
                                            }
                                        }

                                        if (dexItem.TryGetProperty("marketCap", out JsonElement marketCapElement))
                                        {
                                            string? marketCapStr = marketCapElement.GetString();
                                            if (!string.IsNullOrWhiteSpace(marketCapStr) &&
                                                decimal.TryParse(marketCapStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal marketCap))
                                            {
                                                coin.Capitalization = marketCap;
                                            }
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating OKX DEX prices");
            }
        }

        private void WriteMainDictionary()
        {
            if (marketData != null && marketData.Count > 0)
            {
                try
                {
                    _cache.Set("all_prices", marketData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rewriting dictionary in cache error.");
                }
            }
            else
            {
                _logger.LogWarning("Commited contract data for dictionary is null or empty.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _stopwatch.Restart();

                    UpdatePrices();
                    WriteMainDictionary();

                    _stopwatch.Stop();

                    // COMMENT THIS
                    // string stringedDict = JsonSerializer.Serialize(marketData, serializerOptions);
                    // File.WriteAllText(@$"./Temp/PriceDict.json", stringedDict);

                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating prices in dictionaries");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}
