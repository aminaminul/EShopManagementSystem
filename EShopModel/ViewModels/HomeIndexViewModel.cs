using EShopModel.Entities;
using System.Collections.Generic;

namespace EShopModel.ViewModels
{
    public class HomeIndexViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<ReviewViewModel> Reviews { get; set; } = new List<ReviewViewModel>();
        public FooterInfo? FooterInfo { get; set; }
        public IEnumerable<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
        public List<string> Banners { get; set; } = new List<string>();
    }

    public class ReviewViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
