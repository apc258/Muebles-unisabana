namespace InventoryService.Domain.Entities;

public class StockItem
{
    public Guid ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
}
