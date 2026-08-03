using Appointment.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Mock Notification Endpoint
app.MapPost("/api/notifications/send", async ([FromBody] NotificationRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("Processing notification to {PatientId}", request.PatientId);
    
    // Simulate some quick processing delay
    await Task.Delay(TimeSpan.FromMilliseconds(500));
    
    logger.LogInformation("Notification Sent: [{Type}] {Message}", request.Type, request.Message);
    
    return Results.Ok(new { status = "Delivered" });
})
.WithName("SendNotification")
;

app.Run();
