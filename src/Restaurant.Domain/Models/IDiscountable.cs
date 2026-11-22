namespace Restaurant.Domain.Models;
public interface IDiscountable
{
    decimal DiscountPercent { get; }

    void SetDiscount(decimal discountPercent);
}
