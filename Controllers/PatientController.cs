using Microsoft.AspNetCore.Mvc;
namespace Clinic.Web.Controllers
{
    public class PatientController : Controller
    {
        public IActionResult Search() => View();
    }
}
