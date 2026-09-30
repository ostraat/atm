using Atm.Api.Middleware;
using Atm.Domain;
using Atm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Atm")
    ?? "Data Source=atm.db";

builder.Services.AddAtmInfrastructure(connectionString);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "ATM API",
        Version = "v1",
        Description =
            "Amounts and balances are integer cents (e.g. 10000 = $100.00). Timestamps are UTC. " +
            "Mutating POSTs require an Idempotency-Key header. Domain errors return application/problem+json."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Atm.Api.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var atm = scope.ServiceProvider.GetRequiredService<AtmService>();
    await atm.EnsureInitializedAsync();
}

app.UseMiddleware<AtmExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var spaPresent = File.Exists(Path.Combine(wwwroot, "index.html"));
if (spaPresent)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();

if (spaPresent)
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
