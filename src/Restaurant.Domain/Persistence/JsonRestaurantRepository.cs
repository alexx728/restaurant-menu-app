using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Restaurant.Domain.Models;
using Restaurant.Domain.Services;

namespace Restaurant.Domain.Persistence;
public sealed class JsonRestaurantRepository : IRestaurantRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter<OrderStatus>(allowIntegerValues: false)
        }
    };
    public Task<RestaurantData> LoadAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken token = default) => Task.Run(async () =>
    {
        try
        {
            progress?.Report(new(0, "Читання JSON"));
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<RestaurantDocument>(stream, Options, token) ?? throw new InvalidDataException("JSON не містить об'єкта даних.");
            progress?.Report(new(50, "Перевірка даних і зв'язків"));
            var result = RestaurantMapper.FromDocument(document, token);
            token.ThrowIfCancellationRequested();
            progress?.Report(new(100, "Імпорт завершено"));
            return result;
        }
        catch (Exception ex)when (ex is JsonException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Файл містить некоректні дані: " + ex.Message, ex);
        }
    }, token);
    public Task SaveAsync(string path, RestaurantData data, IProgress<OperationProgress>? progress = null, CancellationToken token = default)
    {
        var snapshot = RestaurantMapper.ToDocument(data);
        return Task.Run(async () =>
        {
            _ = RestaurantMapper.FromDocument(snapshot, token);
            progress?.Report(new(10, "Запис JSON"));
            await AtomicFile.WriteAsync(path, stream => JsonSerializer.SerializeAsync(stream, snapshot, Options, token), token);
            progress?.Report(new(100, "JSON збережено"));
        }, token);
    }
}
