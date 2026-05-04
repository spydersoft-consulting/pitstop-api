namespace Spydersoft.PitStop.Contracts.FillUps;

public class FillUpDto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public DateTimeOffset FilledAt { get; set; }
    public decimal OdometerReading { get; set; }
    public decimal GallonsAdded { get; set; }
    public string FuelGrade { get; set; } = "MidGrade";
    public decimal PricePerGallon { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsFullFillUp { get; set; }
    public string? StationName { get; set; }
    public string? StationAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }

    // Computed
    public decimal? MilesSinceLastFillUp { get; set; }
    public decimal? MpgThisFillUp { get; set; }
    public decimal? CostPerMile { get; set; }
}
