using MailWorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>(); // Đây là dòng kích hoạt file Worker.cs
var host = builder.Build();
host.Run();