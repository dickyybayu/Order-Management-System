using OMS.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiFoundation();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddOmsAuthenticationServices(builder.Configuration);
builder.Services.AddOmsBackgroundJobs();

var app = builder.Build();

app.UseApiFoundation();
app.UseOmsBackgroundJobs();
await app.InitializeDatabaseAsync();

app.Run();

public partial class Program;
