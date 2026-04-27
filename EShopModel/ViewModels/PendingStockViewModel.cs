namespace EShopModel.ViewModels
{
    public class PendingStockViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int StockQty { get; set; }
        public int PendingOrderQty { get; set; }
    }
}
