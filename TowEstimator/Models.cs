using System;

namespace TowEstimator;

public sealed class EstimatorPreferences
{
    public string YardAddress { get; set; } = "184 Nicholson Rd, Lincolnton, NC 28092";
    public string CustomDeadheadAddress { get; set; } = string.Empty;
    public bool UseDefaultYard { get; set; } = true;
    public decimal BaseFee { get; set; } = 30m;
    public decimal RatePerMile { get; set; } = 3.50m;
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string DiscountReason { get; set; } = string.Empty;
}

public sealed class LogEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset When { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhonePrimary { get; set; } = string.Empty;
    public string PhoneSecondary { get; set; } = string.Empty;
    public string Pickup { get; set; } = string.Empty;
    public string Dropoff { get; set; } = string.Empty;
    public string Deadhead { get; set; } = string.Empty;
    public double DeadheadMiles { get; set; }
    public double TowMiles { get; set; }
    public double TotalMiles { get; set; }
    public decimal BaseFee { get; set; }
    public decimal RatePerMile { get; set; }
    public decimal MileageCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalCost { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class GeoPoint
{
    public string Formatted { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

public sealed class EstimateResult
{
    public LogEntry Entry { get; init; } = new();
    public string Summary { get; init; } = string.Empty;
}
