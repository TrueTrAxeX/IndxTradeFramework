using System;

namespace IndxTradeFramework.TradeApi
{
    public class RequestException : Exception
    {
        public int Code { get; set; }
            
        public RequestException(int code)
        {
            Code = code;
            Message = "Возникла ошибка с кодом: " + code;
        }
            
        public override string Message { get; }
    }
}