using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Web.Controllers
{
    // Optional: restrict to Admin, remove [Authorize] if not needed
    [Authorize(Roles = "Admin")]
    public class DoctorsController : Controller
    {
        public IActionResult Index() => View();
    }
}
