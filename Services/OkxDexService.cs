using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using ARB.Models;

namespace ARB
{
    public sealed class OkxDexService(IMemoryCache cache, ILogger<OkxDexService> logger) : BackgroundService
    {
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<OkxDexService> _logger = logger;
        private readonly HttpClient _httpClient = new();
        private readonly Stopwatch _stopwatch = new();

        // API Configuration
        private static readonly string ApiKey = Environment.GetEnvironmentVariable("OKX_API_KEY") ?? throw new InvalidOperationException("OKX_API_KEY not found in environment variables");
        private static readonly string SecretKey = Environment.GetEnvironmentVariable("OKX_SECRET_KEY") ?? throw new InvalidOperationException("OKX_SECRET_KEY not found in environment variables");
        private static readonly string Passphrase = Environment.GetEnvironmentVariable("OKX_PASSPHRASE") ?? throw new InvalidOperationException("OKX_PASSPHRASE not found in environment variables");
        private const string Endpoint = "/api/v6/dex/market/price-info";
        private const string BaseUrl = "https://www.okx.com";

        // File Paths
        private const string DictJsonPath = "./Input/FinalDict.json";
        private const string OutputJsonPath = "./Temp/okx_dex_prices.json";
        private const string FullDataJsonPath = "./Temp/okx_dex_full_data.json";

        // Settings
        private const int BatchSize = 100;
        private int _delayBetweenRequests = 50;
        private int _rateLimitHits = 0;

        // Data - статичный словарь загружается один раз при старте
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private List<MarketData>? marketData;

        // Logging
        private readonly List<string> _currentSessionLogs = new();
        private DateTime _currentLogFileTime = DateTime.UtcNow;
        private string _currentLogFilePath = "";
        private readonly object _logLock = new();

        // OKX Chain Index -> Chain Name mapping
        private static readonly Dictionary<string, string> ChainIndexMap = new()
        {
            { "1", "ethereum" },
            { "501", "solana" },
            { "42161", "arbitrum-one" },
            { "534352", "scroll" },
            { "324", "zksync" },
            { "56", "binance-smart-chain" },
            { "8453", "base" },
            { "7777777", "zora-network" },
            { "784", "sui" },
            { "10", "optimistic-ethereum" },
            { "43114", "avalanche" },
            { "195", "tron" },
            { "607", "the-open-network" },
            { "59144", "linea" },
            { "137", "polygon-pos" }
        };

        #region Main Execution

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            InitializeLogFile();
            LogServiceStart();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CheckLogFileRotation();

                    LogToFile("");
                    LogToFile("═══════════════════════════════════════════════════════");
                    LogToFile("🚀 OKX DEX UPDATE START");
                    LogToFile("═══════════════════════════════════════════════════════");

                    _stopwatch.Restart();

                    await UpdateDexPrices();

                    _stopwatch.Stop();

                    SaveResultToFile();
                    SaveCurrentLogFile();

