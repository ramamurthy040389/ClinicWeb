using Microsoft.EntityFrameworkCore;

namespace Clinic.Web.Models
{
    // Prevent duplicate appointments for the same doctor at the same start time
    [Index(nameof(DoctorId), nameof(StartTime), IsUnique = true)]
    public class Appointment
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public int DurationInMinutes { get; set; }
    }
}
