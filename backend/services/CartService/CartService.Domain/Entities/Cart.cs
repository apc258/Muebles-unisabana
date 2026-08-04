namespace CartService.Domain.Entities;

public class Cart
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public List<CartItem> Items { get; set; } = new();
}

public class CartItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
