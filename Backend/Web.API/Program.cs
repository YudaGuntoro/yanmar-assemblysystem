using Serilog;
using Web.API;

LoadLocalEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog before building the app
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)  // Read configuration from appsettings.json
    .Enrich.FromLogContext()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Month)
    .CreateLogger();

// Log starting message
Log.Information("Starting server.");

// Configure Serilog for the application host
builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(); // Include console logging
});

// Add services to the container
builder.Services.AddInfrastructure(builder.Configuration);

// Build the application
var app = builder.Build();


// Configure the HTTP request pipeline
app.UseWebApiPipeline();

// Run the application
app.Run();

static void LoadLocalEnvironmentFile()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        var envPath = Path.Combine(current.FullName, ".env");
        if (File.Exists(envPath))
        {
            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }

        current = current.Parent;
    }
}
