namespace Restaurant.Domain.Persistence;

internal static class AtomicFile
{
    public static async Task WriteAsync(string path, Func<Stream, Task> write, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);
        token.ThrowIfCancellationRequested();

        string destination = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(destination)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(directory);
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await write(stream);
                await stream.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
