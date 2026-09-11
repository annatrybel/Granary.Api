using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Services
{
    public class RecipeService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
