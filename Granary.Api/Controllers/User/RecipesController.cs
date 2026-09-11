using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.User
{
    public class RecipesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
