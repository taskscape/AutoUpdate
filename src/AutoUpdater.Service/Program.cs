using AutoUpdater.Service;
using Microsoft.Extensions.Hosting;

// Per-app SYSTEM Windows Service host (spec §2.1). The application id is supplied by the
// InnoSetup installer when it registers the service (e.g. --application-id MyApp), so a single
// service binary can be reused across apps while still running as a dedicated per-app instance.

// Management verbs used by the InnoSetup installer (spec §2.3):
//   AutoUpdater.Service.exe install|uninstall|start|stop --application-id <id>
if (args.Length > 0 && args[0] is "install" or "uninstall" or "start" or "stop")
{
    var verb = args[0];
    var appId = ResolveApplicationId(args);
    return verb switch
    {
        "install" => ServiceControl.Install(appId),
        "uninstall" => ServiceControl.Uninstall(appId),
        "start" => ServiceControl.Start(appId),
        "stop" => ServiceControl.Stop(appId),
        _ => 1,
    };
}

var applicationId = ResolveApplicationId(args);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new ServiceOptions(applicationId));
builder.Services.AddHostedService<UpdaterWorker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = $"AutoUpdater.{applicationId}";
});

var host = builder.Build();
host.Run();
return 0;

static string ResolveApplicationId(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--application-id" or "-a")
            return args[i + 1];
    }

    return Environment.GetEnvironmentVariable("AUTOUPDATER_APP_ID")
           ?? throw new InvalidOperationException(
               "Application id not provided. Pass --application-id <id> or set AUTOUPDATER_APP_ID.");
}