                    LogToFile("═══════════════════════════════════════════════════════");
                    LogToFile($"✅ UPDATE COMPLETED in {_stopwatch.ElapsedMilliseconds}ms ({_stopwatch.ElapsedMilliseconds / 1000.0:F2}s)");
                    LogToFile("═══════════════════════════════════════════════════════");
                }
                catch (Exception ex)
                {
                    HandleExecutionError(ex, stoppingToken);
                }
            }
        }

        #endregion

        #region Update Logic

        private async Task UpdateDexPrices()
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch prepareStopwatch = Stopwatch.StartNew();

            try
            {
                LogToFile("");
                LogToFile("📊 STARTING UPDATE CYCLE");

                // Загружаем данные из файла
                if (!LoadMarketDataFromFile())
                {
                    LogToFile("❌ ERROR: Failed to load market data");
                    return;
                }

                // Подготовка данных
                List<(string chainIndex, string contractAddress, string chainName)>? uniqueTokenRequests = PrepareTokenRequests();
                if (uniqueTokenRequests == null || uniqueTokenRequests.Count == 0)
                {
                    LogToFile("❌ ERROR: No tokens to process");
                    return;
                }

                prepareStopwatch.Stop();
                LogToFile($"⏱️  Preparation: {prepareStopwatch.ElapsedMilliseconds}ms");

                // Получение цен от OKX DEX
                (Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> dexData, Dictionary<string, string> chainNameByContract) = await FetchDexPrices(uniqueTokenRequests);

                // Обновление данных
                int updatedCount = UpdateMarketDataWithPrices(dexData);

                // Сохранение в кэш
                SaveToCache(dexData);

                totalStopwatch.Stop();
                LogUpdateStatistics(totalStopwatch, prepareStopwatch, uniqueTokenRequests.Count, updatedCount, dexData.Count);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[UpdateDexPrices] ❌ JSON parsing error");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[UpdateDexPrices] ❌ Network error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpdateDexPrices] ❌ Unexpected error: {Type}", ex.GetType().Name);
            }
        }

        private bool LoadMarketDataFromFile()
        {
            try
            {
                string jsonString = File.ReadAllText(DictJsonPath);
                marketData = JsonSerializer.Deserialize<List<MarketData>>(jsonString);

                if (marketData == null || marketData.Count == 0)
                {
                    _logger.LogError("Market data is null or empty after deserialization");
                    return false;
                }

                LogToFile($"📋 Loaded {marketData.Count} records from dictionary");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load market data from {Path}", DictJsonPath);
                return false;
            }
        }

        #endregion

        #region Data Preparation

        private List<(string chainIndex, string contractAddress, string chainName)>? PrepareTokenRequests()
        {
            if (marketData == null || marketData.Count == 0)
            {
                LogToFile("❌ ERROR: Market data is null or empty");
                return null;
            }

            LogToFile($"📋 Total records in dictionary: {marketData.Count}");

            // Получаем уникальные пары Chain + ContractAddress
            List<MarketData> uniqueTokens = marketData
                .Where(t => !string.IsNullOrWhiteSpace(t.Chain) && !string.IsNullOrWhiteSpace(t.ContractAddress))
                .GroupBy(t => new { Chain = t.Chain!.ToLowerInvariant(), Contract = t.ContractAddress!.ToLowerInvariant() })
                .Select(g => g.First())
                .ToList();

            LogToFile($"🔑 Unique Chain:Contract pairs: {uniqueTokens.Count}");

            // Формируем запросы с chainIndex
            List<(string chainIndex, string contractAddress, string chainName)> tokenRequests = [];
            Dictionary<string, int> chainCounts = [];

            foreach (MarketData token in uniqueTokens)
            {
                string chainLower = token.Chain!.ToLowerInvariant();
                string? chainIndex = ChainIndexMap.FirstOrDefault(x =>
                    x.Value.Equals(chainLower, StringComparison.OrdinalIgnoreCase)).Key;

                if (string.IsNullOrEmpty(chainIndex) || string.IsNullOrEmpty(token.ContractAddress))
                    continue;

                tokenRequests.Add((chainIndex, token.ContractAddress, chainLower));

                if (!chainCounts.ContainsKey(chainLower))
                    chainCounts[chainLower] = 0;
                chainCounts[chainLower]++;
            }

            LogToFile($"✅ Prepared {tokenRequests.Count} requests for OKX DEX");
            LogToFile("📊 Tokens by chain:");
            foreach (KeyValuePair<string, int> chain in chainCounts.OrderByDescending(x => x.Value))
            {
                LogToFile($"   • {chain.Key}: {chain.Value} tokens");
            }

            return tokenRequests;
        }

        #endregion

        #region API Communication

        private async Task<(Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> data, Dictionary<string, string> chainMap)> FetchDexPrices(
            List<(string chainIndex, string contractAddress, string chainName)> tokenRequests)
        {
            Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> dexData = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> chainNameByContract = new(StringComparer.OrdinalIgnoreCase);

            int totalBatches = (tokenRequests.Count + BatchSize - 1) / BatchSize;
            LogToFile("");
            LogToFile($"📦 Batch configuration: {BatchSize} tokens per batch, {totalBatches} total batches");
            LogToFile($"⏱️  Estimated time: ~{totalBatches * _delayBetweenRequests / 1000.0:F1}s");

            Stopwatch apiCallsStopwatch = Stopwatch.StartNew();
            int successfulBatches = 0;
            int failedBatches = 0;

            for (int i = 0; i < tokenRequests.Count; i += BatchSize)
            {
                var batch = tokenRequests.Skip(i).Take(BatchSize).ToList();
                int batchNum = (i / BatchSize) + 1;

                // Сохраняем маппинг для обратного поиска
                foreach (var req in batch)
                {
                    string key = $"{req.chainIndex}:{req.contractAddress}".ToLowerInvariant();
                    chainNameByContract[key] = req.chainName;
                }

                try
                {
                    string? response = await SendDexRequest(batch, batchNum);

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        int pricesReceived = ProcessDexResponse(response, batchNum, chainNameByContract, dexData);
                        if (pricesReceived > 0)
                        {
                            successfulBatches++;
                        }
                        else
                        {
                            failedBatches++;
                            LogToFile($"⚠️  Batch {batchNum}/{totalBatches}: No prices received");
                        }
                    }
                    else
                    {
                        failedBatches++;
                        LogToFile($"❌ Batch {batchNum}/{totalBatches}: Empty response");
                    }
                }
                catch (Exception ex)
                {
                    failedBatches++;
                    LogToFile($"❌ Batch {batchNum}/{totalBatches}: Error - {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(_delayBetweenRequests));
            }

            apiCallsStopwatch.Stop();
            LogToFile("");
            LogToFile($"⏱️  API calls completed: {apiCallsStopwatch.ElapsedMilliseconds}ms");
            LogToFile($"✅ Successful batches: {successfulBatches}/{totalBatches}");
            if (failedBatches > 0)
            {
                LogToFile($"❌ Failed batches: {failedBatches}/{totalBatches}");
            }
            LogToFile($"📊 Total prices collected: {dexData.Count}");

            return (dexData, chainNameByContract);
        }

        private async Task<string?> SendDexRequest(List<(string chainIndex, string contractAddress, string chainName)> batch, int batchNumber)
        {
            try
            {
                List<Dictionary<string, object>> parameters = batch.Select(req => new Dictionary<string, object>
                {
                    { "chainIndex", req.chainIndex },
                    { "tokenContractAddress", req.contractAddress }
                }).ToList();

                // Сохраняем первые 5 запросов
                SaveRequestToFile(parameters, batchNumber);

                string timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0)
                    .ToString("F3", CultureInfo.InvariantCulture);
                string jsonPayload = JsonSerializer.Serialize(parameters);
                string message = timestamp + "POST" + Endpoint + jsonPayload;
                string signature = CreateSignature(message, SecretKey);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + Endpoint)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("OK-ACCESS-KEY", ApiKey);
                request.Headers.Add("OK-ACCESS-SIGN", signature);
                request.Headers.Add("OK-ACCESS-TIMESTAMP", timestamp);
                request.Headers.Add("OK-ACCESS-PASSPHRASE", Passphrase);
                request.Headers.Add("Accept", "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[SendDexRequest] HTTP Error: {StatusCode}", response.StatusCode);
                    return null;
                }

                string responseData = await response.Content.ReadAsStringAsync();
                CheckOkxErrorCodes(responseData);

                return responseData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendDexRequest] Exception occurred");
                return null;
            }
        }

        private int ProcessDexResponse(
            string response,
            int batchNum,
            Dictionary<string, string> chainNameByContract,
            Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> dexData)
        {
            // Сохраняем первые 5 ответов
            SaveResponseToFile(response, batchNum);

            using JsonDocument doc = JsonDocument.Parse(response);

            // Проверка на ошибки размера
            if (doc.RootElement.TryGetProperty("msg", out JsonElement msgElement))
            {
                string? msg = msgElement.GetString();
                if (msg != null && msg.Contains("not stored due to its length"))
                {
                    LogToFile($"❌ Batch {batchNum}: Response too large! Reduce batchSize!");
                    return 0;
                }
            }

            if (!doc.RootElement.TryGetProperty("data", out JsonElement rootData) ||
                rootData.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            int pricesInBatch = 0;
            foreach (JsonElement item in rootData.EnumerateArray())
            {
                try
                {
                    if (!item.TryGetProperty("chainIndex", out JsonElement chainIndexElement) ||
                        !item.TryGetProperty("tokenContractAddress", out JsonElement contractElement))
                    {
                        continue;
                    }

                    string? chainIndex = chainIndexElement.GetString();
                    string? contractAddress = contractElement.GetString();

                    if (string.IsNullOrWhiteSpace(chainIndex) || string.IsNullOrWhiteSpace(contractAddress))
                        continue;

                    string lookupKey = $"{chainIndex}:{contractAddress}".ToLowerInvariant();
                    if (!chainNameByContract.TryGetValue(lookupKey, out string? chainName))
                        continue;

                    decimal price = 0;
                    decimal liquidity = 0;
                    decimal marketCap = 0;

                    // Парсим price
                    if (item.TryGetProperty("price", out JsonElement priceElement))
                    {
                        string? priceStr = priceElement.GetString();
                        if (!string.IsNullOrWhiteSpace(priceStr))
                        {
                            decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out price);
                        }
                    }

                    // Парсим liquidity
                    if (item.TryGetProperty("liquidity", out JsonElement liquidityElement))
                    {
                        string? liquidityStr = liquidityElement.GetString();
                        if (!string.IsNullOrWhiteSpace(liquidityStr))
                        {
                            decimal.TryParse(liquidityStr, NumberStyles.Any, CultureInfo.InvariantCulture, out liquidity);
                        }
                    }

                    // Парсим marketCap
                    if (item.TryGetProperty("marketCap", out JsonElement marketCapElement))
                    {
                        string? marketCapStr = marketCapElement.GetString();
                        if (!string.IsNullOrWhiteSpace(marketCapStr))
                        {
                            decimal.TryParse(marketCapStr, NumberStyles.Any, CultureInfo.InvariantCulture, out marketCap);
                        }
                    }

                    if (price > 0)
                    {
                        string key = $"{chainName}:{contractAddress}".ToLowerInvariant();
                        dexData[key] = (price, liquidity, marketCap);
                        pricesInBatch++;
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"⚠️  Error deserializing token: {ex.Message}");
                }
            }

            return pricesInBatch;
        }

        #endregion

        #region Update & Save

        private int UpdateMarketDataWithPrices(Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> dexData)
        {
            if (marketData == null) return 0;

            Stopwatch updateStopwatch = Stopwatch.StartNew();
            int updatedCount = 0;
            int notUpdatedCount = 0;
            List<string> notUpdatedTokens = [];

            foreach (MarketData token in marketData)
            {
                if (string.IsNullOrWhiteSpace(token.Chain) || string.IsNullOrWhiteSpace(token.ContractAddress))
                    continue;

                string key = $"{token.Chain}:{token.ContractAddress}".ToLowerInvariant();
                if (dexData.TryGetValue(key, out var data))
                {
                    token.DexPrice = data.price;
                    token.Liquidity = data.liquidity;
                    token.Capitalization = data.marketCap;
                    updatedCount++;
                }
                else
                {
                    notUpdatedCount++;

                    // Получаем chainIndex для формата OKX
                    string chainLower = token.Chain.ToLowerInvariant();
                    string? chainIndex = ChainIndexMap.FirstOrDefault(x =>
                        x.Value.Equals(chainLower, StringComparison.OrdinalIgnoreCase)).Key;

                    if (!string.IsNullOrEmpty(chainIndex))
                    {
                        notUpdatedTokens.Add($"{{\"chainIndex\":\"{chainIndex}\",\"tokenContractAddress\":\"{token.ContractAddress}\"}}");
                    }
                }
            }

            updateStopwatch.Stop();

            LogToFile("");
            LogToFile($"💾 Market data update: {updateStopwatch.ElapsedMilliseconds}ms");
            LogToFile($"✅ Updated tokens: {updatedCount}");

            if (notUpdatedCount > 0)
            {
                LogToFile("");
                LogToFile($"⚠️  NOT UPDATED TOKENS: {notUpdatedCount}");
                LogToFile("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                LogToFile("Format: OKX API request format (chainIndex + tokenContractAddress)");
                LogToFile("");

                foreach (string tokenInfo in notUpdatedTokens)
                {
                    LogToFile($"   {tokenInfo}");
                }

                LogToFile("");
                LogToFile("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                LogToFile($"Total not updated: {notUpdatedCount} tokens");
                LogToFile("");
            }

            return updatedCount;
        }

        private void SaveToCache(Dictionary<string, (decimal price, decimal liquidity, decimal marketCap)> dexData)
        {
            Stopwatch saveStopwatch = Stopwatch.StartNew();

            // Создаем массив для сохранения в кэш (как другие сервисы)
            var cacheData = dexData.Select(kvp => new
            {
                chainName = kvp.Key.Split(':')[0],
                tokenContractAddress = kvp.Key.Split(':')[1],
                price = kvp.Value.price.ToString(CultureInfo.InvariantCulture),
                liquidity = kvp.Value.liquidity.ToString(CultureInfo.InvariantCulture),
                marketCap = kvp.Value.marketCap.ToString(CultureInfo.InvariantCulture)
            }).ToList();

            string fullDataJson = JsonSerializer.Serialize(cacheData, SerializerOptions);
            _cache.Set("okx_dex", fullDataJson);
            File.WriteAllText(FullDataJsonPath, fullDataJson);

            saveStopwatch.Stop();
            _logger.LogInformation("[SaveToCache] Saved {Count} records in {Ms}ms",
                cacheData.Count, saveStopwatch.ElapsedMilliseconds);
        }

        private void SaveResultToFile()
        {
            try
            {
                string stringedDict = JsonSerializer.Serialize(marketData, SerializerOptions);
                File.WriteAllText(OutputJsonPath, stringedDict);
                _logger.LogInformation("[SaveResultToFile] Saved to {Path}", OutputJsonPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SaveResultToFile] Failed to save result");
            }
        }

        #endregion

        #region Helper Methods

        private static string CreateSignature(string message, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToBase64String(hash);
        }

        private void CheckOkxErrorCodes(string responseData)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseData);
                string code = doc.RootElement.TryGetProperty("code", out JsonElement codeEl) ? codeEl.GetString() ?? "0" : "0";
                string msg = doc.RootElement.TryGetProperty("msg", out JsonElement msgEl) ? msgEl.GetString() ?? "" : "";

                switch (code)
                {
                    case "0":
                        break;
                    case "50011":
                        HandleRateLimit(msg);
                        break;
                    case "50111":
                    case "50113":
                        _logger.LogError("[CheckOkxErrorCodes] ❌ Authentication error! Code: {Code}", code);
                        break;
                    default:
                        if (code != "0")
                            _logger.LogWarning("[CheckOkxErrorCodes] ⚠️ Error code: {Code}, Message: {Msg}", code, msg);
                        break;
                }
            }
            catch { }
        }

        private async void HandleRateLimit(string msg)
        {
            _rateLimitHits++;
            _delayBetweenRequests = Math.Min(_delayBetweenRequests + 50, 500);
            _logger.LogError("[HandleRateLimit] ❌ Rate limit! Increasing delay to {Delay}ms (hit #{Count})",
                _delayBetweenRequests, _rateLimitHits);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        #endregion

        #region File Operations

        private void SaveRequestToFile(List<Dictionary<string, object>> parameters, int batchNumber)
        {
            if (batchNumber > 5) return;

            try
            {
                Directory.CreateDirectory("./Temp");
                string fileName = $"./Temp/okx_request_batch_{batchNumber}.json";
                string json = JsonSerializer.Serialize(parameters, SerializerOptions);
                File.WriteAllText(fileName, json);
            }
            catch { }
        }

        private int _savedResponsesCount = 0;

        private void SaveResponseToFile(string response, int batchNum)
        {
            if (_savedResponsesCount >= 5) return;

            try
            {
                Directory.CreateDirectory("./Temp");
                string fileName = $"./Temp/okx_response_batch_{batchNum}.json";
                using JsonDocument doc = JsonDocument.Parse(response);
                string formattedJson = JsonSerializer.Serialize(doc, SerializerOptions);
                File.WriteAllText(fileName, formattedJson);
                _savedResponsesCount++;
                _logger.LogInformation("[SaveResponseToFile] 💾 Saved response to {File}", fileName);
            }
            catch { }
        }

        #endregion

        #region Logging

        private void LogServiceStart()
        {
            LogToFile("🚀 OKX DEX SERVICE STARTED");
            LogToFile($"📂 Dictionary path: {DictJsonPath}");
            LogToFile($"📦 Batch size: {BatchSize} tokens");
            LogToFile($"⏱️  Initial delay: {_delayBetweenRequests}ms between requests");
            LogToFile($"🔄 Update mode: Continuous (no pauses)");
            LogToFile("");
        }

        private void LogUpdateStatistics(Stopwatch totalStopwatch, Stopwatch prepareStopwatch, int totalRequests, int updatedCount, int pricesCollected)
        {
            LogToFile("");
            LogToFile("📈 UPDATE STATISTICS:");
            LogToFile($"   • Total time: {totalStopwatch.ElapsedMilliseconds}ms ({totalStopwatch.ElapsedMilliseconds / 1000.0:F2}s)");
            LogToFile($"   • Preparation: {prepareStopwatch.ElapsedMilliseconds}ms");
            LogToFile($"   • Total requests sent: {totalRequests}");
            LogToFile($"   • Prices collected from OKX: {pricesCollected}");
            LogToFile($"   • Tokens updated in dictionary: {updatedCount}");
            LogToFile($"   • Success rate: {(totalRequests > 0 ? (pricesCollected * 100.0 / totalRequests) : 0):F1}%");
            LogToFile($"   • Current delay between requests: {_delayBetweenRequests}ms");
            if (_rateLimitHits > 0)
            {
                LogToFile($"   ⚠️  Rate limit hits: {_rateLimitHits}");
            }
        }

        private void HandleExecutionError(Exception ex, CancellationToken stoppingToken)
        {
            LogToFile("");
            LogToFile("❌ ERROR IN MAIN LOOP:");
            LogToFile($"   Type: {ex.GetType().Name}");
            LogToFile($"   Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogToFile($"   Inner: {ex.InnerException.Message}");
            }
            LogToFile($"   Stack trace: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
            LogToFile("⏳ Waiting 5 seconds before retry...");
            LogToFile("");

            SaveCurrentLogFile();
            Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).Wait(stoppingToken);
        }

        #endregion

        #region Logging

        private void InitializeLogFile()
        {
            _currentLogFileTime = DateTime.UtcNow;
            string timestamp = _currentLogFileTime.ToString("yyyy-MM-dd_HH-00-00");
            _currentLogFilePath = $"./Logs/OkxDex_{timestamp}.txt";

            Directory.CreateDirectory("./Logs");

            lock (_logLock)
            {
                _currentSessionLogs.Clear();
                LogToFile("═══════════════════════════════════════════════════════");
                LogToFile($"OKX DEX SERVICE LOG - {_currentLogFileTime:yyyy-MM-dd HH:mm:ss} UTC");
                LogToFile("═══════════════════════════════════════════════════════");
                LogToFile("");
            }
        }

        private void CheckLogFileRotation()
        {
            DateTime now = DateTime.UtcNow;
            if (now.Hour != _currentLogFileTime.Hour)
            {
                SaveCurrentLogFile();
                InitializeLogFile();
            }
        }

        private void LogToFile(string message)
        {
            lock (_logLock)
            {
                string timestampedMessage = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
                _currentSessionLogs.Add(timestampedMessage);

                // Также пишем в консоль для отладки
                _logger.LogInformation(message);
            }
        }

        private void SaveCurrentLogFile()
        {
            lock (_logLock)
            {
                try
                {
                    File.WriteAllLines(_currentLogFilePath, _currentSessionLogs);

                    // Сохраняем в кеш для API
                    _cache.Set("okx_dex_current_log", string.Join("\n", _currentSessionLogs));
                    _cache.Set("okx_dex_log_files", GetLogFilesList());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save log file");
                }
            }
        }

        private List<string> GetLogFilesList()
        {
            try
            {
                if (!Directory.Exists("./Logs")) return new List<string>();

                return Directory.GetFiles("./Logs", "OkxDex_*.txt")
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Select(f => f!)
                    .OrderByDescending(f => f)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        #endregion
    }
}