using FitZone.Api.Data;
using FitZone.Api.Services;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// The real connection string (with the MySQL password) lives in appsettings.Local.json,
// which is git-ignored. Copy appsettings.Local.example.json to create it.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

var connectionString = builder.Configuration.GetConnectionString("FitZone");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "No database connection string. Copy appsettings.Local.example.json to appsettings.Local.json and fill in your MySQL password.");

builder.Services.AddMySqlDataSource(connectionString);
builder.Services.AddScoped<Db>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddControllers();

var app = builder.Build();

// Turn unhandled database errors into readable JSON instead of a blank 500
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (MySqlException ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { message = "Database error: " + ex.Message });
    }
});

app.UseDefaultFiles();   // serves wwwroot/index.html at "/"
app.UseStaticFiles();
app.MapControllers();

app.Run();
