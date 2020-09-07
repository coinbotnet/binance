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
        private readonly HttpClient _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
        });

        private ExchangeInfo _info = null;

        public StockApiService(IMapper mapper)
        {
            _mapper = mapper;
            _client.BaseAddress = new Uri(_serviceUrl);

            var res = GetExchangeInfo().Result;

            if (res.Success)
                _info = res.Data;
        }

        private async Task<ServiceResponse<ExchangeInfo>> GetExchangeInfo()
        {
            try
            {

                var response = await _client.GetAsync("api/v3/exchangeInfo");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<ExchangeInfo>(json);

                    return new ServiceResponse<ExchangeInfo>(0, deserialized, "OK");
                }
                else
                    return new ServiceResponse<ExchangeInfo>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException ex)
            {
                return new ServiceResponse<ExchangeInfo>(-1, null, "Network problems.");
            }
            catch
            {
                throw;
            }
        }

        public async Task<ServiceResponse<Transaction>> GetOrder(string baseCoin, string targetCoin, string apiKey, string secret, string orderRefId)
        {
            try
            {

                var apiUrl = "api/v3/order?";

                var reqUrl = string.Format(CultureInfo.InvariantCulture, "symbol={0}{1}&origClientOrderId={2}&recvWindow={4}&timestamp={3}",
                    targetCoin,
                    baseCoin,
                    orderRefId,
                    Helpers.GetUnixTimeInMilliseconds(),
                    _recvWindow
                );

                var apiSign = Helpers.GetHashSHA256(reqUrl, secret);
                _client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                var response = await _client.GetAsync(apiUrl + reqUrl + $"&signature={apiSign}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TransactionDTO>(json);

                    return new ServiceResponse<Transaction>(0, _mapper.Map<TransactionDTO, Transaction>(deserialized), json);

                }
                else
                    return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException ex)
            {
                return new ServiceResponse<Transaction>(-1, null, "Network problems.");
            }
            catch
            {
                throw;
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

            try
            {

                var response = await _client.GetAsync(string.Format("api/v3/ticker/price?symbol={1}{0}", baseCoin, targetCoin));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TickDTOResult>(json);

                    return new ServiceResponse<Tick>(0, _mapper.Map<Tick>(deserialized), json);
                }
                else
                    return new ServiceResponse<Tick>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException ex)
            {
                return new ServiceResponse<Tick>(-1, null, "Network problems.");
            }
            catch
            {
                throw;
            }

        }

        public async Task<ServiceResponse<Transaction>> PlaceBuyOrder(string baseCoin, string targetCoin, double stack, string apiKey, string secret, double rate, bool? testOnly = false)
        {

            try
            {

                var symbolInfo = _info.symbols.FirstOrDefault(x => x.baseAsset == targetCoin && x.quoteAsset == baseCoin);

                if (symbolInfo != null)
                {
                    var filter = symbolInfo.filters.FirstOrDefault(x => x.filterType == "LOT_SIZE");

                    var filteredStack = ((int)((stack / rate) / filter.stepSize)) * filter.stepSize;

                    var apiUrl = $"api/v3/order{(testOnly.Value ? "/test" : string.Empty)}";

                    var keyValues = new List<KeyValuePair<string, string>>();

                    keyValues.Add(new KeyValuePair<string, string>("symbol", targetCoin + baseCoin));
                    keyValues.Add(new KeyValuePair<string, string>("side", "BUY"));
                    keyValues.Add(new KeyValuePair<string, string>("type", "LIMIT"));
                    keyValues.Add(new KeyValuePair<string, string>("timeInForce", "GTC"));
                    keyValues.Add(new KeyValuePair<string, string>("quantity", filteredStack.ToString("0.00000000", CultureInfo.InvariantCulture)));
                    keyValues.Add(new KeyValuePair<string, string>("price", rate.ToString("0.00000000", CultureInfo.InvariantCulture)));
                    keyValues.Add(new KeyValuePair<string, string>("recvWindow", _recvWindow.ToString()));
                    keyValues.Add(new KeyValuePair<string, string>("timestamp", Helpers.GetUnixTimeInMilliseconds().ToString()));

                    var apiSign = Helpers.GetHashSHA256(string.Join("&", keyValues.Select(x => $"{x.Key}={x.Value}")), secret);
                    _client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                    keyValues.Add(new KeyValuePair<string, string>("signature", apiSign));

                    var response = await _client.PostAsync(apiUrl, new FormUrlEncodedContent(keyValues));

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var deserialized = JsonConvert.DeserializeObject<TransactionMadeDTO>(json);

                        return new ServiceResponse<Transaction>(0, _mapper.Map<Transaction>(deserialized), json);
                    }
                    else
                        return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());

                }
                else
                    return new ServiceResponse<Transaction>(167, null, "Problem getting correct stack size");
            }
            catch (HttpRequestException ex)
            {
                return new ServiceResponse<Transaction>(-1, null, "Network problems.");
            }
            catch
            {
                throw;
            }

        }

        public async Task<ServiceResponse<Transaction>> PlaceSellOrder(string baseCoin, string targetCoin, double stack, string apiKey, string secret, double qty, double toSellFor, double? raisedChangeToSell = null, bool? testOnly = false)
        {
            try
            {
                var apiUrl = $"api/v3/order{(testOnly.Value ? "/test" : string.Empty)}";
                var keyValues = new List<KeyValuePair<string, string>>();

                keyValues.Add(new KeyValuePair<string, string>("symbol", targetCoin + baseCoin));
                keyValues.Add(new KeyValuePair<string, string>("side", "SELL"));
                keyValues.Add(new KeyValuePair<string, string>("type", "LIMIT"));
                keyValues.Add(new KeyValuePair<string, string>("timeInForce", "GTC"));
                keyValues.Add(new KeyValuePair<string, string>("quantity", qty.ToString("0.00000000", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("price", raisedChangeToSell == null ? toSellFor.ToString("0.00000000", CultureInfo.InvariantCulture) : raisedChangeToSell.Value.ToString("0.00000000", CultureInfo.InvariantCulture)));
                keyValues.Add(new KeyValuePair<string, string>("recvWindow", _recvWindow.ToString()));
                keyValues.Add(new KeyValuePair<string, string>("timestamp", Helpers.GetUnixTimeInMilliseconds().ToString()));

                var apiSign = Helpers.GetHashSHA256(string.Join("&", keyValues.Select(x => $"{x.Key}={x.Value}")), secret);
                _client.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

                keyValues.Add(new KeyValuePair<string, string>("signature", apiSign));

                var response = await _client.PostAsync(apiUrl, new FormUrlEncodedContent(keyValues));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<TransactionMadeDTO>(json);

                    return new ServiceResponse<Transaction>(0, _mapper.Map<Transaction>(deserialized), json);

                }
                else
                    return new ServiceResponse<Transaction>((int)response.StatusCode, null, await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException ex)
            {
                return new ServiceResponse<Transaction>(-1, null, "Network problems.");
            }
            catch
            {
                throw;
            }

        }
    }
}
