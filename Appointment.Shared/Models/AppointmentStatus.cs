namespace Appointment.Shared.Models;

public enum AppointmentStatus
{
    Pending,              // Just created, processing in progress
    Confirmed,            // Fully verified, notification sent
    PendingVerification,  // Insurance couldn't be verified (degraded mode)
    Cancelled,
    Failed                // Unrecoverable error
}
