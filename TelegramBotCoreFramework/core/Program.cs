using Analytics;
using CommunicationChat;
using Helpers;
using SpecificToDevEnv;
using TG.UpdatesProcessing;
using TG.Webhooks.Processing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =========== other bindings ===========

builder.Services.AddAnalyticsServices();
builder.Services.AddHelpersServices();
builder.Services.AddWebhookProcessingServices();
builder.Services.AddTgUpdateProcessingServices();
builder.Services.AddCommunicationChannelsServices();

switch (Env.ClientName)
{
    case C.DevEnvClientName:
        builder.Services.AddDevEnvSpecificBindings();
        break;
}

// ===========

var app = builder.Build();
app.UseAuthorization();

app.MapControllers();

app.Run();