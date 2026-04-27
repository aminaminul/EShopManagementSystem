using EShopRepository.Data;
using EShopModel.Entities;
using EShopModel.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EShopWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHomeService _homeService;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<Users> _userManager;

        public HomeController(ILogger<HomeController> logger, IHomeService homeService, IWebHostEnvironment env, UserManager<Users> userManager)
        {
            _logger = logger;
            _homeService = homeService;
            _env = env;
            _userManager = userManager;
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _homeService.GetProductDetailsAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await _homeService.GetHomeDataAsync(_env.WebRootPath);
            return View(viewModel);
        }

        public async Task<IActionResult> Search(string query)
        {
            var vm = await _homeService.GetCategoryProductsAsync(null, null, null, null, query);
            return View("CategoryProducts", vm);
        }

        public async Task<IActionResult> CategoryProducts(int? categoryId, int? typeId, string? size, string? priceRange, string? query)
        {
            var vm = await _homeService.GetCategoryProductsAsync(categoryId, typeId, size, priceRange, query);
            return View("CategoryProducts", vm);
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> SubmitReview(int productId, int rating, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _homeService.SubmitReviewAsync(user.Id, productId, rating, comment);

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
            TempData["Message"] = "Your message has been sent successfully! We will contact you soon.";
            return RedirectToAction("Contact");
        }
    }
}

