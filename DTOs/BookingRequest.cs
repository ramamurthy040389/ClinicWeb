namespace Clinic.Web.Models.DTOs
{
    public class BookingRequest
    {
        public int DoctorId { get; set; }
        public string StartTime { get; set; } = null!;    // ISO string from client
        public int DurationInMinutes { get; set; }
        public PatientDto Patient { get; set; } = null!;
    }

    public class PatientDto
    {
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string? FileNo { get; set; }
        public string Address { get; set; } = null!;
        public string DateOfBirth { get; set; } = null!; // ISO date string from client
        public string Gender { get; set; } = null!;
    }

    public class AppointmentUpdateRequest
    {
        public int? DoctorId { get; set; }
        public DateTime? StartTime { get; set; }
        public int? DurationInMinutes { get; set; }
        public AppointmentPatientUpdateDto? Patient { get; set; }
    }

    public class AppointmentPatientUpdateDto
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? FileNo { get; set; }
        public string? Address { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }

}
