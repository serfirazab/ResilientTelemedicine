using Appointment.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.API.Data;

public class AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : DbContext(options)
{
    public DbSet<Shared.Models.Appointment> Appointments => Set<Shared.Models.Appointment>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Doctor>().HasData(
            new Doctor { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Dr. Sarah Chen", Specialty = "Cardiology" },
            new Doctor { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Dr. James Wilson", Specialty = "Dermatology" },
            new Doctor { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Dr. Emily Park", Specialty = "Neurology" },
            new Doctor { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Dr. Michael Torres", Specialty = "Pediatrics" },
            new Doctor { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Dr. Ayşe Demir", Specialty = "General Practice" }
        );
    }
}
