using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database (EF Core Code-First on SQL Server) ---
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

// --- Azure Blob Storage (event banners and profile pictures; all access goes through this API) ---
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// --- Session-based authentication (user id + role are kept server-side) ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "REST API for the RaceDay race-event system. " +
                      "Log in with POST /api/auth/login first - the session cookie is then sent automatically by Swagger UI. " +
                      "Organiser-only and Participant-only endpoints return 403 for the wrong role and 401 when not logged in."
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);   // XML comments become the Swagger descriptions
});

var app = builder.Build();

// --- Create/upgrade the database on start-up (retries while SQL Server is still starting, e.g. in Docker) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
                db.Database.Migrate();          // SQL Server: apply the Code-First migrations
            else
                db.Database.EnsureCreated();    // in-memory provider (unit tests) or no migrations added yet
            break;
        }
        catch (Exception ex) when (attempt < 12 && ex is SqlException sql && sql.Number != 2714)
        {
            app.Logger.LogWarning("Database is not ready yet (attempt {Attempt} of 12). Retrying in 5 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }

    if (app.Environment.IsDevelopment())
        DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.DocumentTitle = "RaceDay API";
});

app.UseSession();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Makes the Program class visible to the test project (WebApplicationFactory<Program>). 
public partial class Program { }
