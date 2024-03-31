using NFury.Commands.Run;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;
using NFury.UI;

Messages.WriteCopyrigth();

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<RunCommand>("run")
        .WithDescription("Execute the load test.");
});

return app.Run(args);

