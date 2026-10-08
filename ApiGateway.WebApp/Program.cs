using ApiGateway.WebApp;
using ApiGateway.WebApp.Config;
using ApiGateway.WebApp.Extensions;
using Nur.Store2026.Observability;

var builder = WebApplication.CreateBuilder(args);

string serviceName = "ApiGateway";

builder.Host.UseLogging(serviceName, builder.Configuration);

// Add services to the container.
builder.Services.AddServices(builder.Configuration, builder.Environment, serviceName);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUiWithConsul();
}

app.UseRouting();
app.UseHealthChecks();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAdvancedScopeAuthorization();

app.UseCorrelationId();

app.UseAuthorization();

app.MapControllers();

app.MapReverseProxy();

app.Run();
