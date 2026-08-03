using Appointment.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Mock Endpoint to simulate schedule checks
app.MapPost("/api/schedule/validate", async ([FromBody] ScheduleValidationRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("Received schedule validation for Doctor {DoctorId} at {RequestedTime}", request.DoctorId, request.RequestedTime);
    
    var rnd = new Random();
    
    // Simulate 10% chance of random network/system failure
    if (rnd.NextDouble() < 0.1)
    {
        logger.LogError("Simulating random Schedule API failure");
        return Results.Problem("Schedule Service database connection timeout", statusCode: 503);
    }
    
    // Simulate 20% chance of schedule conflict
    if (rnd.NextDouble() < 0.2)
    {
        logger.LogWarning("Simulating schedule conflict for doctor {DoctorId}", request.DoctorId);
        return Results.Ok(new ScheduleValidationResponse
        {
            IsAvailable = false,
            Reason = "The requested time slot is already booked or the doctor is unavailable."
        });
    }
    
    // Success scenario
    return Results.Ok(new ScheduleValidationResponse
    {
        IsAvailable = true,
        Reason = "Time slot is available."
    });
})
.WithName("ValidateSchedule")
;

app.Run();
