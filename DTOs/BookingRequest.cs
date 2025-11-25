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
    }

}
