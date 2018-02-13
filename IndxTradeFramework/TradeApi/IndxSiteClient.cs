using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using IndxTradeFramework.TradeApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace IndxTradeFramework.TradeApi
{
    public class IndxSiteClient
    {
        private readonly RestClient _restClient = new RestClient("https://indx.ru/");

        public IndxSiteClient()
        {
            _restClient.CookieContainer = new CookieContainer();
            _restClient.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.119 Safari/537.36 OPR/51.0.2830.26";

            Init();
        }

        private void Init()
        {
            _restClient.Execute(new RestRequest("trade/0/").AddHeader("Upgrade-Insecure-Requests", "1"));

            int code = (int) _restClient.Execute(new RestRequest("setdefence", Method.POST).AddHeader("X-Requested-With", "XMLHttpRequest"))
                .StatusCode;

            if (code == 200)
            {
                _restClient.Execute(new RestRequest("trade/0/"));
            }
        }

        public class Offer
        {
            public int Count { get; set; }
            public double Price { get; set; }
            public long Id { get; set; }
            public string Nickname { get; set; }
            public TradeDirection Direction { get; set; }
            public string SymbolName { get; set; }
            public DateTime? CreateDate { get; set; }
            public DateTime? UpdateDate { get; set; }
            public int SymbolId { get; set; }
        }

        public class DealHistory
        {
            public long Id { get; set; }
            public DateTime Time { get; set; }
            public string Name { get; set; }
            public int SymbolId { get; set; }
            public TradeDirection Direction { get; set; }
            public double Price { get; set; }
            public Instrument Instrument { get; set; }
            public string BuyerNickname { get; set; }
            public string SellerNickname { get; set; }
            public int Count { get; set; }
        }

        public class DealsHistoryResponse
        {
            public List<DealHistory> Elements { get; set; }
            public int TotalRows { get; set; }
        }

        private DealsHistoryResponse GetDealsHistory(Instrument instrument, TradeDirection? direction = null, DateTime? startDate = null, DateTime? endDate = null, int page = 1)
        {
            try
            {
                JObject requestJsonObject = new JObject()
                {
                    ["field"] = 8,
                    ["only"] = 0,
                    ["orderby"] = 1,
                    ["page"] = page,
                    ["size"] = 50,
                    ["symbolID"] = (int) instrument
                };
                
                if (startDate == null)
                {
                    startDate = DateTime.Now - TimeSpan.FromDays(1);
                }

                if (endDate == null)
                {
                    endDate = DateTime.Now + TimeSpan.FromDays(1);
                }
            
                requestJsonObject["start"] = startDate.Value.ToString("dd.MM.yyyy");
                requestJsonObject["end"] = endDate.Value.ToString("dd.MM.yyyy");
               
                var data = _restClient.Execute(new RestRequest("TradingStats.asmx/GetDealsHistory", Method.POST)
                    .AddHeader("Content-Type", "application/json; charset=UTF-8")
                    .AddParameter("application/json", JsonConvert.SerializeObject(requestJsonObject), ParameterType.RequestBody)
                    .AddHeader("Referer", "https://indx.ru/trade/0/")
                    .AddHeader("X-Requested-With", "XMLHttpRequest"));

                if (data.StatusCode == HttpStatusCode.InternalServerError)
                {
                    throw new RequestException(-47);
                }
                
                var jResponse = JObject.Parse(data.Content);

                Console.WriteLine(jResponse);
                
                if (jResponse["d"]?["history"] != null)
                {
                    var jArr = jResponse["d"]["history"].Value<JArray>();

                    List<DealHistory> deals = new List<DealHistory>();
                    
                    foreach (var jToken in jArr)
                    {
                        var e = (JObject) jToken;

                        var date = DateTime.FromFileTime(e["t"].Value<long>());
                        
                        var dir = e["isbid"].Value<bool>() ? TradeDirection.Buy : TradeDirection.Sell;

                        if (direction != null)
                        {
                            if (dir != direction)
                            {
                                continue;
                            }
                        }
                                
                        deals.Add(new DealHistory()
                        {
                            Count = e["amount"].Value<int>(),
                            Id = e["id"].Value<long>(),
                            Direction = dir,
                            Instrument = instrument,
                            BuyerNickname = e["bnick"].Value<string>(),
                            SellerNickname = e["anick"].Value<string>(),
                            Name = e["name"].Value<string>(),
                            SymbolId = e["symbolid"].Value<int>(),
                            Price = e["Price"].Value<double>(),
                            Time = date
                        });
                    }

                    int totalRows = jResponse["d"]["page"]["rows"].Value<int>();
                    
                    return new DealsHistoryResponse() { Elements = deals, TotalRows = totalRows };
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace + " " + e.Message);
                throw new RequestException(-45);
            }

            return new DealsHistoryResponse() { Elements = new List<DealHistory>(), TotalRows = 0 };
        }
        
        public List<Offer> GetOffers(Instrument instrument, TradeDirection? direction = null, bool fullQueue = false)
        {
            try
            {
                JObject requestJsonObject = new JObject()
                {
                    ["fullqueue"] = fullQueue,
                    ["symbolid"] = (int) instrument
                };
            
                var data = _restClient.Execute(new RestRequest("TradingStats.asmx/GetOffers", Method.POST)
                    .AddHeader("Content-Type", "application/json; charset=UTF-8")
                    .AddParameter("application/json", JsonConvert.SerializeObject(requestJsonObject), ParameterType.RequestBody)
                    .AddHeader("Referer", "https://indx.ru/trade/0/")
                    .AddHeader("X-Requested-With", "XMLHttpRequest"));

                var jResponse = JObject.Parse(data.Content);

                if (jResponse["d"]?["value"] != null)
                {
                    var jArr = jResponse["d"]["value"].Value<JArray>();

                    List<Offer> offers = new List<Offer>();
                    
                    foreach (var jToken in jArr)
                    {
                        var e = (JObject) jToken;

                        foreach (var jT in e["Offers"].Value<JArray>())
                        {
                            var el = (JObject) jT;

                            DateTime? createDate = null;
                            DateTime? updateDate = null;
                            
                            try
                            {
                                createDate = DateTime.Parse(el["DateCrt"].Value<string>());
                            }
                            catch (Exception ex)
                            {
                                
                            }

                            try
                            {
                                updateDate = DateTime.Parse(el["DateUpd"].Value<string>());
                            }
                            catch (Exception ex)
                            {
                                
                            }
                            
                           
                            var dir = el["IsBid"].Value<bool>() ? TradeDirection.Buy : TradeDirection.Sell;

                            if (direction != null)
                            {
                                if (dir != direction)
                                {
                                    continue;
                                }
                            }
                                
                            offers.Add(new Offer()
                            {
                                Count = el["Amount"].Value<int>(),
                                Id = el["ID"].Value<long>(),
                                Nickname = el["IsAnonymous"].Value<bool>() ? null : el["Nickname"].Value<string>(),
                                Direction = dir,
                                CreateDate = createDate,
                                UpdateDate = updateDate,
                                Price = el["Price"].Value<double>(),
                                SymbolId = el["SymbolID"].Value<int>(),
                                SymbolName = el["SymbolName"].Value<string>()
                            });
                            
                        }
                    }

                    return offers;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace + " " + e.Message);
                throw new RequestException(-45);
            }

            return new List<Offer>();
        }
    }
}