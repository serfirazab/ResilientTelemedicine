using Appointment.API.Data;
using Appointment.Shared.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add EF Core DbContext using Aspire component
builder.AddNpgsqlDbContext<AppointmentDbContext>("appointmentdb");

var app = builder.Build();

app.MapDefaultEndpoints();

var group = app.MapGroup("/api/appointments");
group.MapGet("/", () => "API is running");

// Automatically apply migrations and seed data at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
