namespace TokenTap.Storage;

public sealed record DatabaseSize(string Path, long Bytes)
{
    public decimal Megabytes => Math.Round(Bytes / 1024m / 1024m, 3);
}
