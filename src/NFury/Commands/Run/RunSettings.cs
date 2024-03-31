using Spectre.Console.Cli;
using System.ComponentModel;

namespace NFury.Commands.Run;

public class RunSettings : CommandSettings
{
    [CommandOption("-u|--users")]
    [DefaultValue(10)]
    [Description("Define a number of concurrency users. Default is 10.")]
    public int Users { get; set; }

    [CommandOption("-r|--requests")]
    [DefaultValue(100)]
    [Description("Define a number of requests. Default is 100.")]
    public int Requests { get; set; }

    [CommandArgument(0, "[URL]")]
    public string? Url { get; set; }

    [CommandOption("-m|--method <METHOD>")]
    [DefaultValue("GET")]
    [Description("Define http verb. Default is GET.")]
    public string? Method { get; set; }

    [CommandOption("-b|--body <BODY>")]
    [Description("Body of request.")]
    public string? Body { get; set; }

    [CommandOption("-t|--content-type")]
    [DefaultValue("application/json")]
    [Description("Define the content type of request. Default is application/json.")]
    public string? ContentType { get; set;}

    [CommandOption("-d|--duration")]
    [Description("Define the duration of test in seconds.")]
    public int? Duration { get; set; }

    [CommandOption("-i|--insecure")]
    [Description("Allowing untrested SSL Certificates.")]
    public bool? Insecure { get; set; }

}
