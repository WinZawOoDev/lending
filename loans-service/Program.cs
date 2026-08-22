using loans_service.Consumers;
using loans_service.Data;
using loans_service.Search;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<LoansDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<AccountEventsConsumer>();

var elasticsearchUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
builder.Services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(elasticsearchUri))));
builder.Services.AddSingleton<AccountIndexer>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
