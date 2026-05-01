namespace TokenTap.Cli;

public interface IConsole
{
    TextWriter Out { get; }

    TextWriter ErrorOut { get; }
}

public sealed class SystemConsole : IConsole
{
    public TextWriter Out => Console.Out;

    public TextWriter ErrorOut => Console.Error;
}
