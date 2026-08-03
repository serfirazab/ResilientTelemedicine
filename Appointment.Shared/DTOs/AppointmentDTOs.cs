using System;

namespace Appointment.Shared.DTOs;

public class AppointmentBookingRequest
{
    public required string PatientId { get; set; }
    public required string DoctorId { get; set; }
    public required string InsuranceNumber { get; set; }
    public DateTime RequestedTime { get; set; }
    public decimal EstimatedCost { get; set; }
}
