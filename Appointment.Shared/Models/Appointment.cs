namespace Appointment.Shared.Models;

public class Appointment
{
    public Guid Id { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public AppointmentStatus Status { get; set; }
    public bool InsuranceVerified { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? FailureReason { get; set; }
    public bool NotificationSent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
