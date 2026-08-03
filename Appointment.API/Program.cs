using Appointment.API.Data;
using Appointment.Shared.DTOs;
using Appointment.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add EF Core DbContext
builder.AddNpgsqlDbContext<AppointmentDbContext>("appointmentdb");

// Standard HttpClients with Aspire Service Discovery
// Notice: NO RESILIENCE (Polly) PIPELINES ARE ADDED HERE YET
builder.Services.AddHttpClient("ScheduleService", client =>
{
    client.BaseAddress = new Uri("http://schedule-service");
});

builder.Services.AddHttpClient("InsuranceGateway", client =>
{
    client.BaseAddress = new Uri("http://insurance-gateway");
});

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri("http://notification-service");
});

var app = builder.Build();

app.MapDefaultEndpoints();

var group = app.MapGroup("/api/appointments");

// GET Endpoint for testing
group.MapGet("/", async (AppointmentDbContext db) => 
{
    return await db.Appointments.ToListAsync();
});

// NAIVE BOOKING ENDPOINT (No Resilience)
group.MapPost("/book-naive", async (
    [FromBody] AppointmentBookingRequest request, 
    IHttpClientFactory httpClientFactory,
    AppointmentDbContext db,
    ILogger<Program> logger) =>
{
    logger.LogInformation("Starting naive booking process for patient {PatientId}", request.PatientId);

    var scheduleClient = httpClientFactory.CreateClient("ScheduleService");
    var insuranceClient = httpClientFactory.CreateClient("InsuranceGateway");
    var notificationClient = httpClientFactory.CreateClient("NotificationService");

    // 1. Check Schedule
    logger.LogInformation("Calling ScheduleService...");
    var scheduleReq = new ScheduleValidationRequest { DoctorId = request.DoctorId, RequestedTime = request.RequestedTime };
    var scheduleRes = await scheduleClient.PostAsJsonAsync("/api/schedule/validate", scheduleReq);
    scheduleRes.EnsureSuccessStatusCode(); // Will crash if 500/503 is returned!
    var scheduleResult = await scheduleRes.Content.ReadFromJsonAsync<ScheduleValidationResponse>();
    if (scheduleResult == null || !scheduleResult.IsAvailable)
    {
        return Results.BadRequest(scheduleResult?.Reason ?? "Schedule not available");
    }

    // 2. Check Insurance
    logger.LogInformation("Calling InsuranceGateway...");
    var insuranceReq = new InsuranceValidationRequest 
    { 
        PatientId = request.PatientId, 
        InsuranceNumber = request.InsuranceNumber,
        EstimatedCost = request.EstimatedCost
    };
    var insuranceRes = await insuranceClient.PostAsJsonAsync("/api/insurance/validate", insuranceReq);
    insuranceRes.EnsureSuccessStatusCode(); // Will crash if 500 is returned!
    var insuranceResult = await insuranceRes.Content.ReadFromJsonAsync<InsuranceValidationResponse>();
    if (insuranceResult == null || !insuranceResult.IsApproved)
    {
        return Results.BadRequest(insuranceResult?.Reason ?? "Insurance not approved");
    }

    // 3. Save to Database
    logger.LogInformation("Saving appointment to database...");
    var appointment = new Appointment.Shared.Models.Appointment
    {
        Id = Guid.NewGuid(),
        PatientName = request.PatientId, // using PatientId as Name for mock purposes
        DoctorId = Guid.TryParse(request.DoctorId, out var parsedGuid) ? parsedGuid : Guid.Empty,
        SlotStart = request.RequestedTime,
        SlotEnd = request.RequestedTime.AddMinutes(30),
        Status = AppointmentStatus.Confirmed,
        InsuranceVerified = true,
        InsurancePolicyNumber = request.InsuranceNumber,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();

    // 4. Send Notification
    logger.LogInformation("Calling NotificationService...");
    var notificationReq = new NotificationRequest 
    { 
        PatientId = request.PatientId, 
        Type = "EMAIL", 
        Message = $"Your appointment with {request.DoctorId} on {request.RequestedTime} is confirmed." 
    };
    var notifRes = await notificationClient.PostAsJsonAsync("/api/notifications/send", notificationReq);
    notifRes.EnsureSuccessStatusCode();

    logger.LogInformation("Booking process completed successfully.");
    return Results.Ok(appointment);
});

// Automatically apply migrations and seed data at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
