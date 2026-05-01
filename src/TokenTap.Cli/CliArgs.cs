namespace TokenTap.Cli;

public sealed class CliArgs
{
    private readonly IReadOnlyList<string> _args;

    public CliArgs(IReadOnlyList<string> args)
    {
        _args = args;
    }

    public bool Has(string name) =>
        _args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    public string? Value(string name)
    {
        for (int i = 0; i < _args.Count - 1; i++)
        {
            if (string.Equals(_args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return _args[i + 1];
            }
        }

        return null;
    }

    public int IntValue(string name, int fallback) =>
        int.TryParse(Value(name), out int parsed) ? parsed : fallback;

    public decimal DecimalValue(string name, decimal fallback) =>
        decimal.TryParse(Value(name), out decimal parsed) ? parsed : fallback;

    public IReadOnlyList<string> TailAfterDoubleDash()
    {
        int index = _args.ToList().FindIndex(arg => arg == "--");
        return index < 0 || index == _args.Count - 1 ? [] : _args.Skip(index + 1).ToArray();
    }

    public IReadOnlyList<string> Positionals() =>
        _args
            .TakeWhile(arg => arg != "--")
            .Where((arg, index) => !arg.StartsWith('-') && (index == 0 || !_args[index - 1].StartsWith('-')))
            .ToArray();
}
