using Microsoft.AspNetCore.Mvc;
namespace Clinic.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
    }
}