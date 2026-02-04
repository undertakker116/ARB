using System;
using ARB1;

namespace ARB
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Load .env file
            DotNetEnv.Env.Load();
            
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddMemoryCache();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            Dictionary<string, string> clients = new()
            {
                { "bybit", "https://api.bybit.com" },
                { "okx", "https://www.okx.com" },
                { "mexc", "https://api.mexc.com" },
                { "mexc_futures", "https://contract.mexc.com" },
                { "bitget", "https://api.bitget.com" },
                { "htx", "https://api.huobi.pro" },
                { "bitmart", "https://api-cloud.bitmart.com" },
                { "gate", "https://api.gateio.ws" },
                { "kucoin", "https://api.kucoin.com" },
                { "xt", "https://sapi.xt.com" },
                { "lbank", "https://api.lbkex.com" },
                { "binance", "https://api.binance.com" },
                { "poloniex", "https://api.poloniex.com" }
            };

            foreach (var client in clients)
            {
                builder.Services.AddHttpClient(client.Key, c =>
                {
                    c.BaseAddress = new Uri(client.Value);
                    c.DefaultRequestHeaders.Add("Accept", "application/json");
                });
            }

            builder.Services.AddHostedService<BinanceService>();
            builder.Services.AddHostedService<BitgetService>();
            builder.Services.AddHostedService<BitmartService>();
            builder.Services.AddHostedService<BybitService>();
            builder.Services.AddHostedService<GateService>();
            builder.Services.AddHostedService<HtxService>();
            builder.Services.AddHostedService<KucoinService>();
            builder.Services.AddHostedService<LbankService>();
            builder.Services.AddHostedService<MexcService>();
            builder.Services.AddHostedService<OkxService>();
            builder.Services.AddHostedService<PoloniexService>();
            builder.Services.AddHostedService<XtService>();

            
            //builder.Services.AddHostedService<DictService>();
            builder.Services.AddHostedService<CexService>();
            builder.Services.AddHostedService<OkxDexService>();

            //builder.Services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowAll",
            //        policy => policy.AllowAnyOrigin()
            //                        .AllowAnyHeader()
            //                        .AllowAnyMethod());
            //});

            //builder.WebHost.UseUrls("http://localhost:5000");

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();
            app.UseRouting();
            app.MapControllers();
            app.Run();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
        }
    }
}