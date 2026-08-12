// =============================================================================
// V.A.L.I.D. Sample Models
// =============================================================================
// This file demonstrates how to define VALID objects with bitmask-tracked
// properties, validation rules, and source-generated boilerplate.
// =============================================================================

using Valid;

namespace Valid.Samples.Models;

// -----------------------------------------------------------------------------
// Sample 1: Basic Customer Object
// -----------------------------------------------------------------------------
// Demonstrates: [ValidObject], [ValidProperty], validation attributes
// -----------------------------------------------------------------------------

/// <summary>
/// A simple customer object with bitmask-tracked properties.
/// Changes to any property are automatically tracked in DirtyFlags.
/// </summary>
[ValidObject]
public partial class Customer : ValidObjectBase
{
    private string _name = "";
    private string _email = "";
    private int _age;
    private int _loyaltyScore;

    /// <summary>
    /// Customer's full name. Required, 3-100 characters.
    /// </summary>
    [ValidProperty]
    [Required("Customer name is required", "CUST-001")]
    [StringLength(100, "Name must be 3-100 characters", "CUST-002", MinimumLength = 3)]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, 0);
    }

    /// <summary>
    /// Customer's email address. Required.
    /// </summary>
    [ValidProperty]
    [Required("Email is required", "CUST-003")]
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value, 1);
    }

    /// <summary>
    /// Customer's age. Must be between 18 and 120.
    /// </summary>
    [ValidProperty]
    [Range(18, 120, "Age must be between 18 and 120", "CUST-004")]
    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value, 2);
    }

    /// <summary>
    /// Customer's loyalty score. Range 0-1000.
    /// </summary>
    [ValidProperty]
    [Range(0, 1000, "Score must be between 0 and 1000", "CUST-005")]
    public int LoyaltyScore
    {
        get => _loyaltyScore;
        set => SetProperty(ref _loyaltyScore, value, 3);
    }
}

// -----------------------------------------------------------------------------
// Sample 2: Product with Field Proxy
// -----------------------------------------------------------------------------
// Demonstrates: [ValidField] for auto-generated properties from private fields
// -----------------------------------------------------------------------------

/// <summary>
/// A product object using [ValidField] to eliminate boilerplate.
/// The source generator creates public properties from private fields.
/// </summary>
[ValidObject]
public partial class Product : ValidObjectBase
{
    [ValidField] private string _sku = "";
    [ValidField] private string _name = "";
    [ValidField] private decimal _price;
    [ValidField] private int _stockQuantity;

    // Generator emits:
    // public string Sku { get; set; }
    // public string Name { get; set; }
    // public decimal Price { get; set; }
    // public int StockQuantity { get; set; }
}

// -----------------------------------------------------------------------------
// Sample 3: Order with Explicit Bit Indexes
// -----------------------------------------------------------------------------
// Demonstrates: Explicit BitIndex assignment for stable API contracts
// -----------------------------------------------------------------------------

/// <summary>
/// An order object with explicit bit indexes for API stability.
/// Use this when you need guaranteed bit positions across versions.
/// </summary>
[ValidObject]
public partial class Order : ValidObjectBase
{
    private string _orderNumber = "";
    private DateTime _orderDate;
    private decimal _totalAmount;
    private string _status = "Pending";
    private string _shippingAddress = "";

    [ValidProperty(BitIndex = 0)]
    public string OrderNumber
    {
        get => _orderNumber;
        set => SetProperty(ref _orderNumber, value, 0);
    }

    [ValidProperty(BitIndex = 1)]
    public DateTime OrderDate
    {
        get => _orderDate;
        set => SetProperty(ref _orderDate, value, 1);
    }

    [ValidProperty(BitIndex = 2)]
    [Range(0.01, 1000000, "Invalid total amount", "ORD-001")]
    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value, 2);
    }

    [ValidProperty(BitIndex = 3)]
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value, 3);
    }

    [ValidProperty(BitIndex = 4)]
    public string ShippingAddress
    {
        get => _shippingAddress;
        set => SetProperty(ref _shippingAddress, value, 4);
    }
}
