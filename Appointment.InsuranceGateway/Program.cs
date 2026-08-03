using Appointment.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Chaos Engineering Mock Endpoint
app.MapPost("/api/insurance/validate", async ([FromBody] InsuranceValidationRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("Received insurance validation request for patient {PatientId}", request.PatientId);
    
    var rnd = new Random();
    
    // Simulate 30% chance of latency
    if (rnd.NextDouble() < 0.3)
    {
        logger.LogWarning("Simulating latency for external insurance call...");
        await Task.Delay(TimeSpan.FromSeconds(rnd.Next(2, 6))); // 2-5 seconds
    }
    
    // Simulate 30% chance of failure
    if (rnd.NextDouble() < 0.3)
    {
        logger.LogError("Simulating random failure in external insurance call!");
        return Results.Problem("Insurance Provider Gateway is temporarily unavailable.", statusCode: 500);
    }
    
    // Success scenario
    logger.LogInformation("Insurance validation successful for patient {PatientId}", request.PatientId);
    var response = new InsuranceValidationResponse
    {
        IsApproved = true,
        CoveredAmount = request.EstimatedCost * 0.8m, // Covers 80%
        Reason = "Approved by mock provider"
    };
    
    return Results.Ok(response);
})
.WithName("ValidateInsurance")
;

app.Run();
