using NFury.Commands.Run;
using NFury.Commands.Server;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;

WriteCopyrigth();

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<RunCommand>("run")
        .WithDescription("Execute the load test.");

    config.AddCommand<ServerCommand>("server")
        .WithDescription("Start the web server with dashboard for load testing.");
});

return app.Run(args);


static void WriteCopyrigth()
{
    AnsiConsole.Clear();
    AnsiConsole.Write(
        new FigletText("NFury")
            .LeftJustified()
            .Color(Color.Blue));

    Grid grid = new();
    grid.AddColumn();
    grid.AddColumn();

    grid.AddRow(
    [
        new Text("Developed by: ", new Style(Color.Red, Color.Default)).LeftJustified(),
        new Text("Unhacked, 2024", new Style(Color.Red, Color.Default)).LeftJustified()
    ]);
    grid.AddRow(
    [
        new Text("Version: ", new Style(Color.Red, Color.Default)).LeftJustified(),
        new Text($"{Assembly.GetEntryAssembly()?.GetName().Version}", new Style(Color.Red, Color.Default)).LeftJustified()
    ]);
    AnsiConsole.Write(grid);
    AnsiConsole.WriteLine();
}