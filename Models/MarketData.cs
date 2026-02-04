namespace ARB.Models
{
    public class MarketData
    {
        public string? Symbol { get; set; }
        public string? Chain { get; set; }
        public string? ContractAddress { get; set; }
        public decimal DexPrice { get; set; }
        public decimal DexQuotePrice { get; set; }
        public decimal DexQuotePriceImpact { get; set; }
        public decimal Liquidity { get; set; }
        public decimal FDV { get; set; }
        public decimal Capitalization { get; set; }
        public bool IsChainsConflict { get; set; }
        public List<MarketEx>? Exchanges { get; set; }
        
        // OKX DEX дополнительные данные
        public decimal DexVolume24h { get; set; }
        public decimal DexLiquidity { get; set; }
        public decimal DexMarketCap { get; set; }
        public decimal DexPriceChange24h { get; set; }
    }

    public class MarketEx
    {
        public string? Name { get; set; }
        public string? Base { get; set; }
        public string? Target { get; set; }
        public decimal Last { get; set; }
        public decimal Volume { get; set; }
        public decimal Turnover { get; set; }
        public string? TradeUrl { get; set; }
        public string? CommitedChain { get; set; }
        public bool Confirmed { get; set; }
        public bool IsDepositEnabled { get; set; }
        public bool IsWithdrawEnabled { get; set; }
        public decimal WithdrawFee { get; set; }
        public decimal BidPrice { get; set; }
        public decimal BidSize { get; set; }
        public decimal AskPrice { get; set; }
        public decimal AskSize { get; set; }
        public decimal SpreadLP { get; set; }
        public decimal SpreadD { get; set; }
    }
}