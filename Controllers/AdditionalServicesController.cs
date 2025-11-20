using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Data;

namespace CarRentalSystem.Controllers
{
    public class AdditionalServicesController : Controller
    {
        private readonly CarRentalDbContext _context;

        public AdditionalServicesController(CarRentalDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}

