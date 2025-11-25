namespace Clinic.Web.Models.DTOs
{
    public class AppointmentQueryParameters
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int? DoctorId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? SortBy { get; set; } = "StartTime";
        public string? SortDir { get; set; } = "asc";
    }
}
