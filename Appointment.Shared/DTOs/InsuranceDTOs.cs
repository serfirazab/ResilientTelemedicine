namespace Appointment.Shared.DTOs;

public class InsuranceValidationRequest
{
    public required string PatientId { get; set; }
    public required string InsuranceNumber { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class InsuranceValidationResponse
{
    public bool IsApproved { get; set; }
    public decimal CoveredAmount { get; set; }
    public string? Reason { get; set; }
}
