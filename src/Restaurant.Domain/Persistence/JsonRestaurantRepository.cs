using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Restaurant.Domain.Models;
using Restaurant.Domain.Services;

namespace Restaurant.Domain.Persistence;

public sealed class JsonRestaurantRepository : IRestaurantRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<OrderStatus>(allowIntegerValues: false) }
    };

    public Task<RestaurantData> LoadAsync(string path,
        IProgress<OperationProgress>? progress = null, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(async () =>
        {
            try
            {
                progress?.Report(new(0, "Читання файлу"));
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                RestaurantDocument document = await JsonSerializer.DeserializeAsync<RestaurantDocument>(stream, Options, token)
                    ?? throw new InvalidDataException("Файл не містить даних ресторану.");
                progress?.Report(new(50, "Перевірка меню та замовлень"));
                RestaurantData data = RestaurantMapper.FromDocument(document, token);
                progress?.Report(new(100, "Дані прочитано"));
                token.ThrowIfCancellationRequested();
                return data;
            }
            catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
            {
                throw new InvalidDataException("Некоректні дані у JSON: " + error.Message, error);
            }
        }, token);
    }

    public Task SaveAsync(string path, RestaurantData data,
        IProgress<OperationProgress>? progress = null, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        token.ThrowIfCancellationRequested();
        RestaurantDocument snapshot = RestaurantMapper.ToDocument(data);
        return Task.Run(async () =>
        {
            RestaurantMapper.FromDocument(snapshot, token);
            progress?.Report(new(10, "Збереження даних"));
            await AtomicFile.WriteAsync(path,
                stream => JsonSerializer.SerializeAsync(stream, snapshot, Options, token), token);
            progress?.Report(new(100, "Файл збережено"));
        }, token);
    }
}
