using System.Net;
using System.Net.Http.Json;
using Appointment.Shared.DTOs;
using Appointment.Shared.Models;
using Aspire.Hosting.Testing;
using Xunit;

namespace Appointment.IntegrationTests;

public class AppointmentBookingTests
{
    [Fact]
    public async Task BookResilient_ShouldHandleChaosAndReturnValidBusinessResponse()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Appointment_AppHost>();
        
        await using var app = await appHost.BuildAsync();
        
        // Ensure resources are started
        await app.StartAsync();

        // Create an HttpClient configured to communicate with the API
        var httpClient = app.CreateHttpClient("appointment-api");
        
        // Wait a brief moment for DB migrations to complete inside the container
        await Task.Delay(2000);

        var request = new AppointmentBookingRequest
        {
            PatientId = "Test Patient",
            DoctorId = "DOC-101",
            InsuranceNumber = "INS-TEST-123",
            EstimatedCost = 100m,
            RequestedTime = DateTime.UtcNow.AddDays(1)
        };

        // Act
        // We might hit the Polly retry logic here if 503 happens during communication
        var response = await httpClient.PostAsJsonAsync("/api/appointments/book-resilient", request);

        // Assert
        // We should NEVER get a 5xx error because Polly handles the transient 503/500s.
        // We should either get 200 OK (Success + Fallback) or 400 Bad Request (Business Rule: Doctor Unavailable)
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest, 
            $"Expected OK or BadRequest (Business Rule), but got {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
            
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("The requested time slot is already booked", content);
        }
    }
}
