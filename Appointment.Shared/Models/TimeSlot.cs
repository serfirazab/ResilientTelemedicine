namespace Appointment.Shared.Models;

public class TimeSlot
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsReserved { get; set; }
    public Guid? ReservedByAppointmentId { get; set; }
}
