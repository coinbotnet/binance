using System;
using System.Threading.Tasks;
using Coinbot.Domain.Contracts;
using Coinbot.Domain.Contracts.Models;
using Coinbot.Domain.Contracts.Models.StockApiService;
using Coinbot.Binance.Models;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Linq;
using AutoMapper;
using System.Net.Http;
using System.Collections.Generic;

namespace Coinbot.Binance
{
    public class StockApiService : IStockApiService
    {
        private readonly string _serviceUrl = "https://api.binance.com/";
        private readonly int _recvWindow = 60000;
        private readonly IMapper _mapper;
        private readonly HttpClient _client = new HttpClient();

        public StockApiService(IMapper mapper)
        {
            _mapper = mapper;
            _client.BaseAddress = new Uri(_serviceUrl);
        }
        public async Task<ServiceResponse<Transaction>> GetOrder(string baseCoin, string targetCoin, string apiKey, string secret, string orderRefId)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_serviceUrl);

                var apiUrl = "api/v3/order?";

                var reqUrl = string.Format(CultureInfo.InvariantCulture, "symbol={0}{1}&origClientOrderId={2}&recvWindow={4}&timestamp={3}",
                    targetCoin,
                    baseCoin,
                    orderRefId,
                    Helpers.GetUnixTimeInMilliseconds(),
                    _recvWindow
                );

                var apiSign = Helpers.GetHashSHA256(reqUrl, secret);
                client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                var response = await client.GetAsync(apiUrl + reqUrl + $"&signature={apiSign}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TransactionDTO>(json);

                    return new ServiceResponse<Transaction>(0, _mapper.Map<TransactionDTO, Transaction>(deserialized), json);

                }
                else
                    return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
        }

        public StockInfo GetStockInfo()
        {
            return new StockInfo
            {
                FillOrKill = false
            };
        }

        public async Task<ServiceResponse<Tick>> GetTicker(string baseCoin, string targetCoin)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_serviceUrl);
                var response = await client.GetAsync(string.Format("api/v3/ticker/price?symbol={1}{0}", baseCoin, targetCoin));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TickDTOResult>(json);

                    return new ServiceResponse<Tick>(0, _mapper.Map<Tick>(deserialized), json);
                }
                else
                    return new ServiceResponse<Tick>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
        }

        public async Task<ServiceResponse<Transaction>> PlaceBuyOrder(string baseCoin, string targetCoin, double stack, string apiKey, string secret, double rate, bool? testOnly = false)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_serviceUrl);

                var apiUrl = $"api/v3/order{(testOnly.Value ? "/test" : string.Empty)}";

                var keyValues = new List<KeyValuePair<string, string>>();

                keyValues.Add(new KeyValuePair<string, string>("symbol", targetCoin + baseCoin));
                keyValues.Add(new KeyValuePair<string, string>("side", "BUY"));
                keyValues.Add(new KeyValuePair<string, string>("type", "LIMIT"));
                keyValues.Add(new KeyValuePair<string, string>("timeInForce", "GTC"));
                keyValues.Add(new KeyValuePair<string, string>("quantity", (stack / rate).ToString("0.00", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("price", rate.ToString("0.00000000", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("recvWindow", _recvWindow.ToString()));
                keyValues.Add(new KeyValuePair<string, string>("timestamp", Helpers.GetUnixTimeInMilliseconds().ToString()));

                var apiSign = Helpers.GetHashSHA256(string.Join("&", keyValues.Select(x => $"{x.Key}={x.Value}")), secret);
                client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                keyValues.Add(new KeyValuePair<string, string>("signature", apiSign));

                var response = await client.PostAsync(apiUrl, new FormUrlEncodedContent(keyValues));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TransactionMadeDTO>(json);

                    return new ServiceResponse<Transaction>(0, _mapper.Map<Transaction>(deserialized), json);
                }
                else
                    return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
        }

        public async Task<ServiceResponse<Transaction>> PlaceSellOrder(string baseCoin, string targetCoin, double stack, string apiKey, string secret, double qty, double toSellFor, double? raisedChangeToSell = null, bool? testOnly = false)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_serviceUrl);

                var apiUrl = $"api/v3/order{(testOnly.Value ? "/test" : string.Empty)}";

                var keyValues = new List<KeyValuePair<string, string>>();

                keyValues.Add(new KeyValuePair<string, string>("symbol", targetCoin + baseCoin));
                keyValues.Add(new KeyValuePair<string, string>("side", "SELL"));
                keyValues.Add(new KeyValuePair<string, string>("type", "LIMIT"));
                keyValues.Add(new KeyValuePair<string, string>("timeInForce", "GTC"));
                keyValues.Add(new KeyValuePair<string, string>("quantity", qty.ToString("0.00", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("price", raisedChangeToSell == null ? toSellFor.ToString("0.00000000", CultureInfo.InvariantCulture) : raisedChangeToSell.Value.ToString("0.00000000", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("recvWindow", _recvWindow.ToString()));
                keyValues.Add(new KeyValuePair<string, string>("timestamp", Helpers.GetUnixTimeInMilliseconds().ToString()));

                var apiSign = Helpers.GetHashSHA256(string.Join("&", keyValues.Select(x => $"{x.Key}={x.Value}")), secret);
                client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                keyValues.Add(new KeyValuePair<string, string>("signature", apiSign));

                var response = await client.PostAsync(apiUrl, new FormUrlEncodedContent(keyValues));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TransactionMadeDTO>(json);

                    return new ServiceResponse<Transaction>(0, _mapper.Map<Transaction>(deserialized), json);

                }
                else
                    return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
        }
    }
}
