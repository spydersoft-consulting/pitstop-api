using System.ComponentModel.DataAnnotations;

namespace Spydersoft.PitStop.Contracts.FillUps;

public class FillUpRequest
{
    public DateTimeOffset FilledAt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OdometerReading { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal GallonsAdded { get; set; }

    /// <summary>Valid values: Regular, MidGrade, Premium, Diesel, E85. Defaults to MidGrade if omitted.</summary>
    public string? FuelGrade { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal? PricePerGallon { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal? TotalCost { get; set; }

    public bool IsFullFillUp { get; set; } = true;

    [MaxLength(200)]
    public string? StationName { get; set; }

    [MaxLength(500)]
    public string? StationAddress { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
