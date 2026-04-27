using EShopModel.Entities;
using EShopModel.ViewModels;
using EShopRepository.Interfaces;
using EShopService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EShopService.Services
{
    public class HomeService : IHomeService
    {
        private readonly IUnitOfWork _unitOfWork;

        public HomeService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<HomeIndexViewModel> GetHomeDataAsync(string webRootPath)
        {
            var viewModel = new HomeIndexViewModel();

            // Banner logic
            string bannerPath = Path.Combine(webRootPath, "images", "banners");
            if (Directory.Exists(bannerPath))
            {
                viewModel.Banners = Directory.GetFiles(bannerPath)
                                        .Select(f => "/images/banners/" + Path.GetFileName(f))
                                        .ToList();
            }

            viewModel.Products = await _unitOfWork.Repository<Product>().Query()
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .Where(p => p.Status == "Approved" || p.Status == "Pending" || p.Status == "Active")
                .ToListAsync();

            viewModel.FooterInfo = await _unitOfWork.Repository<FooterInfo>().Query().FirstOrDefaultAsync();
            viewModel.PaymentMethods = await _unitOfWork.Repository<PaymentMethod>().Query().Where(pm => pm.IsActive).ToListAsync();

            var recentReviews = await _unitOfWork.Repository<Review>().Query()
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .ToListAsync();

            var reviewViewModels = new List<ReviewViewModel>();
            foreach (var r in recentReviews)
            {
                var customer = await _unitOfWork.Repository<Customer>().Query().FirstOrDefaultAsync(c => c.UserId == r.UserId);
                reviewViewModels.Add(new ReviewViewModel
                {
                    CustomerName = customer?.Name ?? "Anonymous",
                    ProfilePicture = customer?.ProfilePictureUrl,
                    Rating = r.Rating,
                    Comment = r.Comment
                });
            }
            viewModel.Reviews = reviewViewModels;

            return viewModel;
        }

        public async Task<Product?> GetProductDetailsAsync(int id)
        {
            return await _unitOfWork.Repository<Product>().Query()
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<CategoryProductsViewModel> GetCategoryProductsAsync(int? categoryId, int? typeId, string? size, string? priceRange, string? query)
        {
            var productsQuery = _unitOfWork.Repository<Product>().Query()
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .Where(p => p.Status == "Active")
                .AsQueryable();

            if (categoryId.HasValue) productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
            if (typeId.HasValue) productsQuery = productsQuery.Where(p => p.ProductTypeId == typeId);
            if (!string.IsNullOrEmpty(size)) productsQuery = productsQuery.Where(p => p.AvailableSizes != null && p.AvailableSizes.Contains(size));
            if (!string.IsNullOrEmpty(query))
            {
                productsQuery = productsQuery.Where(p => p.Name.Contains(query) || p.Description.Contains(query));
            }

            if (!string.IsNullOrEmpty(priceRange))
            {
                switch (priceRange)
                {
                    case "low": productsQuery = productsQuery.Where(p => p.Price < 500); break;
                    case "mid": productsQuery = productsQuery.Where(p => p.Price >= 500 && p.Price <= 2000); break;
                    case "high": productsQuery = productsQuery.Where(p => p.Price > 2000); break;
                    case "low-mid": productsQuery = productsQuery.Where(p => p.Price <= 1500); break;
                    case "mid-high": productsQuery = productsQuery.Where(p => p.Price >= 1200); break;
                }
            }

            return new CategoryProductsViewModel
            {
                Products = await productsQuery.OrderByDescending(p => p.CreatedAt).ToListAsync(),
                Categories = (await _unitOfWork.Repository<Category>().GetAllAsync()).ToList(),
                ProductTypes = (await _unitOfWork.Repository<ProductType>().GetAllAsync()).ToList(),
                SelectedCategory = categoryId,
                SelectedType = typeId,
                SelectedSize = size,
                PriceRange = priceRange,
                SearchQuery = query
            };
        }

        public async Task<bool> SubmitReviewAsync(string userId, int productId, int rating, string comment)
        {
            var review = new Review
            {
                ProductId = productId,
                UserId = userId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow,
                Status = "Active"
            };

            await _unitOfWork.Repository<Review>().AddAsync(review);
            await _unitOfWork.CompleteAsync();

            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId);
            if (product != null)
            {
                var allReviews = await _unitOfWork.Repository<Review>().FindAsync(r => r.ProductId == productId);
                if (allReviews.Any())
                {
                    product.AverageRating = allReviews.Average(r => r.Rating);
                    _unitOfWork.Repository<Product>().Update(product);
                    await _unitOfWork.CompleteAsync();
                }
            }

            return true;
        }
    }
}
