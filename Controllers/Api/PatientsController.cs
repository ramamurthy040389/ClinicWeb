using Clinic.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly ClinicContext _db;
        public PatientsController(ClinicContext db) { _db = db; }

        [HttpGet("{fileNo}")]
        public async Task<IActionResult> GetByFileNo(string fileNo)
        {
            var p = await _db.Patients.Where(x => x.FileNo == fileNo)
                .Select(x => new { x.Id, x.Name, x.Phone, x.FileNo }).FirstOrDefaultAsync();
            if (p == null) return NotFound();
            return Ok(p);
        }
    }
}
