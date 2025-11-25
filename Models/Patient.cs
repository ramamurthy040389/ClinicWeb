namespace Clinic.Web.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string FileNo { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
