namespace Restaurant.Domain.Persistence;
internal static class AtomicFile
{
    public static async Task WriteAsync(string path, Func<Stream, Task> write, CancellationToken token)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await write(stream);
                await stream.FlushAsync(token);
            }

            token.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
