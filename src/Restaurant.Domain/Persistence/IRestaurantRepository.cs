using Restaurant.Domain.Models;
using Restaurant.Domain.Services;

namespace Restaurant.Domain.Persistence;
public interface IRestaurantRepository
{
    Task<RestaurantData> LoadAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken token = default);
    Task SaveAsync(string path, RestaurantData data, IProgress<OperationProgress>? progress = null, CancellationToken token = default);
}
