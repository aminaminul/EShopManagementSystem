using EShopModel.Entities;
using EShopModel.ViewModels;

namespace EShopService.Interfaces
{
    public interface IHomeService
    {
        Task<HomeIndexViewModel> GetHomeDataAsync(string webRootPath);
        Task<Product?> GetProductDetailsAsync(int id);
        Task<CategoryProductsViewModel> GetCategoryProductsAsync(int? categoryId, int? typeId, string? size, string? priceRange, string? query);
        Task<bool> SubmitReviewAsync(string userId, int productId, int rating, string comment);
    }
}
