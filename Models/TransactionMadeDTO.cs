namespace Coinbot.Binance.Models
{
    public class TransactionMadeDTO
    {
        public string symbol { get; set; }
        public int orderId { get; set; }
        public string clientOrderId { get; set; }
        public long transactTime { get; set; }
        public decimal price { get; set; }
        public decimal origQty { get; set; }
        public decimal executedQty { get; set; }
        public decimal cummulativeQuoteQty { get; set; }
        public string status { get; set; }
        public string timeInForce { get; set; }
        public string type { get; set; }
        public string side { get; set; }
    }
}