using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARB.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARB1
{
    public sealed class DictService : BackgroundService
    {
        #region CONFIG

        private static readonly string CG_API_KEY = Environment.GetEnvironmentVariable("COINGECKO_API_KEY") ?? throw new InvalidOperationException("COINGECKO_API_KEY not found in environment variables");
        private const int REQ_PER_MIN = 30;
        private static readonly int DelayMs = 60_000 / REQ_PER_MIN;

        private static readonly string[] ExchangeIds =
        {
            "bybit_spot",
            "binance",
            "mxc",
            "okex",
            "bitget",
            "huobi",
            "bitmart",
            "gate",
            "kucoin",
            "xt",
            "lbank",
            "poloniex"
        };

        private static readonly HashSet<string> TargetChains = new(StringComparer.OrdinalIgnoreCase)
        {
            "ethereum", "solana", "arbitrum-one", "scroll", "zksync", "binance-smart-chain",
            "base", "zora-network", "sui", "optimistic-ethereum", "avalanche", "tron",
            "the-open-network", "aptos", "near-protocol", "kava", "celo", "linea", "polygon-pos", "osmosis"
        };

        private static readonly HashSet<string> AllowedTargets = new(StringComparer.OrdinalIgnoreCase)
            { "USDT", "USDC", "ETH", "SOL" };

        #endregion

        private readonly IMemoryCache _cache;
        private readonly ILogger<DictService> _logger;
        private readonly HttpClient _http = new();
        private DateTime _lastReq = DateTime.MinValue;

        private readonly Dictionary<string, Dictionary<string, AssetRec>> _assets =
            new(StringComparer.OrdinalIgnoreCase);

        public DictService(IMemoryCache cache, ILogger<DictService> logger)
        {
            _cache = cache;
            _logger = logger;
            _http.DefaultRequestHeaders.Add("x-cg-demo-api-key", CG_API_KEY);
            _http.Timeout = TimeSpan.FromSeconds(180);
        }

        #region PARSE HELPERS

        private static decimal ParseDecimal(string element)
        {
            try
            {
                return decimal.TryParse(element, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result)
                    ? result
                    : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string NormalizeChain(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var s = raw.Trim().ToUpperInvariant();

            if (s is "ETH" or "ERC20" or "ETHEREUM" or "ETH-ERC20") return "ethereum";
            if (s is "SOL" or "SOL-SOL" or "SOLANA" or "SPL") return "solana";
            if (s is "ARBITRUM" or "ARBITRUMONE" or "ARBI" or "ARBEVM" or "ANIME-ARBITRUM ONE" or "ARBITRUM-ONE")
                return "arbitrum-one";
            if (s is "SCROLL" or "SCROLLETH" or "SCROLL-ETH") return "scroll";
            if (s is "ZKSYNC" or "ZKSYNCERA" or "ZKSERA" or "ZKSYNK") return "zksync";
            if (s is "BSC" or "BEP20" or "BNB SMART CHAIN" or "BSC_BNB" or "BNB" or "BNBCHAIN")
                return "binance-smart-chain";
            if (s is "BASE" or "BASEEVM" or "BASE-ETH" or "Base") return "base";
            if (s is "ZORA" or "ZORA-NETWORK") return "zora-network";
            if (s is "SUI") return "sui";
            if (s is "OPTIMISM" or "OP" or "OPETH" or "OPT" or "OPTIMISTIC-ETHEREUM") return "optimistic-ethereum";
            if (s is "AVAX" or "AVAX_C" or "C-CHAIN" or "CAVAX" or "AVALANCHE") return "avalanche";
            if (s is "TRON" or "TRX" or "TRC20" or "TRC") return "tron";
            if (s is "TON" or "TONCOIN") return "the-open-network";
            if (s is "APTOS" or "APT") return "aptos";
            if (s is "NEAR" or "NEAR PROTOCOL") return "near-protocol";
            if (s is "KAVA") return "kava";
            if (s is "CELO") return "celo";
            if (s is "LINEA" or "LINEAETH" or "LINEA-ETH") return "linea";
            if (s is "POLYGON" or "MATIC" or "POLYGON POS" or "POLYGON-POS") return "polygon-pos";
            if (s is "OSMOSIS") return "osmosis";

            return "";
        }

        #endregion

        #region COINGECKO API

        private async Task DelayIfNeeded(CancellationToken ct)
        {
            try
            {
                var now = DateTime.UtcNow;
                var el = (now - _lastReq).TotalMilliseconds;
                if (el < DelayMs) await Task.Delay(DelayMs - (int)el, ct);
                _lastReq = DateTime.UtcNow;
            }
            catch
            {
            }
        }

        private async Task<List<FullTicker>> LoadAllTickersAsync(CancellationToken ct)
        {
            var all = new List<FullTicker>();
            try
            {
                _logger.LogInformation("Loading tickers from {Count} exchanges: {Exchanges}",
                    ExchangeIds.Length, string.Join(", ", ExchangeIds));

                foreach (var ex in ExchangeIds)
                {
                    try
                    {
                        _logger.LogInformation("→ Loading {Ex}...", ex);
                        int page = 1;
                        while (true)
                        {
                            try
                            {
                                await DelayIfNeeded(ct);
                                var url = $"https://api.coingecko.com/api/v3/exchanges/{ex}/tickers?page={page}";
                                using var resp = await _http.GetAsync(url, ct);
                                if (!resp.IsSuccessStatusCode) break;

                                var json = await resp.Content.ReadAsStringAsync(ct);
                                var dto = JsonSerializer.Deserialize<ExchangeResponse>(json,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                var arr = dto?.Tickers;
                                if (arr == null || arr.Count == 0) break;

                                all.AddRange(arr.Select(t => new FullTicker
                                {
                                    ExchangeName = ex,
                                    Base = t.Base,
                                    Target = t.Target,
                                    Last = t.Last,
                                    Volume = t.Volume,
                                    TradeUrl = t.TradeUrl?.Trim(),
                                    CoinId = t.CoinId
                                }));

                                if (page == 1)
                                    _logger.LogInformation("  {Ex} page {Page}: {Count} tickers", ex, page, arr.Count);

                                page++;
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogWarning("Page error {Ex} page {Page}: {Msg}", ex, page, ex2.Message);
                                break;
                            }
                        }

                        var exCount = all.Count(t => t.ExchangeName == ex);
                        _logger.LogInformation("✓ {Ex}: {Count} tickers loaded", ex, exCount);
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError(ex2, "Exchange error {Ex}: {Msg}", ex, ex2.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadAllTickers failed: {Msg}", ex.Message);
            }

            return all;
        }

        private async Task<List<CoinInfo>> LoadCoinsAsync(CancellationToken ct)
        {
            try
            {
                await DelayIfNeeded(ct);
                var url = "https://api.coingecko.com/api/v3/coins/list?include_platform=true";
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return new();

                var json = await resp.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<List<CoinInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadCoins failed: {Msg}", ex.Message);
                return new();
            }
        }

        #endregion

        #region ASSETS LOADING

        private void AddAsset(string ex, string contract, string committedChain, bool dep, bool wd, decimal fee)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(contract)) return;
                var key = contract.ToLowerInvariant();
                if (!_assets.TryGetValue(key, out var dict))
                    _assets[key] = dict = new(StringComparer.OrdinalIgnoreCase);

                // Нормализуем имя биржи для соответствия с CoinGecko
                var normalizedEx = NormalizeExchangeName(ex);
                dict[normalizedEx] = new AssetRec
                {
                    Exchange = normalizedEx,
                    CommitedChain = committedChain,
                    Deposit = dep,
                    Withdraw = wd,
                    Fee = fee
                };
            }
            catch
            {
            }
        }

        private static string NormalizeExchangeName(string exchange)
        {
            return exchange.ToLowerInvariant() switch
            {
                "mexc" => "mxc",
                "bybit_spot" => "bybit",
                "binance" => "binance",
                "huobi" => "huobi",
                "okex" => "okex",
                "bitget" => "bitget",
                "bitmart" => "bitmart",
                "gate" => "gate",
                "kucoin" => "kucoin",
                "xt" => "xt",
                "lbank" => "lbank",
                "poloniex" => "poloniex",
                _ => exchange.ToLowerInvariant()
            };
        }

        private async Task<string?> GetBinanceAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api.binance.com/sapi/v1/capital/config/getall", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetBybitAsync(CancellationToken ct)
        {
            try
            {
                string ak = Environment.GetEnvironmentVariable("BYBIT_API_KEY") ?? throw new InvalidOperationException("BYBIT_API_KEY not found");
                string sk = Environment.GetEnvironmentVariable("BYBIT_SECRET_KEY") ?? throw new InvalidOperationException("BYBIT_SECRET_KEY not found");
                var ts = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 10_000).ToString();
                const string recv = "30000";
                string sigSrc = ts + ak + recv;
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
                var sig = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(sigSrc))).Replace("-", "")
                    .ToLower();
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.bybit.com/v5/asset/coin/query-info");
                req.Headers.Add("X-BAPI-API-KEY", ak);
                req.Headers.Add("X-BAPI-SIGN", sig);
                req.Headers.Add("X-BAPI-SIGN-TYPE", "2");
                req.Headers.Add("X-BAPI-TIMESTAMP", ts);
                req.Headers.Add("X-BAPI-RECV-WINDOW", recv);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.SendAsync(req, cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetOkxAsync(CancellationToken ct)
        {
            try
            {
                string ak = Environment.GetEnvironmentVariable("OKX_API_KEY") ?? throw new InvalidOperationException("OKX_API_KEY not found");
                string sk = Environment.GetEnvironmentVariable("OKX_SECRET_KEY") ?? throw new InvalidOperationException("OKX_SECRET_KEY not found");
                string pw = Environment.GetEnvironmentVariable("OKX_PASSPHRASE") ?? throw new InvalidOperationException("OKX_PASSPHRASE not found");
                var ts = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString("F3",
                    CultureInfo.InvariantCulture);
                const string ep = "/api/v5/asset/currencies", mtd = "GET";
                string msg = ts + mtd + ep;
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
                var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(msg)));
                var req = new HttpRequestMessage(HttpMethod.Get, "https://www.okx.com" + ep);
                req.Headers.Add("OK-ACCESS-KEY", ak);
                req.Headers.Add("OK-ACCESS-SIGN", sig);
                req.Headers.Add("OK-ACCESS-TIMESTAMP", ts);
                req.Headers.Add("OK-ACCESS-PASSPHRASE", pw);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.SendAsync(req, cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetMexcAsync(CancellationToken ct)
        {
            try
            {
                string ak = Environment.GetEnvironmentVariable("MEXC_API_KEY") ?? throw new InvalidOperationException("MEXC_API_KEY not found");
                string sk = Environment.GetEnvironmentVariable("MEXC_SECRET_KEY") ?? throw new InvalidOperationException("MEXC_SECRET_KEY not found");
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                const string recv = "30000";
                string qs = $"recvWindow={recv}&timestamp={ts}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
                var sig = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(qs))).Replace("-", "")
                    .ToLower();
                var url = $"https://api.mexc.com/api/v3/capital/config/getall?{qs}&signature={sig}";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-MEXC-APIKEY", ak);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.SendAsync(req, cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetBitgetAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api.bitget.com/api/v2/spot/public/coins", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetHtxAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api.huobi.pro/v1/settings/common/chains", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetBitmartAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api-cloud.bitmart.com/account/v1/currencies", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetGateAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api.gateio.ws/api/v4/spot/currencies", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetKucoinAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://api.kucoin.com/api/v3/currencies", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetXtAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var resp = await _http.GetAsync("https://sapi.xt.com/v4/public/wallet/support/currency", cts.Token);
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
            }
            catch
            {
                return null;
            }
        }

        private void FillBinanceAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                foreach (var coin in doc.RootElement.EnumerateArray())
                {
                    if (!coin.TryGetProperty("networkList", out var nets)) continue;
                    foreach (var net in nets.EnumerateArray())
                    {
                        try
                        {
                            if (!net.TryGetProperty("contractAddress", out var ctEl) ||
                                !net.TryGetProperty("network", out var nw) ||
                                !net.TryGetProperty("depositEnable", out var dep) ||
                                !net.TryGetProperty("withdrawEnable", out var wd) ||
                                !net.TryGetProperty("withdrawFee", out var fee)) continue;
                            AddAsset("binance", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                                wd.GetBoolean(), ParseDecimal(fee.GetString() ?? ""));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillBinanceAssets error: {Msg}", ex.Message);
            }
        }

        private void FillBybitAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("result", out var res) ||
                    !res.TryGetProperty("rows", out var rows)) return;
                foreach (var coin in rows.EnumerateArray())
                {
                    if (!coin.TryGetProperty("chains", out var chains)) continue;
                    foreach (var ch in chains.EnumerateArray())
                    {
                        try
                        {
                            if (!ch.TryGetProperty("contractAddress", out var ctEl) ||
                                !ch.TryGetProperty("chain", out var chainEl) ||
                                !ch.TryGetProperty("chainDeposit", out var depEl) ||
                                !ch.TryGetProperty("chainWithdraw", out var wdEl) ||
                                !ch.TryGetProperty("withdrawFee", out var feeEl)) continue;
                            AddAsset("bybit", ctEl.GetString() ?? "", chainEl.GetString() ?? "",
                                depEl.GetString() == "1", wdEl.GetString() == "1",
                                ParseDecimal(feeEl.GetString() ?? ""));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillBybitAssets error: {Msg}", ex.Message);
            }
        }

        private void FillOkxAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return;
                foreach (var coin in data.EnumerateArray())
                {
                    try
                    {
                        if (!coin.TryGetProperty("ctAddr", out var ctEl) || !coin.TryGetProperty("chain", out var ch) ||
                            !coin.TryGetProperty("canDep", out var dep) || !coin.TryGetProperty("canWd", out var wd) ||
                            !coin.TryGetProperty("fee", out var fee)) continue;
                        AddAsset("okex", ctEl.GetString() ?? "", ch.GetString() ?? "", dep.GetBoolean(),
                            wd.GetBoolean(), ParseDecimal(fee.GetString() ?? ""));
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillOkxAssets error: {Msg}", ex.Message);
            }
        }

        private void FillMexcAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                foreach (var coin in doc.RootElement.EnumerateArray())
                {
                    if (!coin.TryGetProperty("networkList", out var nets)) continue;
                    foreach (var net in nets.EnumerateArray())
                    {
                        try
                        {
                            if (!net.TryGetProperty("contract", out var ctEl) ||
                                !net.TryGetProperty("netWork", out var nw) ||
                                !net.TryGetProperty("depositEnable", out var dep) ||
                                !net.TryGetProperty("withdrawEnable", out var wd) ||
                                !net.TryGetProperty("withdrawFee", out var fee)) continue;
                            AddAsset("mexc", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                                wd.GetBoolean(), ParseDecimal(fee.GetString() ?? ""));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillMexcAssets error: {Msg}", ex.Message);
            }
        }

        private void FillBitgetAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return;
                foreach (var coin in data.EnumerateArray())
                {
                    if (!coin.TryGetProperty("chains", out var chains)) continue;
                    foreach (var ch in chains.EnumerateArray())
                    {
                        try
                        {
                            if (!ch.TryGetProperty("contractAddress", out var ctEl) ||
                                !ch.TryGetProperty("chain", out var nw) ||
                                !ch.TryGetProperty("rechargeable", out var dep) ||
                                !ch.TryGetProperty("withdrawable", out var wd) ||
                                !ch.TryGetProperty("withdrawFee", out var fee)) continue;
                            AddAsset("bitget", ctEl.GetString() ?? "", nw.GetString() ?? "",
                                bool.Parse(dep.GetString() ?? "false"), bool.Parse(wd.GetString() ?? "false"),
                                ParseDecimal(fee.GetString() ?? ""));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillBitgetAssets error: {Msg}", ex.Message);
            }
        }

        private void FillHtxAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return;
                foreach (var coin in data.EnumerateArray())
                {
                    try
                    {
                        if (!coin.TryGetProperty("ca", out var ctEl) || !coin.TryGetProperty("dn", out var nw) ||
                            !coin.TryGetProperty("de", out var dep) || !coin.TryGetProperty("we", out var wd)) continue;
                        AddAsset("huobi", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                            wd.GetBoolean(), 0m);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillHtxAssets error: {Msg}", ex.Message);
            }
        }

        private void FillBitmartAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("currencies", out var curs)) return;
                foreach (var coin in curs.EnumerateArray())
                {
                    try
                    {
                        if (!coin.TryGetProperty("contract_address", out var ctEl) ||
                            !coin.TryGetProperty("network", out var nw) ||
                            !coin.TryGetProperty("deposit_enabled", out var dep) ||
                            !coin.TryGetProperty("withdraw_enabled", out var wd) ||
                            !coin.TryGetProperty("withdraw_fee", out var fee)) continue;
                        AddAsset("bitmart", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                            wd.GetBoolean(), ParseDecimal(fee.GetString() ?? ""));
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillBitmartAssets error: {Msg}", ex.Message);
            }
        }

        private void FillGateAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                foreach (var coin in doc.RootElement.EnumerateArray())
                {
                    if (!coin.TryGetProperty("chains", out var chains)) continue;
                    foreach (var ch in chains.EnumerateArray())
                    {
                        try
                        {
                            if (!ch.TryGetProperty("addr", out var ctEl) || !ch.TryGetProperty("name", out var nw) ||
                                !ch.TryGetProperty("deposit_disabled", out var dep) ||
                                !ch.TryGetProperty("withdraw_disabled", out var wd)) continue;
                            AddAsset("gate", ctEl.GetString() ?? "", nw.GetString() ?? "", !dep.GetBoolean(),
                                !wd.GetBoolean(), 0m);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillGateAssets error: {Msg}", ex.Message);
            }
        }

        private void FillKucoinAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return;
                foreach (var coin in data.EnumerateArray())
                {
                    if (!coin.TryGetProperty("chains", out var chains) || chains.ValueKind != JsonValueKind.Array) continue;
                    foreach (var ch in chains.EnumerateArray())
                    {
                        try
                        {
                            if (!ch.TryGetProperty("contractAddress", out var ctEl) ||
                                !ch.TryGetProperty("chainName", out var nw) ||
                                !ch.TryGetProperty("isDepositEnabled", out var dep) ||
                                !ch.TryGetProperty("isWithdrawEnabled", out var wd) ||
                                !ch.TryGetProperty("withdrawalMinFee", out var fee)) continue;
                            AddAsset("kucoin", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                                wd.GetBoolean(), ParseDecimal(fee.GetString() ?? ""));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillKucoinAssets error: {Msg}", ex.Message);
            }
        }

        private void FillXtAssets(string? json)
        {
            try
            {
                if (json == null) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("result", out var res)) return;
                foreach (var coin in res.EnumerateArray())
                {
                    if (!coin.TryGetProperty("supportChains", out var chains)) continue;
                    foreach (var ch in chains.EnumerateArray())
                    {
                        try
                        {
                            if (!ch.TryGetProperty("contract", out var ctEl) ||
                                !ch.TryGetProperty("chain", out var nw) ||
                                !ch.TryGetProperty("depositEnabled", out var dep) ||
                                !ch.TryGetProperty("withdrawEnabled", out var wd)) continue;
                            decimal fee = 0;
                            if (ch.TryGetProperty("withdrawFeeAmount", out var feeEl))
                                fee = feeEl.ValueKind == JsonValueKind.String
                                    ? ParseDecimal(feeEl.GetString() ?? "0")
                                    : feeEl.GetDecimal();
                            AddAsset("xt", ctEl.GetString() ?? "", nw.GetString() ?? "", dep.GetBoolean(),
                                wd.GetBoolean(), fee);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FillXtAssets error: {Msg}", ex.Message);
            }
        }

        private async Task LoadAssetsAsync(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Loading assets from exchanges...");
                _assets.Clear();

                var binanceTask = GetBinanceAsync(ct);
                var bybitTask = GetBybitAsync(ct);
                var okxTask = GetOkxAsync(ct);
                var mexcTask = GetMexcAsync(ct);
                var bitgetTask = GetBitgetAsync(ct);
                var htxTask = GetHtxAsync(ct);
                var bitmartTask = GetBitmartAsync(ct);
                var gateTask = GetGateAsync(ct);
                var kucoinTask = GetKucoinAsync(ct);
                var xtTask = GetXtAsync(ct);

                await Task.WhenAll(binanceTask, bybitTask, okxTask, mexcTask, bitgetTask, htxTask, bitmartTask, gateTask, kucoinTask,
                    xtTask);

                _logger.LogInformation("Assets API calls completed");

                FillBinanceAssets(await binanceTask);
                FillBybitAssets(await bybitTask);
                FillOkxAssets(await okxTask);
                FillMexcAssets(await mexcTask);
                FillBitgetAssets(await bitgetTask);
                FillHtxAssets(await htxTask);
                FillBitmartAssets(await bitmartTask);
                FillGateAssets(await gateTask);
                FillKucoinAssets(await kucoinTask);
                FillXtAssets(await xtTask);

                var assetsByExchange = _assets
                    .SelectMany(kv => kv.Value.Values.Select(v => v.Exchange))
                    .GroupBy(e => e)
                    .ToDictionary(g => g.Key, g => g.Count());

                _logger.LogInformation("Assets loaded: Total={Total}, ByExchange={@Exchanges}",
                    _assets.Sum(kv => kv.Value.Count), assetsByExchange);
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadAssets error: {Msg}", ex.Message);
            }
        }

        #endregion

        #region BUILD & FILTER

        private List<TokenResult> BuildFinalList(List<FullTicker> tickers, List<CoinInfo> coins)
        {
            var result = new List<TokenResult>();
            int processedCoins = 0;
            int coinsWithPlatforms = 0;
            int matchedContracts = 0;
            int confirmedExchanges = 0;

            try
            {
                var byCoin = tickers.Where(t => !string.IsNullOrEmpty(t.CoinId))
                    .GroupBy(t => t.CoinId!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "BuildFinalList: Processing {CoinsCount} coins, {TickersCount} tickers grouped by {UniqueCoinIds} coin IDs",
                    coins.Count, tickers.Count, byCoin.Count);

                foreach (var coin in coins)
                {
                    processedCoins++;
                    if (processedCoins % 1000 == 0)
                        _logger.LogInformation("Processed {Count} coins...", processedCoins);

                    try
                    {
                        if (string.IsNullOrEmpty(coin.Id) || coin.Platforms == null) continue;
                        coinsWithPlatforms++;
                        var symbol = coin.Symbol?.ToUpperInvariant() ?? coin.Id.ToUpperInvariant();

                        foreach (var (chain, contract) in coin.Platforms)
                        {
                            try
                            {
                                if (!TargetChains.Contains(chain) || string.IsNullOrWhiteSpace(contract)) continue;
                                if (!byCoin.TryGetValue(coin.Id, out var tlist)) continue;

                                var exchanges = tlist
                                    .Where(t => !string.IsNullOrEmpty(t.Target) && AllowedTargets.Contains(t.Target))
                                    .Select(t => new ExchangeInfo
                                    {
                                        Name = t.ExchangeName,
                                        Base = t.Base ?? symbol,
                                        Target = t.Target!,
                                        Last = t.Last ?? 0m,
                                        Volume = t.Volume ?? 0m,
                                        TradeUrl = t.TradeUrl ?? "",
                                        Confirmed = false
                                    }).ToList();

                                if (exchanges.Count == 0) continue;

                                // Проверяем наличие данных от API бирж
                                var key = contract.ToLowerInvariant();
                                if (_assets.TryGetValue(key, out var dict))
                                {
                                    matchedContracts++;
                                    foreach (var ex in exchanges)
                                    {
                                        try
                                        {
                                            var normalizedName = NormalizeExchangeName(ex.Name);
                                            if (dict.TryGetValue(ex.Name, out var asset) || dict.TryGetValue(normalizedName, out asset))
                                            {
                                                ex.CommitedChain = asset.CommitedChain;
                                                ex.Confirmed = true;
                                                ex.IsDepositEnabled = asset.Deposit;
                                                ex.IsWithdrawEnabled = asset.Withdraw;
                                                ex.WithdrawFee = asset.Fee;
                                                confirmedExchanges++;
                                            }
                                        }
                                        catch (Exception exErr)
                                        {
                                            _logger.LogWarning("Error processing exchange {Ex}: {Msg}", ex.Name, exErr.Message);
                                        }
                                    }
                                }

                                // Для бирж без данных от API - помечаем как подтвержденные с дефолтными значениями
                                var unconfirmedCount = exchanges.Count(e => !e.Confirmed);
                                if (unconfirmedCount > 0)
                                {
                                    foreach (var ex in exchanges.Where(e => !e.Confirmed))
                                    {
                                        ex.CommitedChain = chain; // Используем chain из CoinGecko
                                        ex.Confirmed = true;
                                        ex.IsDepositEnabled = true;
                                        ex.IsWithdrawEnabled = true;
                                        ex.WithdrawFee = 0;
                                    }
                                }
                                if (exchanges.Count > 0)
                                {
                                    result.Add(new TokenResult
                                    {
                                        Symbol = symbol,
                                        Chain = chain,
                                        ContractAddress = contract,
                                        DexPrice = 0,
                                        DexQuotePrice = 0,
                                        DexQuotePriceImpact = 0,
                                        Liquidity = 0,
                                        FDV = 0,
                                        Capitalization = 0,
                                        Exchanges = exchanges
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("Error processing chain {Chain} for coin {CoinId}: {Msg}", chain, coin.Id, ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Error processing coin {CoinId}: {Msg}", coin.Id, ex.Message);
                    }
                }

                // Статистика по биржам в результате
                var exchangeStats = result
                    .SelectMany(t => t.Exchanges.Select(e => e.Name))
                    .GroupBy(e => e)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                _logger.LogInformation(
                    "BuildFinalList stats: ProcessedCoins={Processed}, WithPlatforms={Platforms}, MatchedContracts={Matched}, ConfirmedExchanges={Confirmed}, ResultTokens={Result}",
                    processedCoins, coinsWithPlatforms, matchedContracts, confirmedExchanges, result.Count);

                _logger.LogInformation("Exchanges in result: {@ExchangeStats}", exchangeStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BuildFinalList error: {Msg}", ex.Message);
            }

            return result;
        }

        private List<TokenResult> LeaveOnlyRealChains(List<TokenResult> src)
        {
            try
            {
                return src
                    .GroupBy(t => (t.Symbol, t.ContractAddress))
                    .SelectMany(g =>
                    {
                        try
                        {
                            var confirmed = g.Where(t => t.Exchanges.Any(e => e.Confirmed)).ToList();
                            if (confirmed.Count == 0) return g;

                            var realChains = confirmed
                                .Select(t => t.Exchanges.First(e => e.Confirmed).CommitedChain)
                                .Select(NormalizeChain)
                                .Where(c => !string.IsNullOrEmpty(c))
                                .Distinct()
                                .ToList();

                            if (realChains.Count == 1)
                                return g.Where(t =>
                                    t.Chain.Equals(realChains[0], StringComparison.OrdinalIgnoreCase));

                            return g;
                        }
                        catch
                        {
                            return g;
                        }
                    })
                    .ToList();
            }
            catch
            {
                return src;
            }
        }

        private List<TokenResult> FilterByTargets(List<TokenResult> src, HashSet<string> targets)
        {
            var list = new List<TokenResult>();
            try
            {
                foreach (var t in src)
                {
                    try
                    {
                        var filteredEx = t.Exchanges.Where(e => targets.Contains(e.Target)).ToList();
                        if (filteredEx.Count == 0) continue;
                        list.Add(new TokenResult
                        {
                            Symbol = t.Symbol,
                            Chain = t.Chain,
                            ContractAddress = t.ContractAddress,
                            DexPrice = t.DexPrice,
                            DexQuotePrice = t.DexQuotePrice,
                            DexQuotePriceImpact = t.DexQuotePriceImpact,
                            Liquidity = t.Liquidity,
                            FDV = t.FDV,
                            Capitalization = t.Capitalization,
                            Exchanges = filteredEx
                        });
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return list;
        }

        #endregion

        #region MAIN EXECUTION

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DictService started! Waiting 5 seconds before first run...");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("=== DICT UPDATE START ===");
                    var startTime = DateTime.UtcNow;

                    _logger.LogInformation("Loading tickers, coins and assets...");

                    var tickersTask = LoadAllTickersAsync(stoppingToken);
                    var coinsTask = LoadCoinsAsync(stoppingToken);
                    var assetsTask = LoadAssetsAsync(stoppingToken);

                    await Task.WhenAll(tickersTask, coinsTask, assetsTask);

                    _logger.LogInformation("Loaded: Tickers={Tickers}, Coins={Coins}",
                        tickersTask.Result.Count, coinsTask.Result.Count);

                    // Анализ тикеров по биржам
                    var tickersByExchange = tickersTask.Result
                        .GroupBy(t => t.ExchangeName)
                        .ToDictionary(g => g.Key, g => g.Count());
                    _logger.LogInformation("Tickers by exchange: {@Exchanges}", tickersByExchange);

                    // Сохраняем сырые тикеры (первые 1000)
                    Directory.CreateDirectory("./Temp");
                    await File.WriteAllTextAsync(@"./Temp/1_tickers_raw.json",
                        JsonSerializer.Serialize(tickersTask.Result.Take(1000),
                            new JsonSerializerOptions { WriteIndented = true }));

                    _logger.LogInformation("Building final list...");
                    var final = BuildFinalList(tickersTask.Result, coinsTask.Result);
                    _logger.LogInformation("Before chain filter: {Count}", final.Count);

                    // Анализ бирж до фильтрации
                    var beforeFilterExchanges = final
                        .SelectMany(t => t.Exchanges.Select(e => e.Name))
                        .GroupBy(e => e)
                        .ToDictionary(g => g.Key, g => g.Count());
                    _logger.LogInformation("Exchanges before filter: {@Exchanges}", beforeFilterExchanges);

                    // Сохраняем до фильтрации (первые 100)
                    await File.WriteAllTextAsync(@"./Temp/2_before_chain_filter.json",
                        JsonSerializer.Serialize(final.Take(100), new JsonSerializerOptions { WriteIndented = true }));

                    final = LeaveOnlyRealChains(final);
                    _logger.LogInformation("After chain filter: {Count}", final.Count);

                    // Анализ бирж после фильтрации
                    var afterFilterExchanges = final
                        .SelectMany(t => t.Exchanges.Select(e => e.Name))
                        .GroupBy(e => e)
                        .ToDictionary(g => g.Key, g => g.Count());
                    _logger.LogInformation("Exchanges after filter: {@Exchanges}", afterFilterExchanges);

                    _cache.Set("dict", final);

                    var usdtDict = FilterByTargets(final,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USDT" });
                    var solEthDict = FilterByTargets(final,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SOL", "ETH" });
                    var usdcDict = FilterByTargets(final,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USDC" });

                    _cache.Set("dict_usdt", usdtDict);
                    _cache.Set("dict_sol_eth", solEthDict);
                    _cache.Set("dict_usdc", usdcDict);

                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    _logger.LogInformation(
                        "Saved: all={All}, usdt={Usdt}, sol/eth={SolEth}, usdc={Usdc}, elapsed={Elapsed}s",
                        final.Count, usdtDict.Count, solEthDict.Count, usdcDict.Count, elapsed);

                    // Сохраняем все финальные результаты
                    await File.WriteAllTextAsync(@"./Temp/3_final_all.json",
                        JsonSerializer.Serialize(final, new JsonSerializerOptions { WriteIndented = true }));
                    await File.WriteAllTextAsync(@"./Temp/4_final_usdt.json",
                        JsonSerializer.Serialize(usdtDict, new JsonSerializerOptions { WriteIndented = true }));
                    await File.WriteAllTextAsync(@"./Temp/TestDict.json",
                        JsonSerializer.Serialize(final, new JsonSerializerOptions { WriteIndented = true }));

                    _logger.LogInformation("=== DICT UPDATE COMPLETE ===");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExecuteAsync error: {Msg}", ex.Message);
                }

                _logger.LogInformation("Waiting 1 minute before next update...");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        #endregion
    }
}