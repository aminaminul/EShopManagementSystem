using E_ShoppingManagement.Data;
using E_ShoppingManagement.Models;
using E_ShoppingManagement.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace E_ShoppingManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<Users> _userManager;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IWebHostEnvironment env, UserManager<Users> userManager)
        {
            _logger = logger;
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeIndexViewModel();

            // Get Banner Images
            string bannerPath = Path.Combine(_env.WebRootPath, "images", "banners");
            if (Directory.Exists(bannerPath))
            {
                viewModel.Banners = Directory.GetFiles(bannerPath)
                                        .Select(f => "/images/banners/" + Path.GetFileName(f))
                                        .ToList();
            }

            viewModel.Products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .Where(p => p.Status == "Approved" || p.Status == "Pending" || p.Status == "Active")
                .ToListAsync();

            viewModel.FooterInfo = await _context.FooterInfos.FirstOrDefaultAsync();
            viewModel.PaymentMethods = await _context.PaymentMethods.Where(pm => pm.IsActive).ToListAsync();

            // Fetch recent reviews with customer details
            var recentReviews = await _context.Reviews
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .ToListAsync();

            var reviewViewModels = new List<ReviewViewModel>();
            foreach (var r in recentReviews)
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == r.UserId);
                
                reviewViewModels.Add(new ReviewViewModel
                {
                    CustomerName = customer?.Name ?? "Anonymous",
                    ProfilePicture = customer?.ProfilePictureUrl,
                    Rating = r.Rating,
                    Comment = r.Comment
                });
            }
            viewModel.Reviews = reviewViewModels;

            return View(viewModel);
        }

        public async Task<IActionResult> Search(string query)
        {
            return await CategoryProducts(null, null, null, null, query);
        }

        public async Task<IActionResult> CategoryProducts(int? categoryId, int? typeId, string? size, string? priceRange, string? query)
        {
            var productsQuery = _context.Products
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

            var vm = new CategoryProductsViewModel
            {
                Products = await productsQuery.OrderByDescending(p => p.CreatedAt).ToListAsync(),
                Categories = await _context.Categories.ToListAsync(),
                ProductTypes = await _context.ProductTypes.ToListAsync(),
                SelectedCategory = categoryId,
                SelectedType = typeId,
                SelectedSize = size,
                PriceRange = priceRange,
                SearchQuery = query
            };

            return View("CategoryProducts", vm);
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> SubmitReview(int productId, int rating, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!User.IsInRole("Customer"))
            {
                TempData["Error"] = "Only customers can submit reviews.";
                return RedirectToAction("Details", new { id = productId });
            }

            var review = new Review
            {
                ProductId = productId,
                UserId = user.Id,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow,
                Status = "Active"
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Refresh product average rating
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                var allReviews = await _context.Reviews.Where(r => r.ProductId == productId).ToListAsync();
                product.AverageRating = allReviews.Average(r => r.Rating);
                await _context.SaveChangesAsync();
            }

            TempData["Message"] = "Thank you for your review!";
            return RedirectToAction("Details", new { id = productId });
        }


        public IActionResult Privacy() => View();
        public IActionResult HelpCenter() => View();
        public IActionResult Returns() => View();
        public IActionResult TrackOrder() => View();
        public IActionResult Contact() => View();

        [HttpPost]
        public IActionResult SendMessage(string name, string email, string subject, string message)
        {
            // Here you would implement email sending logic.
            // For now, we simulate success.
            TempData["Message"] = "Your message has been sent successfully! We will contact you soon.";
            return RedirectToAction("Contact");
        }
    }
}

