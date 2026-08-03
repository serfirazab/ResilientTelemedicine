# Resilient Telemedicine

A modern, robust telemedicine appointment booking system demonstrating cloud-native resilience patterns using **.NET 10**, **.NET Aspire**, and **Polly**.

## 🌟 Overview

The goal of this project is to showcase how to build distributed systems that can survive downstream chaos (network failures, service outages, timeouts). It features a beautiful Blazor frontend, a central API gateway, and several mock microservices that intentionally simulate random failures.

## 🏗 Architecture

This project is built using the **.NET Aspire** orchestrator to seamlessly manage dependencies and containers.

- **Appointment.AppHost**: The central Aspire orchestrator that spins up the services, PostgreSQL, and MongoDB containers.
- **Appointment.Dashboard**: A premium Blazor Server frontend with Glassmorphism UI to book appointments.
- **Appointment.API**: A Minimal API acting as the central gateway. It routes booking requests to downstream microservices and enforces Polly Resilience pipelines.
- **Appointment.ScheduleService**: Checks doctor availability. (Simulates random 503 errors and business logic conflicts).
- **Appointment.InsuranceGateway**: Verifies patient insurance. (Simulates extreme latency/timeouts).
- **Appointment.NotificationService**: Sends booking confirmations. (Simulates random 500 server crashes).

## 🛡️ Resilience Patterns (Polly)

The system utilizes `Microsoft.Extensions.Http.Resilience` to protect the `Appointment.API` from downstream chaos:

1. **Standard Resilience**: Applied to `ScheduleService` and `NotificationService`. Automatically handles retries with exponential backoff and circuit breaking when the services randomly crash or return 5xx errors.
2. **Timeout + Fallback**: Applied to `InsuranceGateway`. Since this service simulates extreme latency, a tight `Timeout` policy ensures the user isn't kept waiting. When a timeout occurs, a `Fallback` policy catches the exception and returns a synthetic "Offline Verification" response, allowing the booking process to gracefully continue instead of failing.

## 🚀 How to Run

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker Desktop (Required by Aspire to host PostgreSQL and MongoDB)

### Execution
1. Clone the repository.
2. Navigate to the root directory.
3. Run the Aspire Host:
   ```bash
   dotnet run --project Appointment.AppHost
   ```
4. Open the Aspire Dashboard (URL provided in the terminal output) to view traces, metrics, and logs.
5. Click on the `blazor-dashboard` endpoint to open the Telemedicine UI.

### Testing the Chaos
In the Dashboard UI, try booking an appointment in **Naive API** mode. You will frequently encounter failures due to the simulated chaos. Then, switch to **Resilient Polly** mode and observe how the exact same chaos is mitigated seamlessly by the Polly pipelines!

## 🧪 Integration Tests

The project includes an end-to-end integration test suite using `Aspire.Hosting.Testing`.
To run the tests, which spin up the entire Aspire topology in memory:

```bash
dotnet test tests/Appointment.IntegrationTests
```
