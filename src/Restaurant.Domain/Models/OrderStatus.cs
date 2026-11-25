namespace Restaurant.Domain.Models;

public enum OrderStatus
{
    New = 0,
    Preparing = 1,
    Served = 2,
    Paid = 3,
    Cancelled = 4
}
