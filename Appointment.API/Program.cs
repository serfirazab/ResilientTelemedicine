using Appointment.API.Data;
using Appointment.Shared.DTOs;
using Appointment.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Fallback;
using System.Net;

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

// RESILIENT CLIENTS (Phase 4)
builder.Services.AddHttpClient("ResilientScheduleService", client =>
{
    client.BaseAddress = new Uri("http://schedule-service");
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("ResilientInsuranceGateway", client =>
{
    client.BaseAddress = new Uri("http://insurance-gateway");
})
.AddResilienceHandler("insurance-fallback", pipelineBuilder =>
{
    // Fallback comes first in the pipeline (outermost)
    pipelineBuilder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<Exception>()
            .HandleResult(r => !r.IsSuccessStatusCode),
        FallbackAction = args =>
        {
            var fallbackResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new InsuranceValidationResponse
                {
                    IsApproved = true,
                    CoveredAmount = 0, // Fallback default
                    Reason = "Offline Verification (System degraded)"
                })
            };
            return Outcome.FromResultAsValueTask(fallbackResponse);
        }
    });

    // Timeout comes after fallback
    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(2));
});

builder.Services.AddHttpClient("ResilientNotificationService", client =>
{
    client.BaseAddress = new Uri("http://notification-service");
})
.AddStandardResilienceHandler();

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

// RESILIENT BOOKING ENDPOINT (With Polly)
group.MapPost("/book-resilient", async (
    [FromBody] AppointmentBookingRequest request, 
    IHttpClientFactory httpClientFactory,
    AppointmentDbContext db,
    ILogger<Program> logger) =>
{
    logger.LogInformation("Starting RESILIENT booking process for patient {PatientId}", request.PatientId);

    var scheduleClient = httpClientFactory.CreateClient("ResilientScheduleService");
    var insuranceClient = httpClientFactory.CreateClient("ResilientInsuranceGateway");
    var notificationClient = httpClientFactory.CreateClient("ResilientNotificationService");

    // 1. Check Schedule (Retry + Circuit Breaker will handle random 503s automatically)
    logger.LogInformation("Calling ScheduleService with Standard Resilience...");
    var scheduleReq = new ScheduleValidationRequest { DoctorId = request.DoctorId, RequestedTime = request.RequestedTime };
    var scheduleRes = await scheduleClient.PostAsJsonAsync("/api/schedule/validate", scheduleReq);
    
    // If it still fails after all retries, we return graceful error to user
    if (!scheduleRes.IsSuccessStatusCode)
    {
        return Results.BadRequest("Our scheduling system is currently experiencing heavy load. Please try again later.");
    }
    
    var scheduleResult = await scheduleRes.Content.ReadFromJsonAsync<ScheduleValidationResponse>();
    if (scheduleResult == null || !scheduleResult.IsAvailable)
    {
        return Results.BadRequest(scheduleResult?.Reason ?? "Schedule not available");
    }

    // 2. Check Insurance (Timeout + Fallback will prevent 5s hangs and 500s)
    logger.LogInformation("Calling InsuranceGateway with Fallback+Timeout...");
    var insuranceReq = new InsuranceValidationRequest 
    { 
        PatientId = request.PatientId, 
        InsuranceNumber = request.InsuranceNumber,
        EstimatedCost = request.EstimatedCost
    };
    var insuranceRes = await insuranceClient.PostAsJsonAsync("/api/insurance/validate", insuranceReq);
    
    // This will NEVER crash, because Fallback ensures a 200 OK is returned even on failure!
    var insuranceResult = await insuranceRes.Content.ReadFromJsonAsync<InsuranceValidationResponse>();
    if (insuranceResult == null || !insuranceResult.IsApproved)
    {
        return Results.BadRequest(insuranceResult?.Reason ?? "Insurance not approved");
    }

    // Determine status based on if fallback kicked in
    var isFallback = insuranceResult.Reason != null && insuranceResult.Reason.Contains("Offline");
    var finalStatus = isFallback ? AppointmentStatus.PendingVerification : AppointmentStatus.Confirmed;

    // 3. Save to Database
    logger.LogInformation("Saving appointment to database...");
    var appointment = new Appointment.Shared.Models.Appointment
    {
        Id = Guid.NewGuid(),
        PatientName = request.PatientId, // using PatientId as Name for mock purposes
        DoctorId = Guid.TryParse(request.DoctorId, out var parsedGuid) ? parsedGuid : Guid.Empty,
        SlotStart = request.RequestedTime,
        SlotEnd = request.RequestedTime.AddMinutes(30),
        Status = finalStatus,
        InsuranceVerified = !isFallback,
        InsurancePolicyNumber = request.InsuranceNumber,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();

    // 4. Send Notification (Fire-and-forget style for this demo so user doesn't wait)
    logger.LogInformation("Calling NotificationService asynchronously...");
    var notificationReq = new NotificationRequest 
    { 
        PatientId = request.PatientId, 
        Type = "EMAIL", 
        Message = $"Your appointment with {request.DoctorId} on {request.RequestedTime} is {finalStatus}." 
    };
    
    // We don't await this so the user gets an instant response! (Background fire and forget)
    _ = notificationClient.PostAsJsonAsync("/api/notifications/send", notificationReq);

    logger.LogInformation("Resilient Booking process completed successfully.");
    return Results.Ok(appointment);
});

// Automatically apply migrations and seed data at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
