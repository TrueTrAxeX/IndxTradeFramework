using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Parser.Xml;
using AngleSharp.Services;
using MartingaleCryptoBot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace IndxTradeFramework.TradeApi
{
    public class IndxTradeApi
    { 
        private readonly Credentials _credentials;
        private readonly RestClient _restClient;
        private readonly XmlParser _parser = new XmlParser();

        public class BalanceResponse
        {
            public double Price { get; set; }
            public double Wmz { get; set; }
            public string Nickname { get; set; }

            public List<Portfolio> Portfolios { get; set; }
            
            public override String ToString()
            {
                return $"Price: {Price}, Wmz: {Wmz}";
            }
        }

        public class OfferListResponse
        {
            public bool Success { get; set; }
            
            
        }

        public class OfferAddResponse
        {
            public bool Success { get; set; }
            public long OfferId { get; set; }
            public int Code { get; set; }
        }

        public class OfferDeleteResponse
        {
            public bool Success { get; set; }
            public long OfferId { get; set; }
            public int Code { get; set; }
        }

        public class OfferMy
        {
            public long OfferId { get; set; }
            public string Name { get; set; }
            public TradeDirection Direction { get; set; }
            public double Price { get; set; }
            public int Count { get; set; }
            public DateTime Date { get; set; }
        }

        public class Offer
        {
            public string NickName { get; set; }
            public TradeDirection Direction { get; set; }
            public double Price { get; set; }
            public int Count { get; set; }
        }
        
        public class OfferMyResponse
        {
            public bool Success { get; set; }
            public List<OfferMy> Data { get; set; }
        }

        public IndxTradeApi(Credentials credentials)
        {
            _restClient = new RestClient("https://secure.indx.ru");
            _restClient.Timeout = 5000;
            
            _credentials = credentials;
        }

        private readonly string _soapTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
    <soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
    <soap12:Body>
    <{1} xmlns=""http://indx.ru/"">
        <Request>{0}</Request>
    </{1}>
    </soap12:Body>
    </soap12:Envelope>";
 
        public OfferListResponse OfferListRequest(Instrument instrument, TradeDirection? direction = null, TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            {
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }

                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid};{(int)instrument}",
                        null),
                    ["Trading"] = new JObject()
                    {
                        ["ID"] = (int) instrument
                    }
                };

                var json = JsonConvert.SerializeObject(requestObj);
                
                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "OfferList"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);
                
                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return OfferListRequest(instrument, direction, timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }
                
                var xml = _parser.Parse(response.Content);
           
                var content = xml.QuerySelector("OfferListResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return OfferListRequest(instrument, direction, timeout, --maxAttempts);
                }

                if (responseCode == 0)
                {
                    if (jResponse["value"] != null)
                    {
                        var list = new List<Offer>();
                        
                        if (jResponse["value"].Type == JTokenType.String)
                        {
                            return new OfferListResponse() {Success = true};
                        }
                        
                        var arr = jResponse["value"].Value<JArray>();

                        foreach (var jToken in arr)
                        {
                            JObject el = (JObject) jToken;
                            
                            var n = el["Nickname"].Value<string>();
                            var dir = el["Incoming"].Value<int>() == 1 ? TradeDirection.Buy : TradeDirection.Sell;

                            if (direction != null)
                            {
                                if (dir != direction)
                                {
                                    continue;
                                }
                            }
                            
                            list.Add(new Offer()
                            {
                                NickName = string.IsNullOrEmpty(n) ? null : n,
                                Direction = dir,
                                Price = el["Price"].Value<double>(),
                                Count = el["Count"].Value<int>()
                            });
                        }
                        
                        return new OfferListResponse() {Success = true};
                    }
                    
                    return new OfferListResponse() {Success = false};
                }
                else
                {
                    throw new RequestException(responseCode);
                }
            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }
            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }   
        }

        public class HistoryTrade
        {
            public long Id { get; set; }
            public DateTime Date { get; set; }
            public string Name { get; set; }
            public TradeDirection Direction { get; set; }
            public int Count { get; set; }
            public double Price { get; set; }
        }

        public class HistoryTradingResponse
        {
            public List<HistoryTrade> Trades { get; set; }
            public bool Success { get; set; }
        }
        
        public HistoryTradingResponse HistoryTradingRequest(Instrument instrument, DateTime? dateStart = null, DateTime? dateEnd = null, TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            {
                if (dateStart == null)
                {
                    dateStart = DateTime.Now - TimeSpan.FromDays(1);
                }

                if (dateEnd == null)
                {
                    dateEnd = DateTime.Now + TimeSpan.FromDays(1);
                }
                
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }

                var dateStartStr = dateStart.Value.ToString("yyyyMMdd");
                var dateEndStr = dateEnd.Value.ToString("yyyyMMdd");
                
                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid};{(int)instrument};{dateStartStr};{dateEndStr}",
                        null),
                    ["Trading"] = new JObject()
                    {
                        ["ID"] = (int) instrument,
                        ["DateStart"] = dateStartStr,
                        ["DateEnd"] = dateEndStr
                    }
                };

                var json = JsonConvert.SerializeObject(requestObj);
                
                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
               
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "HistoryTrading"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);
                
                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return HistoryTradingRequest(instrument, dateStart, dateEnd, timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }
                
                var xml = _parser.Parse(response.Content);
           
                var content = xml.QuerySelector("HistoryTradingResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return HistoryTradingRequest(instrument, dateStart, dateEnd, timeout, --maxAttempts);
                }

                if (responseCode == 0)
                {
                    if (jResponse["value"] != null)
                    {
                        var list = new List<HistoryTrade>();
                        
                        if (jResponse["value"].Type == JTokenType.String)
                        {
                            return new HistoryTradingResponse() {Success = true, Trades = list};
                        }
                        
                        var arr = jResponse["value"].Value<JArray>();

                        foreach (var jToken in arr)
                        {
                            JObject el = (JObject) jToken;
                            
                            list.Add(new HistoryTrade()
                            {
                                Id = el["ID"].Value<long>(),
                                Name = el["Name"].Value<string>(),
                                Direction = el["IsBid"].Value<int>() == 1 ? TradeDirection.Buy : TradeDirection.Sell,
                                Price = el["Price"].Value<double>(),
                                Count = el["Count"].Value<int>(),
                                Date = DateTime.Parse(el["Stamp"].Value<string>())
                            });
                        }
                        
                        return new HistoryTradingResponse() {Success = true, Trades = list};
                    }
                    
                    return new HistoryTradingResponse() { Success = false };
                }
                else
                {
                    throw new RequestException(responseCode);
                }
            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }
            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }    
        }
        
        public OfferDeleteResponse OfferDeleteRequest(long offerId, TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            { 
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }
                
                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid};{offerId}",
                        null),
                    ["OfferID"] = offerId
                };

                var json = JsonConvert.SerializeObject(requestObj);
                
                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
               
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "OfferDelete"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);
                
                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return OfferDeleteRequest(offerId, timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }
                
                var xml = _parser.Parse(response.Content);

                var content = xml.QuerySelector("OfferDeleteResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return OfferDeleteRequest(offerId, timeout, --maxAttempts);
                }

                if (responseCode == 0)
                {
                    int code = jResponse["value"]["Code"].Value<int>();
                    long offId = jResponse["value"]["OfferID"].Value<long>();
                    
                    return new OfferDeleteResponse() {Success = true, OfferId = offId, Code = code};
                }
                else
                {
                    throw new RequestException(responseCode);
                }
            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }
            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }
        }

        public OfferMyResponse OfferMyRequest(Instrument instrument, DateTime? dateStart = null, DateTime? dateEnd = null, TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            {
                if (dateStart == null)
                {
                    dateStart = DateTime.Now - TimeSpan.FromDays(30);
                }

                if (dateEnd == null)
                {
                    dateEnd = dateStart + TimeSpan.FromDays(30);
                }
                
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }

                var dateStartStr = dateStart.Value.ToString("yyyyMMdd");
                var dateEndStr = dateEnd.Value.ToString("yyyyMMdd");
                
                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid};{(int)instrument};{dateStartStr};{dateEndStr}",
                        null),
                    ["Trading"] = new JObject()
                    {
                        ["ID"] = (int) instrument,
                        ["DateStart"] = dateStartStr,
                        ["DateEnd"] = dateEndStr
                    }
                };

                var json = JsonConvert.SerializeObject(requestObj);
                
                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
               
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "OfferMy"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);
                
                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return OfferMyRequest(instrument, dateStart, dateEnd, timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }
                
                var xml = _parser.Parse(response.Content);
           
                var content = xml.QuerySelector("OfferMyResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return OfferMyRequest(instrument, dateStart, dateEnd, timeout, --maxAttempts);
                }

                if (responseCode == 0)
                {
                    if (jResponse["value"] != null)
                    {
                        var list = new List<OfferMy>();
                        
                        if (jResponse["value"].Type == JTokenType.String)
                        {
                            return new OfferMyResponse() {Success = true, Data = list};
                        }
                        
                        var arr = jResponse["value"].Value<JArray>();

                        foreach (var jToken in arr)
                        {
                            JObject el = (JObject) jToken;
                            
                            list.Add(new OfferMy()
                            {
                                OfferId = el["OfferID"].Value<long>(),
                                Name = el["Name"].Value<string>(),
                                Direction = el["Incoming"].Value<int>() == 1 ? TradeDirection.Buy : TradeDirection.Sell,
                                Price = el["Price"].Value<double>(),
                                Count = el["Count"].Value<int>(),
                                Date = DateTime.Parse(el["Stamp"].Value<string>())
                            });
                        }
                        
                        return new OfferMyResponse() {Success = true, Data = list};
                    }
                    
                    return new OfferMyResponse() {Success = false};
                }
                else
                {
                    throw new RequestException(responseCode);
                }
            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }
            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }    
        }

        public OfferAddResponse OfferAddRequest(Instrument instrument, int count, TradeDirection direction, double price, bool isAnonymous = true, TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            { 
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }
                
                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid};{(int)instrument}",
                        null),
                    ["Offer"] = new JObject()
                    {
                        ["ID"] = (int) instrument,
                        ["Count"] = count,
                        ["IsAnonymous"] = isAnonymous,
                        ["IsBid"] = direction == TradeDirection.Buy ? true : false,
                        ["Price"] = price
                    }
                };

                var json = JsonConvert.SerializeObject(requestObj);
                
                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
               
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "OfferAdd"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);
                
                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return OfferAddRequest(instrument, count, direction, price, isAnonymous, timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }
                
                var xml = _parser.Parse(response.Content);

                var content = xml.QuerySelector("OfferAddResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return OfferAddRequest(instrument, count, direction, price, isAnonymous, timeout, --maxAttempts);
                }

                if (responseCode == 0)
                { 
                    var code = jResponse["value"]["Code"].Value<int>();

                    if (code != 0)
                    {
                        return new OfferAddResponse() {Success = false, OfferId = -1, Code = code};
                    }
                    
                    var offerId = jResponse["value"]["OfferID"].Value<long>();
                   
                    return new OfferAddResponse() {Success = true, OfferId = offerId, Code = code};
                }
                else
                {
                    throw new RequestException(responseCode);
                }
            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }
            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }
        }

        /// <summary>
        /// Позволяет получить предложения по указанному инструменту, не учитывая свои предложения
        /// </summary>
        /// <param name="client">Клиент для доступа к бирже без авторизации</param>
        /// <param name="instrument">Инструмент для торговли</param>
        /// <param name="direction">Направление торговли Buy/Sell</param>
        /// <param name="fullQueue">Получить весь стакан (больше 20 записей)</param>
        /// <returns></returns>
        public List<IndxSiteClient.Offer> GetOffers(IndxSiteClient client, Instrument instrument, TradeDirection? direction = null, bool fullQueue = false)
        {
            var myOffers = this.OfferMyRequest(instrument, DateTime.Now - TimeSpan.FromDays(30),
                DateTime.Now + TimeSpan.FromDays(300));
            
            var offers = client.GetOffers(instrument, direction, fullQueue);
            
            offers.RemoveAll(x => myOffers.Data.Any(c => c.OfferId == x.Id));

            return offers;
        }
        
        public BalanceResponse GetBalance(TimeSpan? timeout = null, int maxAttempts = 3)
        {
            int oldTimeout = _restClient.Timeout;

            try
            {
                if (timeout != null)
                {
                    _restClient.Timeout = (int) timeout.Value.TotalMilliseconds;
                }

                var requestObj = new JObject
                {
                    ["Login"] = _credentials.TraderLogin,
                    ["Password"] = _credentials.TraderPassword,
                    ["Wmid"] = _credentials.Wmid,
                    ["Culture"] = _credentials.Culture,
                    ["Signature"] =
                    Crypto.HashToBase64(
                        $"{_credentials.TraderLogin};{_credentials.TraderPassword};{_credentials.Culture};{_credentials.Wmid}",
                        null)
                };

                var json = JsonConvert.SerializeObject(requestObj);

                var req = new RestRequest("api/v1/tradejson.asmx", Method.POST);
                req.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
               
                req.AddParameter("text/xml", string.Format(_soapTemplate, json, "Balance"), ParameterType.RequestBody);

                var response = _restClient.Execute(req);

                if (!response.IsSuccessful)
                {
                    if(maxAttempts > 0)
                        return GetBalance(timeout, --maxAttempts);
                    else
                    {
                        throw new RequestException(-1001);
                    }
                }

                var xml = _parser.Parse(response.Content);

                var content = xml.QuerySelector("BalanceResult").TextContent;

                var jResponse = JObject.Parse(content);

                int responseCode = jResponse["code"].Value<int>();

                if (responseCode == -666)
                {
                    if(maxAttempts > 0) 
                        return GetBalance(timeout, --maxAttempts);
                }
                    
                if (responseCode == 0)
                {
                    double price = jResponse["value"]["Balance"]["Price"].Value<double>();
                    double wmz = jResponse["value"]["Balance"]["Wmz"].Value<double>();

                    var portfolios = new List<Portfolio>();

                    if (jResponse["value"]["Portfolio"] != null)
                    {
                        foreach (var jToken in jResponse["value"]["Portfolio"].Value<JArray>())
                        {
                            var el = (JObject) jToken;
                            
                            portfolios.Add(new Portfolio() { Name = el["Name"].Value<string>(), AveragePrice = el["AveragePrice"].Value<double>(), Count = el["Count"].Value<int>()});
                        }
                    }
                    
                    return new BalanceResponse() {Price = price, Wmz = wmz, Portfolios = portfolios, Nickname = jResponse["value"]["Nickname"].Value<string>()};
                }
                else
                {
                    throw new RequestException(responseCode);
                }

            }
            catch (Exception e)
            {
                if (e is RequestException)
                {
                    throw;
                }
                else
                {
                    Console.WriteLine("Error: " + e.Message + " " + e.StackTrace);
                    throw new RequestException(-1000);
                }

            }
            finally
            {
                _restClient.Timeout = oldTimeout;
            }
        }
      
    }
}