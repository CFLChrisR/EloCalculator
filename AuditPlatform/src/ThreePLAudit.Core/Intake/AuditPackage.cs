namespace ThreePLAudit.Core.Intake;

/// <summary>
/// The contract between the intake system and the auditor. Intake normalizes whatever it
/// receives (PDF invoices, WMS exports, carrier EDI 210s, retailer portal deductions) into one
/// package per client per audit period. Every collection is optional so intake can ship partial
/// packages; rules simply skip what they have no data for.
/// </summary>
public sealed record AuditPackage
{
    public required string PackageId { get; init; }
    public required string ClientName { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<Facility> Facilities { get; init; } = [];

    // 3PL invoice vs contract
    public List<WarehouseContract> WarehouseContracts { get; init; } = [];
    public List<WarehouseInvoice> WarehouseInvoices { get; init; } = [];
    public List<ActivitySummary> Activity { get; init; } = [];

    // Freight bill audit
    public List<CarrierContract> CarrierContracts { get; init; } = [];
    public List<FreightBill> FreightBills { get; init; } = [];

    // Retailer deductions
    public List<RetailerProfile> RetailerProfiles { get; init; } = [];
    public List<Deduction> Deductions { get; init; } = [];
    public List<ShipmentEvidence> Shipments { get; init; } = [];
}

// ---------------------------------------------------------------- Facilities / compliance

public sealed record Facility
{
    public required string FacilityId { get; init; }
    public required string Name { get; init; }
    /// <summary>Two-letter USPS state code, e.g. "FL".</summary>
    public required string State { get; init; }
    public string? Operator { get; init; }
    /// <summary>Product types handled, e.g. "Food", "Hazmat", "Pharma", "ControlledSubstances".</summary>
    public List<string> Handles { get; init; } = [];
    public bool UsesPoweredIndustrialTrucks { get; init; } = true;
    public List<Credential> Credentials { get; init; } = [];
    public List<TrainingRecord> Training { get; init; } = [];
}

public sealed record Credential
{
    public required string Type { get; init; }
    public string? Number { get; init; }
    public string? IssuedBy { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public string? DocumentRef { get; init; }
}

public sealed record TrainingRecord
{
    public required string EmployeeId { get; init; }
    public required string Type { get; init; }
    public DateOnly CompletedOn { get; init; }
}

// ---------------------------------------------------------------- 3PL billing

public sealed record WarehouseContract
{
    public required string ContractId { get; init; }
    public required string ThreePLName { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    /// <summary>When set, a monthly minimum may only be billed if activity charges fall below it.</summary>
    public decimal? MonthlyMinimum { get; init; }
    public List<ContractRate> Rates { get; init; } = [];
}

public sealed record ContractRate
{
    public required string ChargeCode { get; init; }
    public string? Description { get; init; }
    /// <summary>Billing unit, e.g. "pallet/month", "order", "each", "hour".</summary>
    public string? Unit { get; init; }
    public decimal Rate { get; init; }
    /// <summary>Per-line minimum charge, if the contract has one.</summary>
    public decimal? LineMinimum { get; init; }
    /// <summary>Charges that must carry a client approval reference (e.g. special projects, labor).</summary>
    public bool RequiresApproval { get; init; }
}

public sealed record WarehouseInvoice
{
    public required string InvoiceNumber { get; init; }
    public string? ContractId { get; init; }
    public string? FacilityId { get; init; }
    public DateOnly InvoiceDate { get; init; }
    public DateOnly ServicePeriodStart { get; init; }
    public DateOnly ServicePeriodEnd { get; init; }
    public decimal? InvoiceTotal { get; init; }
    public List<InvoiceLine> Lines { get; init; } = [];
}

public sealed record InvoiceLine
{
    public required string LineId { get; init; }
    public required string ChargeCode { get; init; }
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal Amount { get; init; }
    /// <summary>Order, receipt, or project reference the charge relates to.</summary>
    public string? Reference { get; init; }
    public string? ApprovalReference { get; init; }
}

/// <summary>
/// Billable activity as recorded by the client's own systems (WMS, OMS, cycle counts), summed
/// per charge code per facility per period. This is what invoice quantities are tested against.
/// </summary>
public sealed record ActivitySummary
{
    public string? FacilityId { get; init; }
    public required string ChargeCode { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Quantity { get; init; }
    public string? Source { get; init; }
}

// ---------------------------------------------------------------- Freight

public enum FreightMode { Ltl, Parcel, Truckload }

public sealed record CarrierContract
{
    public required string Carrier { get; init; }
    public FreightMode Mode { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public List<LaneRate> Lanes { get; init; } = [];
    public List<FuelBand> FuelTable { get; init; } = [];
    public List<AllowedAccessorial> Accessorials { get; init; } = [];
    /// <summary>Parcel dimensional divisor (cubic inches per pound), e.g. 139.</summary>
    public decimal? DimDivisor { get; init; }
    /// <summary>False when the contract waives guaranteed-service refunds.</summary>
    public bool GuaranteedServiceRefunds { get; init; } = true;
}

public sealed record LaneRate
{
    /// <summary>Origin and destination as state codes or 3-digit ZIP prefixes; "*" matches anything.</summary>
    public required string Origin { get; init; }
    public required string Destination { get; init; }
    public decimal? RatePerCwt { get; init; }
    public decimal? FlatRate { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal MinimumCharge { get; init; }
}

public sealed record FuelBand
{
    /// <summary>Inclusive lower bound of the diesel price ($/gal) for this band.</summary>
    public decimal PriceFrom { get; init; }
    /// <summary>Exclusive upper bound.</summary>
    public decimal PriceTo { get; init; }
    public decimal SurchargePercent { get; init; }
}

public sealed record AllowedAccessorial
{
    public required string Code { get; init; }
    public decimal? MaxAmount { get; init; }
    /// <summary>When true the accessorial is only payable if the shipment requested it.</summary>
    public bool RequiresRequest { get; init; } = true;
}

public sealed record FreightBill
{
    public required string BillNumber { get; init; }
    public required string Carrier { get; init; }
    public FreightMode Mode { get; init; }
    /// <summary>PRO number (LTL) or tracking number (parcel).</summary>
    public required string ProNumber { get; init; }
    public DateOnly ShipDate { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public DateOnly? GuaranteedDeliveryDate { get; init; }
    /// <summary>Date the bill was received; starts the federal 180-day dispute clock.</summary>
    public DateOnly ReceivedDate { get; init; }
    public required string Origin { get; init; }
    public required string Destination { get; init; }
    public decimal BilledWeightLbs { get; init; }
    public decimal? ActualWeightLbs { get; init; }
    public Dimensions? Dimensions { get; init; }
    public string? BilledClass { get; init; }
    public decimal BaseCharge { get; init; }
    public decimal FuelSurcharge { get; init; }
    public List<BilledAccessorial> Accessorials { get; init; } = [];
    public decimal Total { get; init; }
    /// <summary>Published diesel index price for the week of the ship date ($/gal).</summary>
    public decimal? DieselPrice { get; init; }
    /// <summary>Accessorial codes the shipper actually requested on the BOL.</summary>
    public List<string> RequestedAccessorials { get; init; } = [];
}

public sealed record Dimensions(decimal LengthIn, decimal WidthIn, decimal HeightIn, int Pieces = 1)
{
    public decimal CubicFeet => LengthIn * WidthIn * HeightIn * Pieces / 1728m;
    public decimal CubicInches => LengthIn * WidthIn * HeightIn * Pieces;
}

public sealed record BilledAccessorial(string Code, decimal Amount);

// ---------------------------------------------------------------- Retailer deductions

public enum DeductionType { Shortage, LateShipment, EarlyShipment, Labeling, Asn, Pricing, Damage, Other }

/// <summary>
/// Retailer-specific rules. These come from each retailer's vendor agreement or routing guide,
/// which differ by retailer and change often, so they are always client-supplied.
/// </summary>
public sealed record RetailerProfile
{
    public required string Retailer { get; init; }
    public int? DisputeWindowDays { get; init; }
    public int? AsnDeadlineHours { get; init; }
    public string? RoutingGuideRef { get; init; }
}

public sealed record Deduction
{
    public required string DeductionId { get; init; }
    public required string Retailer { get; init; }
    public DeductionType Type { get; init; }
    public decimal Amount { get; init; }
    public DateOnly DeductionDate { get; init; }
    public string? PoNumber { get; init; }
    public decimal? UnitsClaimed { get; init; }
    public string? ReasonCode { get; init; }
    public string? Memo { get; init; }
}

public sealed record ShipmentEvidence
{
    public required string PoNumber { get; init; }
    public string? BolNumber { get; init; }
    public DateOnly ShipDate { get; init; }
    public DateOnly? ShipWindowStart { get; init; }
    public DateOnly? ShipWindowEnd { get; init; }
    public DateTimeOffset? ShippedAt { get; init; }
    public DateTimeOffset? AsnSentAt { get; init; }
    public decimal? UnitsShipped { get; init; }
    public bool PodSigned { get; init; }
    public decimal? PodUnitsReceived { get; init; }
    /// <summary>Exceptions noted on the POD ("2 cartons damaged"). Empty means a clean POD.</summary>
    public string? PodExceptions { get; init; }
    public bool LabelsCompliant { get; init; } = true;
    public List<string> DocumentRefs { get; init; } = [];
}
