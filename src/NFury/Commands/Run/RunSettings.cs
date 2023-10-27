using Spectre.Console.Cli;
using System.ComponentModel;

namespace NFury.Commands.Run;

public class RunSettings : CommandSettings
{
    [CommandOption("-u|--virtual-users")]
    [DefaultValue(10)]
    [Description("Define a number of concurrency users. Default is 10.")]
    public int VirtualUsers { get; set; }

    [CommandOption("-r|--requests")]
    [DefaultValue(100)]
    [Description("Define a number of requests. Default is 100.")]
    public int Requests { get; set; }

    [CommandArgument(0, "[URL]")]
    public string? Url { get; set; }

    [CommandOption("-m|--method <METHOD>")]
    [DefaultValue("GET")]
    public string? Method { get; set; }
}
