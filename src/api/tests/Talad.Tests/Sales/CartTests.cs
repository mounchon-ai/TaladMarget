using Talad.Domain.Accounts;
using Talad.Domain.Catalog;
using Talad.Domain.Sales;

namespace Talad.Tests.Sales;

[Trait("feature", "FE-talad-005")]
public class CartTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Product Product(int id, string name, int stock, int threshold = 0)
    {
        var p = new Product(name, null, stock, threshold, Now);
        typeof(Product).GetProperty(nameof(Talad.Domain.Catalog.Product.Id))!.SetValue(p, id);
        return p;
    }

    [Fact, Trait("ac", "AC-talad-001")]
    public void Adding_the_same_product_again_raises_the_line_instead_of_adding_a_second()
    {
        var cart = new Cart(1, Now);
        var orange = Product(1, "ส้มสายน้ำผึ้ง", stock: 10);

        cart.AddOne(orange, Now);
        cart.AddOne(orange, Now);
        cart.AddOne(orange, Now);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(3, line.Qty);
    }

    [Fact, Trait("ac", "AC-talad-069")]
    public void Plus_beyond_what_is_left_is_refused_and_the_quantity_stays()
    {
        var cart = new Cart(1, Now);
        var orange = Product(1, "ส้มสายน้ำผึ้ง", stock: 2);
        cart.AddOne(orange, Now);
        cart.AddOne(orange, Now);

        var e = Assert.Throws<InsufficientStockException>(() => cart.SetQty(orange, 3));

        Assert.Equal("ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)", e.Message);
        Assert.Equal(2, cart.Lines.Single().Qty);
    }

    [Fact, Trait("ac", "AC-talad-110")]
    public void A_product_with_nothing_left_does_not_go_in()
    {
        var cart = new Cart(1, Now);
        var mangosteen = Product(2, "มังคุด แพ็ก", stock: 0);

        var e = Assert.Throws<InsufficientStockException>(() => cart.AddOne(mangosteen, Now));

        Assert.Equal("มังคุด แพ็ก คงเหลือไม่พอ (เหลือ 0)", e.Message);
        Assert.Empty(cart.Lines);
    }

    [Fact, Trait("ac", "AC-talad-003")]
    public void Quantity_zero_takes_the_line_out()
    {
        var cart = new Cart(1, Now);
        var orange = Product(1, "ส้มสายน้ำผึ้ง", stock: 10);
        var mangosteen = Product(2, "มังคุด แพ็ก", stock: 10);
        cart.AddOne(orange, Now);
        cart.AddOne(mangosteen, Now);

        cart.SetQty(orange, 0);

        Assert.Equal([2], cart.Lines.Select(l => l.ProductId));
    }

    [Fact]
    public void A_discontinued_product_cannot_be_added()
    {
        var cart = new Cart(1, Now);
        var orange = Product(1, "ส้มสายน้ำผึ้ง", stock: 10);
        orange.Discontinue(new UserAccount("owner", "เจ้าของร้าน", UserRole.Owner, Now));

        var e = Assert.Throws<ProductDiscontinuedException>(() => cart.AddOne(orange, Now));

        Assert.Equal("ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า", e.Message);
    }

    [Theory, Trait("ac", "AC-talad-071"), Trait("ac", "AC-talad-072")]
    [InlineData(5, true)]  // AC-talad-071 · threshold 5, left 5 → "ใกล้หมด"
    [InlineData(6, false)] // AC-talad-072 · threshold 5, left 6 → no badge
    [InlineData(0, true)]
    public void Low_stock_is_at_or_below_the_threshold(int left, bool low)
    {
        Assert.Equal(low, Product(1, "ส้มสายน้ำผึ้ง", stock: left, threshold: 5).IsLowStock);
    }
}
