using Poc.Exchange.Callback.Configuration;
using Poc.Exchange.Callback.Endpoints;
using Poc.Exchange.Callback.Options;
using Poc.Exchange.Callback.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddPocConfiguration();

builder.Services
    .AddOptions<GraphOptions>()
    .Bind(builder.Configuration.GetSection(GraphOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<INotificationStore, InMemoryNotificationStore>();
builder.Services.AddSingleton<IGraphCalendarService, GraphCalendarService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapGraphEndpoints();
app.Run();

public partial class Program;
