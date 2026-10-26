using loans_service.Consumers;
using loans_service.Data;
using loans_service.Health;
using loans_service.Middleware;
using loans_service.Search;
using loans_service.Audit;
using loans_service.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<LoansDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ILoanService, LoanService>();

const string PlaceholderJwtKey = "replace-me-with-a-secret-at-least-32-characters-long";

var jwtKey = builder.Configuration["Auth:Jwt:Key"]
    ?? throw new InvalidOperationException("Auth:Jwt:Key is missing.");
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Auth:Jwt:Key must be at least 32 characters long.");
}
if (!builder.Environment.IsDevelopment() && jwtKey == PlaceholderJwtKey)
{
    throw new InvalidOperationException(
        "Auth:Jwt:Key is still the placeholder value. Replace it with a strong secret outside Development.");
}
var jwtIssuer = builder.Configuration["Auth:Jwt:Issuer"]
    ?? throw new InvalidOperationException("Auth:Jwt:Issuer is missing.");
var jwtAudience = builder.Configuration["Auth:Jwt:Audience"]
    ?? throw new InvalidOperationException("Auth:Jwt:Audience is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("lending.api", policy =>
        policy.RequireAuthenticatedUser().RequireRole("lending.api"));
});

builder.Services.AddControllers()
    // Accept documented string enum values (e.g. "status": "Active") in JSON.
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807 responses for unhandled exceptions instead of bare 500s.
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch");

builder.Services.AddHostedService<AccountEventsConsumer>();

var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"]
    ?? throw new InvalidOperationException("Elasticsearch:Uri is missing.");
builder.Services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(elasticsearchUri))));
builder.Services.AddSingleton<AccountIndexer>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddHttpContextAccessor();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
    await db.Database.MigrateAsync();

    // Bootstrap the Elasticsearch index with an explicit mapping. A few
    // retries ride out a cold ES; if it stays down the consumer keeps
    // working and will bootstrap on its next reconnect.
    var indexer = scope.ServiceProvider.GetRequiredService<AccountIndexer>();
    foreach (var attempt in Enumerable.Range(1, 3))
    {
        try
        {
            await indexer.EnsureIndexAsync(CancellationToken.None);
            break;
        }
        catch (Exception ex)
        {
            if (attempt < 3)
            {
                app.Logger.LogWarning(ex, "Elasticsearch index bootstrap failed (attempt {Attempt}/3)", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            else
            {
                app.Logger.LogError(ex, "Elasticsearch index bootstrap failed after 3 attempts; continuing without index");
            }
        }
    }

    // Audit indexes use the same fail-soft semantics: bounded retries with
    // backoff, then continue booting; audit writes self-heal the indexes later.
    var audit = scope.ServiceProvider.GetRequiredService<AuditService>();
    await audit.EnsureOnStartupAsync(CancellationToken.None);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCorrelationId();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
