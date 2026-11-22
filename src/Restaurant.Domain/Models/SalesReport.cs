using System.Xml.Serialization;

namespace Restaurant.Domain.Models;
[XmlRoot("SalesReport")]
public sealed class SalesReport
{
    public string GeneratedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public int PaidOrders { get; set; }
    public decimal Total { get; set; }
    public List<CategorySales> Categories { get; set; } = [];
}

public sealed class CategorySales
{
    public string Category { get; set; } = "";
    public long Quantity { get; set; }
    public decimal Revenue { get; set; }
}
