namespace Appointment.Shared.DTOs;

public class NotificationRequest
{
    public required string PatientId { get; set; }
    public required string Type { get; set; } // e.g. "SMS", "EMAIL"
    public required string Message { get; set; }
}
