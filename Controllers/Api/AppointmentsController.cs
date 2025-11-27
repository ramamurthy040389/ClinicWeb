using Clinic.Web.Data;
using Clinic.Web.Models;
using Clinic.Web.Models.DTOs;
using Clinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Clinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
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
                .Select(a => new
                {
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
        // Public endpoint for booking (no admin required)
        // ------------------------------------------------------------------
        [HttpPost]
        [AllowAnonymous]
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

        // GET /api/appointments/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var appt = await _db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.DoctorId,
                    DoctorName = a.Doctor.Name,
                    a.PatientId,
                    PatientName = a.Patient.Name,
                    PatientFileNo = a.Patient.FileNo,
                    PatientPhone = a.Patient.Phone,
                    PatientAddress = a.Patient.Address,
                    PatientDateOfBirth = a.Patient.DateOfBirth,
                    PatientGender = a.Patient.Gender,
                    StartTime = a.StartTime,
                    a.DurationInMinutes
                })
                .FirstOrDefaultAsync();

            if (appt == null) return NotFound();
            return Ok(appt);
        }

        // PUT /api/appointments/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AppointmentUpdateRequest req)
        {
            if (req == null) return BadRequest("Request is required.");

            var appt = await _db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appt == null) return NotFound("Appointment not found.");

            // Validate doctor exists
            if (req.DoctorId.HasValue && req.DoctorId.Value != appt.DoctorId)
            {
                var doctor = await _db.Doctors.FindAsync(req.DoctorId.Value);
                if (doctor == null) return BadRequest("Doctor not found.");
                appt.DoctorId = req.DoctorId.Value;
            }

            // Validate start time
            if (req.StartTime.HasValue)
            {
                var newStart = req.StartTime.Value;

                // Check for overlaps (excluding current appointment)
                var requestedEnd = newStart.AddMinutes(req.DurationInMinutes ?? appt.DurationInMinutes);
                var overlap = await _db.Appointments.AnyAsync(a =>
                    a.Id != id &&
                    a.DoctorId == (req.DoctorId ?? appt.DoctorId) &&
                    a.StartTime < requestedEnd &&
                    a.StartTime.AddMinutes(a.DurationInMinutes) > newStart);

                if (overlap)
                    return BadRequest("Requested time overlaps an existing appointment.");

                appt.StartTime = newStart;
            }

            // Validate duration
            if (req.DurationInMinutes.HasValue)
            {
                if (req.DurationInMinutes.Value <= 0)
                    return BadRequest("Duration must be greater than zero.");
                appt.DurationInMinutes = req.DurationInMinutes.Value;
            }

            if (req.Patient != null)
            {
                var existing = appt.Patient;

                string resolvedName = string.IsNullOrWhiteSpace(req.Patient.Name)
                    ? existing.Name
                    : req.Patient.Name.Trim();

                string resolvedAddress = string.IsNullOrWhiteSpace(req.Patient.Address)
                    ? existing.Address
                    : req.Patient.Address.Trim();

                string resolvedGender = string.IsNullOrWhiteSpace(req.Patient.Gender)
                    ? existing.Gender
                    : req.Patient.Gender.Trim();

                string resolvedPhone = existing.Phone;
                if (!string.IsNullOrWhiteSpace(req.Patient.Phone))
                {
                    var digits = Regex.Replace(req.Patient.Phone, @"\D", "");
                    if (string.IsNullOrWhiteSpace(digits))
                        return BadRequest("Patient phone must contain digits.");
                    resolvedPhone = digits;
                }

                DateTime resolvedDob = existing.DateOfBirth;
                if (!string.IsNullOrWhiteSpace(req.Patient.DateOfBirth))
                {
                    if (!DateTime.TryParse(req.Patient.DateOfBirth, out resolvedDob))
                        return BadRequest("Invalid date of birth.");
                    resolvedDob = resolvedDob.Date;
                }

                string? resolvedFileNo = req.Patient.FileNo switch
                {
                    null => existing.FileNo,
                    var s when string.IsNullOrWhiteSpace(s) => null,
                    var s => s.Trim()
                };

                if (!string.IsNullOrWhiteSpace(resolvedFileNo))
                {
                    var exists = await _db.Patients.AnyAsync(p => p.Id != existing.Id && p.FileNo == resolvedFileNo);
                    if (exists)
                        return BadRequest("File number already in use.");
                }

                existing.Name = resolvedName;
                existing.Address = resolvedAddress;
                existing.Gender = resolvedGender;
                existing.Phone = resolvedPhone;
                existing.DateOfBirth = resolvedDob;
                existing.FileNo = resolvedFileNo;
            }

            await _db.SaveChangesAsync();
            return NoContent();
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
