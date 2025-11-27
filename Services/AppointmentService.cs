using Clinic.Web.Data;
using Clinic.Web.Models;
using Clinic.Web.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Clinic.Web.Services
{
    // Simple result object returned by BookAsync (controller expects res.Success, res.Message, res.AppointmentId)
    public class BookingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AppointmentId { get; set; }
    }

    // Interface - NOTE: method signature must NOT be async and must end with semicolon
    public interface IAppointmentService
    {
        Task<BookingResult> BookAsync(BookingRequest req);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly ClinicContext _db;

        public AppointmentService(ClinicContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Books an appointment. Validates start time parsing, overlap, and patient handling.
        /// Returns BookingResult containing success flag, message and created appointment id (if success).
        /// </summary>
        public async Task<BookingResult> BookAsync(BookingRequest req)
        {
            var result = new BookingResult();

            if (req == null)
            {
                result.Success = false;
                result.Message = "Request is null.";
                return result;
            }

            // Basic validation
            if (req.DoctorId <= 0)
            {
                result.Success = false;
                result.Message = "Invalid doctor id.";
                return result;
            }

            if (req.DurationInMinutes <= 0)
            {
                result.Success = false;
                result.Message = "Duration must be greater than zero.";
                return result;
            }

            if (req.Patient == null || string.IsNullOrWhiteSpace(req.Patient.Name))
            {
                result.Success = false;
                result.Message = "Patient name is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(req.Patient.FileNo))
            {
                result.Success = false;
                result.Message = "Patient file number is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(req.Patient.Phone))
            {
                result.Success = false;
                result.Message = "Patient phone is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(req.Patient.Address))
            {
                result.Success = false;
                result.Message = "Patient address is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(req.Patient.DateOfBirth))
            {
                result.Success = false;
                result.Message = "Patient date of birth is required.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(req.Patient.Gender))
            {
                result.Success = false;
                result.Message = "Patient gender is required.";
                return result;
            }

            // Parse date of birth
            DateTime dateOfBirth;
            if (!DateTime.TryParse(req.Patient.DateOfBirth, null, System.Globalization.DateTimeStyles.RoundtripKind, out dateOfBirth))
            {
                if (!DateTime.TryParse(req.Patient.DateOfBirth, out dateOfBirth))
                {
                    result.Success = false;
                    result.Message = "Invalid date of birth format. Use ISO format like 1990-01-01.";
                    return result;
                }
            }

            // defensive phone normalization: keep digits only
            var phoneDigits = Regex.Replace(req.Patient.Phone, @"\D", "");
            if (string.IsNullOrWhiteSpace(phoneDigits))
            {
                result.Success = false;
                result.Message = "Patient phone must contain digits.";
                return result;
            }

            // Parse start time (client sends ISO string). Accept both local and Z/UTC.
            if (string.IsNullOrWhiteSpace(req.StartTime))
            {
                result.Success = false;
                result.Message = "StartTime is required.";
                return result;
            }

            DateTime start;
            if (!DateTime.TryParse(req.StartTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out start))
            {
                // try a more permissive parse
                if (!DateTime.TryParse(req.StartTime, out start))
                {
                    result.Success = false;
                    result.Message = "Invalid StartTime format. Use ISO format like 2025-11-26T09:00:00Z or a valid local datetime.";
                    return result;
                }
            }

            // Ensure start is in the future (use server local time comparison)
            var now = DateTime.Now;
            var startLocal = start.Kind == DateTimeKind.Utc ? start.ToLocalTime() : start;
            if (startLocal <= now)
            {
                result.Success = false;
                result.Message = "Selected appointment time must be in the future.";
                return result;
            }

            // Check doctor exists
            var doctor = await _db.Doctors.FindAsync(req.DoctorId);
            if (doctor == null)
            {
                result.Success = false;
                result.Message = "Doctor not found.";
                return result;
            }

            // compute requested end
            var requestedEnd = startLocal.AddMinutes(req.DurationInMinutes);

            // Check overlapping appointments (for the same doctor)
            // This assumes StartTime stored in DB is local or consistent with startLocal; adjust if you store UTC.
            var overlap = await _db.Appointments.AnyAsync(a =>
                a.DoctorId == req.DoctorId &&
                // appointment start < requestedEnd && appointment end > requestedStart
                a.StartTime < requestedEnd && a.StartTime.AddMinutes(a.DurationInMinutes) > startLocal
            );

            if (overlap)
            {
                result.Success = false;
                result.Message = "Requested time overlaps an existing appointment. Please choose another slot.";
                return result;
            }

            // Insert or reuse patient record.
            // Prefer matching by FileNo (required field), otherwise match by phone (digits) or create new.
            Patient patient = null;

            patient = await _db.Patients.FirstOrDefaultAsync(p => p.FileNo == req.Patient.FileNo);

            if (patient == null)
            {
                // try by phone (normalize DB phone similarly if needed)
                patient = await _db.Patients.FirstOrDefaultAsync(p => (p.Phone ?? "") == phoneDigits);
            }

            if (patient == null)
            {
                patient = new Patient
                {
                    FileNo = req.Patient.FileNo.Trim(),
                    Name = req.Patient.Name.Trim(),
                    Phone = phoneDigits,
                    Address = req.Patient.Address.Trim(),
                    DateOfBirth = dateOfBirth.Date,
                    Gender = req.Patient.Gender.Trim()
                };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }
            else
            {
                // update patient details if missing or changed
                var changed = false;
                if (string.IsNullOrWhiteSpace(patient.Name) && !string.IsNullOrWhiteSpace(req.Patient.Name))
                {
                    patient.Name = req.Patient.Name.Trim();
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(patient.Phone) && !string.IsNullOrWhiteSpace(phoneDigits))
                {
                    patient.Phone = phoneDigits;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(patient.Address) && !string.IsNullOrWhiteSpace(req.Patient.Address))
                {
                    patient.Address = req.Patient.Address.Trim();
                    changed = true;
                }
                if (patient.DateOfBirth == default(DateTime) && dateOfBirth != default(DateTime))
                {
                    patient.DateOfBirth = dateOfBirth.Date;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(patient.Gender) && !string.IsNullOrWhiteSpace(req.Patient.Gender))
                {
                    patient.Gender = req.Patient.Gender.Trim();
                    changed = true;
                }
                if (changed)
                {
                    _db.Patients.Update(patient);
                    await _db.SaveChangesAsync();
                }
            }

            // create appointment
            var appointment = new Appointment
            {
                DoctorId = req.DoctorId,
                PatientId = patient.Id,
                StartTime = startLocal,
                DurationInMinutes = req.DurationInMinutes
            };

            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            result.Success = true;
            result.Message = "Appointment booked";
            result.AppointmentId = appointment.Id;
            return result;
        }
    }
}
