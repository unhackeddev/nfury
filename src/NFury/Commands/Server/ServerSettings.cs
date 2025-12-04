using Spectre.Console.Cli;
using System.ComponentModel;

namespace NFury.Commands.Server;

public class ServerSettings : CommandSettings
{
    [CommandOption("-p|--port")]
    [DefaultValue(5000)]
    [Description("Define the port for the web server. Default is 5000.")]
    public int Port { get; set; }

    [CommandOption("--host")]
    [DefaultValue("localhost")]
    [Description("Define the host for the web server. Default is localhost.")]
    public string Host { get; set; } = "localhost";
}
