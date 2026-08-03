var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();
var appointmentDb = postgres.AddDatabase("appointmentdb");

var scheduleService = builder.AddProject<Projects.Appointment_ScheduleService>("schedule-service");
var insuranceGateway = builder.AddProject<Projects.Appointment_InsuranceGateway>("insurance-gateway");
var notificationService = builder.AddProject<Projects.Appointment_NotificationService>("notification-service");

builder.AddProject<Projects.Appointment_API>("appointment-api")
    .WithReference(appointmentDb)
    .WithReference(scheduleService)
    .WithReference(insuranceGateway)
    .WithReference(notificationService)
    .WaitFor(appointmentDb);

builder.Build().Run();
