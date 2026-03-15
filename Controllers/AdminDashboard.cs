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
    [Authorize(Roles = "Admin,Employee")]
    public class AdminDashboard : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public AdminDashboard(IWebHostEnvironment env, AppDbContext context, UserManager<Users> userManager)
        {
            _env = env;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        public async Task<IActionResult> Index()
        {
            var stats = new AdminStatsViewModel();

            var orders = await _context.Orders.Include(o => o.Customer).ToListAsync();
            stats.TotalOrders = orders.Count;
            stats.TotalDelivered = orders.Count(o => o.OrderStatus == "Delivered");
            stats.TotalPending = orders.Count(o => o.OrderStatus == "Pending");
            stats.TotalProcessing = orders.Count(o => o.OrderStatus == "Processing");
            stats.TotalShipped = orders.Count(o => o.OrderStatus == "Shipped");
            stats.TotalCancelled = orders.Count(o => o.OrderStatus == "Cancelled");
            stats.TotalRevenue = orders.Where(o => o.OrderStatus == "Delivered").Sum(o => o.TotalAmount);

            stats.PaidOrders = orders.Count(o => o.PaymentStatus == "Paid");
            stats.PendingPaymentOrders = orders.Count(o => o.PaymentStatus == "Pending");

            stats.RecentOrders = orders.OrderByDescending(o => o.CreatedAt).Take(10).Select(o => new RecentOrderViewModel
            {
                OrderId = o.Id,
                CustomerName = o.Customer?.Name ?? "Unknown",
                OrderDate = o.CreatedAt,
                Status = o.OrderStatus,
                Amount = o.TotalAmount,
                PaymentStatus = o.PaymentStatus,
                PaymentMethod = o.PaymentMethod,
                ShippingAddress = $"{o.ShippingAddress}, {o.City}"
            }).ToList();

            // Employee Performance
            var employees = await _context.Employees.ToListAsync();
            foreach (var emp in employees)
            {
                var productsManaged = await _context.Products.Where(p => p.CreatedBy == emp.Email || p.CreatedBy == emp.Name).ToListAsync();
                
                // For sales, we need to check orders assigned to this employee
                var ordersAssigned = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .Where(o => o.AssignedEmployeeId == emp.Id && o.OrderStatus == "Delivered")
                    .ToListAsync();

                stats.EmployeePerformances.Add(new EmployeePerformanceViewModel
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    TotalProductsManaged = productsManaged.Count,
                    TotalStockValue = productsManaged.Sum(p => p.Price * p.StockQty),
                    ProductsSold = ordersAssigned.SelectMany(o => o.OrderDetails ?? new List<OrderDetails>()).Sum(od => od.Quantity),
                    TotalSalesValue = ordersAssigned.Sum(o => o.TotalAmount),
                    ManagedProducts = productsManaged // Added for details
                });
            }

            return View(stats);
        }

        public async Task<IActionResult> OrdersByStatus(string status)
        {
            ViewBag.Status = status;
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.AssignedEmployee)
                .Include(o => o.DeliveryMan)
                .Include(o => o.OrderDetails!)
                    .ThenInclude(od => od.Product)
                .Where(o => o.OrderStatus == status)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> EmployeeProducts(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .Where(p => p.CreatedBy == employee.Email && (p.Status == "Approved" || p.Status == "Pending" || p.Status == "Active")) // Be more lenient for now
                .ToListAsync();

            ViewBag.EmployeeName = employee.Name;
            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalValue = products.Sum(p => p.Price * p.StockQty);

            return View(products);
        }

        public async Task<IActionResult> EmployeeSalesDetails(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var sales = await _context.Orders
                .Where(o => o.AssignedEmployeeId == employeeId && o.OrderStatus == "Delivered")
                .Include(o => o.OrderDetails!)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.EmployeeName = employee.Name;
            return View(sales);
        }

        public async Task<IActionResult> EmployeeDashboard(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var products = await _context.Products.Where(p => p.CreatedBy == employee.Name).ToListAsync();
            var sales = await _context.Orders
                .Where(o => o.AssignedEmployeeId == employeeId && o.OrderStatus == "Delivered")
                .Include(o => o.OrderDetails)
                .ToListAsync();

            var returns = await _context.Orders
                .Where(o => o.AssignedEmployeeId == employeeId && o.OrderStatus == "Returned") 
                .CountAsync();

            ViewBag.EmployeeName = employee.Name;
            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalProductValue = products.Sum(p => p.Price * p.StockQty);
            ViewBag.TotalSalesCount = sales.Count;
            ViewBag.TotalRevenue = sales.Sum(s => s.TotalAmount);
            ViewBag.ReturnsCount = returns;

            return View(sales);
        }

        public IActionResult ManageBanners()
        {
            string bannerPath = Path.Combine(_env.WebRootPath, "images", "banners");
            if (!Directory.Exists(bannerPath))
            {
                Directory.CreateDirectory(bannerPath);
            }

            var banners = Directory.GetFiles(bannerPath)
                                   .Select(f => Path.GetFileName(f))
                                   .ToList();

            return View(banners);
        }

        [HttpPost]
        public async Task<IActionResult> UploadBanner(IFormFile bannerImage)
        {
            if (bannerImage != null && bannerImage.Length > 0)
            {
                string bannerPath = Path.Combine(_env.WebRootPath, "images", "banners");
                if (!Directory.Exists(bannerPath))
                {
                    Directory.CreateDirectory(bannerPath);
                }

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(bannerImage.FileName);
                string fullPath = Path.Combine(bannerPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await bannerImage.CopyToAsync(stream);
                }
            }
            return RedirectToAction("ManageBanners");
        }

        [HttpPost]
        public IActionResult DeleteBanner(string fileName)
        {
            string fullPath = Path.Combine(_env.WebRootPath, "images", "banners", fileName);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
            return RedirectToAction("ManageBanners");
        }

        // SETTINGS: Footer & Payment
        public async Task<IActionResult> FooterSettings()
        {
            var footer = await _context.FooterInfos.FirstOrDefaultAsync();
            if (footer == null)
            {
                footer = new FooterInfo { Address = "Update Address", ContactNumber = "000", LastUpdated = DateTime.UtcNow };
                _context.FooterInfos.Add(footer);
                await _context.SaveChangesAsync();
            }
            return View(footer);
        }

        [HttpPost]
        public async Task<IActionResult> FooterSettings(FooterInfo model)
        {
            var footer = await _context.FooterInfos.FirstOrDefaultAsync();
            if (footer != null)
            {
                footer.Address = model.Address;
                footer.ContactNumber = model.ContactNumber;
                footer.Email = model.Email;
                footer.FacebookUrl = model.FacebookUrl;
                footer.InstagramUrl = model.InstagramUrl;
                footer.TwitterUrl = model.TwitterUrl;
                footer.LastUpdated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(FooterSettings));
        }

        public async Task<IActionResult> PaymentMethods()
        {
            var methods = await _context.PaymentMethods.ToListAsync();
            return View(methods);
        }

        [HttpPost]
        public async Task<IActionResult> AddPaymentMethod(PaymentMethod method, IFormFile? logoFile)
        {
            if (logoFile != null && logoFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "logo");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(logoFile.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }
                method.LogoUrl = "/images/logo/" + fileName;
            }

            method.CreatedAt = DateTime.UtcNow;
            method.Status = "Active";
            _context.PaymentMethods.Add(method);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PaymentMethods));
        }
    }
}
