namespace Spydersoft.PitStop.Data.Entities;

public class FillUp
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateTimeOffset FilledAt { get; set; }
    public decimal OdometerReading { get; set; }
    public decimal GallonsAdded { get; set; }
    public FuelGrade FuelGrade { get; set; } = FuelGrade.MidGrade;
    public decimal PricePerGallon { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsFullFillUp { get; set; }

    public string? StationName { get; set; }
    public string? StationAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }

    // Stored computed values for query performance
    public decimal? MilesSinceLastFillUp { get; set; }
    public decimal? MpgThisFillUp { get; set; }
}
