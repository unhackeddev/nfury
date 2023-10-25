using System.Reflection;
using NFury.Commands;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

WriteCopyrigth();

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<RunCommand>("run")
        .WithDescription("Execute the load test.");
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
    
    grid.AddRow(new IRenderable[]
    {
        new Text("Developed by: ", new Style(Color.Red, Color.Default)).LeftJustified(),
        new Text("Unhacked, 2023", new Style(Color.Red, Color.Default)).LeftJustified()
    });
    grid.AddRow(new IRenderable[]
    {
        new Text("Version: ", new Style(Color.Red, Color.Default)).LeftJustified(),
        new Text($"{Assembly.GetEntryAssembly()?.GetName().Version}", new Style(Color.Red, Color.Default)).LeftJustified()
    });
    AnsiConsole.Write(grid);
    AnsiConsole.WriteLine();
}