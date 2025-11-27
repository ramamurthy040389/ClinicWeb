using System.ComponentModel.DataAnnotations;

namespace Clinic.Web.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Doctor name is required.")]
        [StringLength(120, ErrorMessage = "Doctor name cannot exceed 120 characters.")]
        [MinLength(2, ErrorMessage = "Doctor name must be at least 2 characters.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Specialization is required.")]
        [StringLength(80, ErrorMessage = "Specialization cannot exceed 80 characters.")]
        [MinLength(2, ErrorMessage = "Specialization must be at least 2 characters.")]
        public string Specialization { get; set; } = null!;

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
