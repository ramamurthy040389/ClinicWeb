using Clinic.Web.Data;
using Clinic.Web.Models;
using Clinic.Web.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Clinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DoctorsController : ControllerBase
    {
        private readonly ClinicContext _db;
        public DoctorsController(ClinicContext db) { _db = db; }

        // GET /api/doctors (public for booking, but CRUD operations require admin)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Doctors
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name, d.Specialization })
                .ToListAsync();

            return Ok(list);
        }

        // GET /api/doctors/admin?page=1&pageSize=10&search=cardio
        [HttpGet("admin")]
        public async Task<IActionResult> GetPaged([FromQuery] DoctorQueryParameters q)
        {
            if (q.Page <= 0) q.Page = 1;
            if (q.PageSize <= 0) q.PageSize = 10;
            if (q.PageSize > 100) q.PageSize = 100;

            var baseQuery = _db.Doctors.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var search = q.Search.Trim().ToLower();
                baseQuery = baseQuery.Where(d =>
                    d.Name.ToLower().Contains(search) ||
                    d.Specialization.ToLower().Contains(search));
            }

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(d => d.Name)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(d => new { d.Id, d.Name, d.Specialization })
                .ToListAsync();

            var paged = new PagedResult<object>
            {
                Items = items,
                TotalCount = totalCount,
                Page = q.Page,
                PageSize = q.PageSize
            };

            return Ok(paged);
        }

        // GET /api/doctors/{id}
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var d = await _db.Doctors
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.Name, x.Specialization })
                .FirstOrDefaultAsync();

            if (d == null) return NotFound();
            return Ok(d);
        }

        // POST /api/doctors
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Doctor model)
        {
            if (model == null) return BadRequest("Doctor is required.");

            // Validate model
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest("Doctor name is required.");
            if (model.Name.Length < 2 || model.Name.Length > 120)
                return BadRequest("Doctor name must be between 2 and 120 characters.");

            if (string.IsNullOrWhiteSpace(model.Specialization))
                return BadRequest("Specialization is required.");
            if (model.Specialization.Length < 2 || model.Specialization.Length > 80)
                return BadRequest("Specialization must be between 2 and 80 characters.");

            // Check for duplicate name
            var exists = await _db.Doctors.AnyAsync(d => d.Name.Trim().ToLower() == model.Name.Trim().ToLower());
            if (exists)
                return BadRequest("A doctor with this name already exists.");

            model.Name = model.Name.Trim();
            model.Specialization = model.Specialization.Trim();

            _db.Doctors.Add(model);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = model.Id }, new { model.Id });
        }

        // PUT /api/doctors/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Doctor model)
        {
            if (model == null) return BadRequest("Doctor is required.");

            var d = await _db.Doctors.FindAsync(id);
            if (d == null) return NotFound();

            // Validate model
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest("Doctor name is required.");
            if (model.Name.Length < 2 || model.Name.Length > 120)
                return BadRequest("Doctor name must be between 2 and 120 characters.");

            if (string.IsNullOrWhiteSpace(model.Specialization))
                return BadRequest("Specialization is required.");
            if (model.Specialization.Length < 2 || model.Specialization.Length > 80)
                return BadRequest("Specialization must be between 2 and 80 characters.");

            // Check for duplicate name (excluding current doctor)
            var exists = await _db.Doctors.AnyAsync(doc => doc.Id != id && doc.Name.Trim().ToLower() == model.Name.Trim().ToLower());
            if (exists)
                return BadRequest("A doctor with this name already exists.");

            d.Name = model.Name.Trim();
            d.Specialization = model.Specialization.Trim();

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/doctors/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var d = await _db.Doctors.FindAsync(id);
            if (d == null) return NotFound();

            var hasAppts = await _db.Appointments.AnyAsync(a => a.DoctorId == id);
            if (hasAppts) return BadRequest("Doctor has appointments. Remove appointments first.");

            _db.Doctors.Remove(d);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ----------------------------------------------------------
        // GET /api/doctors/{id}/availabletimes?date=2025-11-26 (public for booking)
        // ----------------------------------------------------------
        [HttpGet("{id:int}/availabletimes")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableTimes(
            int id,
            [FromQuery] string date,
            [FromQuery] int slotMinutes = 30,
            [FromQuery] string workStart = "09:00",
            [FromQuery] string workEnd = "17:00")
        {
            // validate date
            if (string.IsNullOrWhiteSpace(date) ||
                !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var targetDate))
            {
                return BadRequest("Date must be in yyyy-MM-dd format.");
            }

            // validate doctor
            var doctor = await _db.Doctors.FindAsync(id);
            if (doctor == null) return NotFound("Doctor not found.");

            // working hours
            if (!TimeSpan.TryParseExact(workStart, "hh\\:mm", CultureInfo.InvariantCulture, out var startTs))
                return BadRequest("workStart must be HH:mm format");

            if (!TimeSpan.TryParseExact(workEnd, "hh\\:mm", CultureInfo.InvariantCulture, out var endTs))
                return BadRequest("workEnd must be HH:mm format");

            if (endTs <= startTs) return BadRequest("workEnd must be > workStart");

            if (slotMinutes <= 0 || slotMinutes > 240)
                return BadRequest("slotMinutes must be 1�240");

            var dayStart = targetDate.Date + startTs;
            var dayEnd = targetDate.Date + endTs;
            var slotSpan = TimeSpan.FromMinutes(slotMinutes);

            // load doctor's appointments on that day
            var appts = await _db.Appointments
                .Where(a => a.DoctorId == id && a.StartTime.Date == targetDate.Date)
                .Select(a => new
                {
                    a.StartTime,
                    EndTime = a.StartTime.AddMinutes(a.DurationInMinutes)
                })
                .ToListAsync();

            // overlap check
            static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
                => aStart < bEnd && bStart < aEnd;

            var available = new List<object>();

            // generate slots
            for (var slotStart = dayStart; slotStart + slotSpan <= dayEnd; slotStart += slotSpan)
            {
                var slotEnd = slotStart + slotSpan;

                bool conflict = appts.Any(a => Overlaps(slotStart, slotEnd, a.StartTime, a.EndTime));
                if (!conflict)
                {
                    available.Add(new
                    {
                        iso = slotStart.ToString("o"),
                        time = slotStart.ToString("HH:mm")
                    });
                }
            }

            return Ok(new
            {
                DoctorId = id,
                Date = targetDate.ToString("yyyy-MM-dd"),
                SlotMinutes = slotMinutes,
                WorkStart = startTs.ToString(@"hh\:mm"),
                WorkEnd = endTs.ToString(@"hh\:mm"),
                AvailableSlots = available
            });
        }
    }
}
