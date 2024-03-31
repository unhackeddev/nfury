using System.Reflection;
using NFury.Commands.Run;
using Spectre.Console;

namespace NFury.UI;

public static class Messages
{
    public static void WriteCopyrigth()
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
            new Text("Unhacked, 2023", new Style(Color.Red, Color.Default)).LeftJustified()
        ]);
        grid.AddRow(
        [
            new Text("Version: ", new Style(Color.Red, Color.Default)).LeftJustified(),
            new Text($"{Assembly.GetEntryAssembly()?.GetName().Version}", new Style(Color.Red, Color.Default)).LeftJustified()
        ]);
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }
    
    public static void DisplayRunningSettings(RunSettings runSettings)
    {
        AnsiConsole.MarkupLine("[bold]Execution context[/]");
        AnsiConsole.MarkupLine($"[bold]Url:[/] {runSettings.Url}");
        AnsiConsole.MarkupLine($"[bold]Virtual Users:[/] {runSettings.VirtualUsers}");
        AnsiConsole.MarkupLine($"[bold]Number of Requests:[/] {runSettings.Requests}");
    }
}