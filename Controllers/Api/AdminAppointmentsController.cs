using Clinic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/admin/appointments")]
    [Authorize(Roles = "Admin")]
    public class AdminAppointmentsController : ControllerBase
    {
        private readonly ClinicContext _db;
        public AdminAppointmentsController(ClinicContext db) { _db = db; }

        // GET api/admin/appointments
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .OrderBy(a => a.StartTime)
                .Select(a => new {
                    a.Id,
                    StartTime = a.StartTime,
                    a.DurationInMinutes,
                    Doctor = a.Doctor.Name,
                    Patient = a.Patient.Name,
                    FileNo = a.Patient.FileNo
                }).ToListAsync();

            return Ok(list);
        }

        // Optionally: admin delete (keeps same logic as public delete, but protected)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound();
            _db.Appointments.Remove(appt);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
