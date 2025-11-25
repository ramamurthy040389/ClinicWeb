using Clinic.Web.Data;
using Clinic.Web.Models.DTOs;
using Clinic.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Clinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly ClinicContext _db;
        private readonly IAppointmentService _svc;
        public AppointmentsController(ClinicContext db, IAppointmentService svc)
        {
            _db = db;
            _svc = svc;
        }

        // GET /api/appointments
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AppointmentQueryParameters q)
        {
            if (q.Page <= 0) q.Page = 1;
            if (q.PageSize <= 0) q.PageSize = 20;
            if (q.PageSize > 200) q.PageSize = 200;

            var baseQuery = _db.Appointments.Include(a => a.Doctor).Include(a => a.Patient).AsQueryable();

            if (q.DoctorId.HasValue)
                baseQuery = baseQuery.Where(a => a.DoctorId == q.DoctorId.Value);

            if (!string.IsNullOrWhiteSpace(q.PatientName))
            {
                var name = q.PatientName.Trim().ToLower();
                baseQuery = baseQuery.Where(a => a.Patient.Name.ToLower().Contains(name));
            }

            if (q.DateFrom.HasValue)
                baseQuery = baseQuery.Where(a => a.StartTime.Date >= q.DateFrom.Value.Date);

            if (q.DateTo.HasValue)
                baseQuery = baseQuery.Where(a => a.StartTime.Date <= q.DateTo.Value.Date);

            var sortBy = (q.SortBy ?? "StartTime").ToLower();
            var sortDir = (q.SortDir ?? "asc").ToLower();

            baseQuery = (sortBy, sortDir) switch
            {
                ("starttime", "asc") => baseQuery.OrderBy(a => a.StartTime),
                ("starttime", "desc") => baseQuery.OrderByDescending(a => a.StartTime),
                ("doctor", "asc") => baseQuery.OrderBy(a => a.Doctor.Name),
                ("doctor", "desc") => baseQuery.OrderByDescending(a => a.Doctor.Name),
                ("patient", "asc") => baseQuery.OrderBy(a => a.Patient.Name),
                ("patient", "desc") => baseQuery.OrderByDescending(a => a.Patient.Name),
                _ => baseQuery.OrderBy(a => a.StartTime)
            };

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(a => new {
                    a.Id,
                    StartTime = a.StartTime,
                    a.DurationInMinutes,
                    Doctor = a.Doctor.Name,
                    Patient = a.Patient.Name,
                    FileNo = a.Patient.FileNo
                }).ToListAsync();

            var paged = new PagedResult<object>
            {
                Items = items,
                TotalCount = totalCount,
                Page = q.Page,
                PageSize = q.PageSize
            };

            return Ok(paged);
        }

        // ------------------------------------------------------------------
        // POST with validation: phone-only-digits, date must be future
        // ------------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Book([FromBody] BookingRequest req)
        {
            if (req == null)
                return BadRequest("Invalid request.");

            // ========== VALIDATE PHONE NUMBER ==========
            if (req.Patient == null || string.IsNullOrWhiteSpace(req.Patient.Phone))
                return BadRequest("Phone number is required.");

            var phoneDigitsOnly = new Regex(@"^\d+$");

            if (!phoneDigitsOnly.IsMatch(req.Patient.Phone))
                return BadRequest("Phone number must contain digits only (0-9).");

            // ========== VALIDATE START TIME IS IN FUTURE ==========
            if (string.IsNullOrWhiteSpace(req.StartTime))
                return BadRequest("Start time is required.");

            DateTime parsedStart;
            try
            {
                parsedStart = DateTime.Parse(req.StartTime); // adjust parsing as needed
            }
            catch
            {
                return BadRequest("Invalid start time format.");
            }

            var now = DateTime.Now;

            if (parsedStart <= now)
                return BadRequest("Selected appointment time must be in the future.");

            // ========== CALL SERVICE VALIDATION ==========
            var res = await _svc.BookAsync(req);

            if (!res.Success)
                return BadRequest(res.Message);

            return CreatedAtAction(nameof(GetAll), new { id = res.AppointmentId }, new { appointmentId = res.AppointmentId });
        }

        // DELETE
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
