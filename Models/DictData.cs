using System.Text.Json.Serialization;

namespace ARB.Models
{
    public class TickerItem
    {
        [JsonPropertyName("Base")] public string? Base { get; set; }
        [JsonPropertyName("Target")] public string? Target { get; set; }
        [JsonPropertyName("Last")] public decimal? Last { get; set; }
        [JsonPropertyName("Volume")] public decimal? Volume { get; set; }
        [JsonPropertyName("Turnover")] public decimal? Turnover { get; set; }
        [JsonPropertyName("Trade_url")] public string? TradeUrl { get; set; }
        [JsonPropertyName("Coin_id")] public string? CoinId { get; set; }
    }

    public class ExchangeResponse
    {
        [JsonPropertyName("Tickers")] public List<TickerItem>? Tickers { get; set; }
    }

    public class CoinInfo
    {
        [JsonPropertyName("Id")] public string? Id { get; set; }
        [JsonPropertyName("Symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("Platforms")] public Dictionary<string, string>? Platforms { get; set; }
    }

    public class FullTicker
    {
        public string ExchangeName { get; set; } = null!;
        public string? Base { get; set; }
        public string? Target { get; set; }
        public string? TradeUrl { get; set; }
        public string? CoinId { get; set; }
        public decimal? Last { get; set; }
        public decimal? Volume { get; set; }
    }

    public class AssetRec
    {
        public string Exchange { get; init; } = default!;
        public string CommitedChain { get; init; } = default!;
        public bool Deposit { get; init; }
        public bool Withdraw { get; init; }
        public decimal Fee { get; init; }
    }

    public class TokenResult
    {
        [JsonPropertyName("Symbol")] public string Symbol { get; set; } = null!;
        [JsonPropertyName("Chain")] public string Chain { get; set; } = null!;
        [JsonPropertyName("ContractAddress")] public string ContractAddress { get; set; } = null!;
        [JsonPropertyName("DexPrice")] public decimal DexPrice { get; set; }
        [JsonPropertyName("DexQuotePrice")] public decimal DexQuotePrice { get; set; }
        [JsonPropertyName("DexQuotePriceImpact")] public decimal DexQuotePriceImpact { get; set; }
        [JsonPropertyName("Liquidity")] public decimal Liquidity { get; set; }
        [JsonPropertyName("FDV")] public decimal FDV { get; set; }
        [JsonPropertyName("Capitalization")] public decimal Capitalization { get; set; }
        [JsonPropertyName("Exchanges")] public List<ExchangeInfo> Exchanges { get; set; } = new();
    }

    public class ExchangeInfo
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = null!;
        [JsonPropertyName("Base")] public string Base { get; set; } = null!;
        [JsonPropertyName("Target")] public string Target { get; set; } = null!;
        [JsonPropertyName("Last")] public decimal Last { get; set; }
        [JsonPropertyName("Volume")] public decimal Volume { get; set; }
        [JsonPropertyName("Turnover")] public decimal Turnover { get; set; }
        [JsonPropertyName("TradeUrl")] public string TradeUrl { get; set; } = null!;
        [JsonPropertyName("CommitedChain")] public string CommitedChain { get; set; } = null!;
        [JsonPropertyName("Confirmed")] public bool Confirmed { get; set; }
        [JsonPropertyName("IsDepositEnabled")] public bool IsDepositEnabled { get; set; }
        [JsonPropertyName("IsWithdrawEnabled")] public bool IsWithdrawEnabled { get; set; }
        [JsonPropertyName("WithdrawFee")] public decimal WithdrawFee { get; set; }
    }
}