using System;

namespace Appointment.Shared.DTOs;

public class ScheduleValidationRequest
{
    public required string DoctorId { get; set; }
    public DateTime RequestedTime { get; set; }
}

public class ScheduleValidationResponse
{
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
}
