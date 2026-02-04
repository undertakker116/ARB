using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ARB.Controllers
{
    [ApiController]
    [Route("api")]
    public class DataController(IMemoryCache cache) : ControllerBase
    {
        private readonly IMemoryCache _cache = cache;

        [HttpGet("binance_spot")]
        public IActionResult GetBinanceSpotData()
        {
            if (_cache.TryGetValue("binance_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Binance spot data available");
        }

        [HttpGet("bitget_spot")]
        public IActionResult GetBitgetSpotData()
        {
            if (_cache.TryGetValue("bitget_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Bitget spot data available");
        }

        [HttpGet("bitmart_spot")]
        public IActionResult GetBitmartSpotData()
        {
            if (_cache.TryGetValue("bitmart_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Bitmart spot data available");
        }

        [HttpGet("bybit_spot")]
        public IActionResult GetBybitSpotData()
        {
            if (_cache.TryGetValue("bybit_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Bybit spot data available");
        }

        [HttpGet("gate_spot")]
        public IActionResult GetGateSpotData()
        {
            if (_cache.TryGetValue("gate_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Gate spot data available");
        }

        [HttpGet("htx_spot")]
        public IActionResult GetHtxSpotData()
        {
            if (_cache.TryGetValue("htx_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No HTX spot data available");
        }

        [HttpGet("kucoin_spot")]
        public IActionResult GetKucoinSpotData()
        {
            if (_cache.TryGetValue("kucoin_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No KuCoin spot data available");
        }

        [HttpGet("lbank_spot")]
        public IActionResult GetLbankSpotData()
        {
            if (_cache.TryGetValue("lbank_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Lbank spot data available");
        }

        [HttpGet("mexc_spot")]
        public IActionResult GetMexcSpotData()
        {
            if (_cache.TryGetValue("mexc_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No MEXC spot data available");
        }

        [HttpGet("okx_spot")]
        public IActionResult GetOkxSpotData()
        {
            if (_cache.TryGetValue("okx_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No OKX spot data available");
        }

        [HttpGet("poloniex_spot")]
        public IActionResult GetPoloniexSpotData()
        {
            if (_cache.TryGetValue("poloniex_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No Poloniex spot data available");
        }

        [HttpGet("xt_spot")]
        public IActionResult GetXtSpotData()
        {
            if (_cache.TryGetValue("xt_spot", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No XT spot data available");
        }

        // === DICTIONARY ENDPOINTS ===

        [HttpGet("dict")]
        public IActionResult GetDictData()
        {
            if (_cache.TryGetValue("dict", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No dictionary data available");
        }

        [HttpGet("dict_usdt")]
        public IActionResult GetDictUsdtData()
        {
            if (_cache.TryGetValue("dict_usdt", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No USDT dictionary data available");
        }

        [HttpGet("dict_sol_eth")]
        public IActionResult GetDictSolEthData()
        {
            if (_cache.TryGetValue("dict_sol_eth", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No SOL/ETH dictionary data available");
        }

        [HttpGet("dict_usdc")]
        public IActionResult GetDictUsdcData()
        {
            if (_cache.TryGetValue("dict_usdc", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No USDC dictionary data available");
        }

        // === PRICE SERVICE DICTIONARIES ===

        [HttpGet("price_dict")]
        public IActionResult GetPriceDictData()
        {
            if (_cache.TryGetValue("price_dict", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No price dictionary data available");
        }

        [HttpGet("price_dict_usdt")]
        public IActionResult GetPriceDictUsdtData()
        {
            if (_cache.TryGetValue("price_dict_usdt", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No price USDT dictionary data available");
        }

        [HttpGet("price_dict_sol_eth")]
        public IActionResult GetPriceDictSolEthData()
        {
            if (_cache.TryGetValue("price_dict_sol_eth", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No price SOL/ETH dictionary data available");
        }

        [HttpGet("price_dict_usdc")]
        public IActionResult GetPriceDictUsdcData()
        {
            if (_cache.TryGetValue("price_dict_usdc", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No price USDC dictionary data available");
        }

        [HttpGet("defillama_updates")]
        public IActionResult GetDefiLlamaUpdates()
        {
            if (_cache.TryGetValue("defillama_updates", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No DefiLlama price updates available");
        }

        [HttpGet("all_prices")]
        public IActionResult GetCexPrices()
        {
            if (_cache.TryGetValue("all_prices", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No CEX prices available");
        }

        // === OKX DEX ENDPOINTS ===

        [HttpGet("okx_dex_prices")]
        public IActionResult GetOkxDexPrices()
        {
            if (_cache.TryGetValue("okx_dex_prices", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No OKX DEX prices available");
        }

        [HttpGet("okx_dex_full_data")]
        public IActionResult GetOkxDexFullData()
        {
            if (_cache.TryGetValue("okx_dex_full_data", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No OKX DEX full data available");
        }

        [HttpGet("okx_dex_log")]
        public IActionResult GetOkxDexCurrentLog()
        {
            if (_cache.TryGetValue("okx_dex_current_log", out object? data))
            {
                return Content(data?.ToString() ?? "", "text/plain");
            }

            return NotFound("No OKX DEX log available");
        }

        [HttpGet("okx_dex_log_files")]
        public IActionResult GetOkxDexLogFiles()
        {
            if (_cache.TryGetValue("okx_dex_log_files", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No OKX DEX log files available");
        }

        [HttpGet("dict_with_dex_prices")]
        public IActionResult GetDictWithDexPrices()
        {
            if (_cache.TryGetValue("dict_with_dex_prices", out object? data))
            {
                return Ok(data);
            }

            return NotFound("No dictionary with DEX prices available");
        }

    }
}