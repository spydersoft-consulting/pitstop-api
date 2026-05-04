using Spydersoft.PitStop.Api.Services;
using Spydersoft.PitStop.Contracts.FillUps;
using Spydersoft.PitStop.Data;
using Spydersoft.PitStop.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Spydersoft.PitStop.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles/{vehicleId:int}/fillups")]
public class FillUpsController(PitStopDbContext db, FillUpService fillUpService)
    : PitStopControllerBase(db)
{
    private const string OrderByOdometer = "odometer";
    private const string OrderAscending = "asc";

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Read)]
    public async Task<ActionResult<FillUpListResponse>> GetAll(
        int vehicleId,
        [FromQuery] FillUpListQuery listQuery,
        CancellationToken ct = default)
    {
        if (!await VehicleExistsAsync(vehicleId, GetCurrentUserId(), ct))
            return NotFound();

        var pageSize = Math.Clamp(listQuery.PageSize, 1, 100);

        var query = Db.FillUps.Where(f => f.VehicleId == vehicleId);

        if (listQuery.From.HasValue)
            query = query.Where(f => f.FilledAt >= listQuery.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (listQuery.To.HasValue)
            query = query.Where(f => f.FilledAt <= listQuery.To.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var totalCount = await query.CountAsync(ct);

        query = (listQuery.OrderBy.ToLowerInvariant(), listQuery.Order.ToLowerInvariant()) switch
        {
            (OrderByOdometer, OrderAscending) => query.OrderBy(f => f.OdometerReading),
            (OrderByOdometer, _) => query.OrderByDescending(f => f.OdometerReading),
            (_, OrderAscending) => query.OrderBy(f => f.FilledAt),
            _ => query.OrderByDescending(f => f.FilledAt)
        };

        var items = await query
            .Skip((listQuery.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new FillUpListResponse
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = listQuery.Page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.Read)]
    public async Task<ActionResult<FillUpDto>> GetById(int vehicleId, int id, CancellationToken ct)
    {
        if (!await VehicleExistsAsync(vehicleId, GetCurrentUserId(), ct))
            return NotFound();

        var fillUp = await GetFillUpAsync(vehicleId, id, ct);
        return fillUp is null ? NotFound() : Ok(MapToDto(fillUp));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Write)]
    public async Task<ActionResult<FillUpDto>> Create(int vehicleId, CreateFillUpRequest request, CancellationToken ct)
    {
        if (!await VehicleExistsAsync(vehicleId, GetCurrentUserId(), ct))
            return NotFound();

        if (request.PricePerGallon is null && request.TotalCost is null)
            return ValidationProblem("At least one of pricePerGallon or totalCost must be provided.");

        var fillUp = new FillUp { VehicleId = vehicleId };
        ApplyRequest(fillUp, request);

        Db.FillUps.Add(fillUp);
        await Db.SaveChangesAsync(ct);
        await fillUpService.RecalculateComputedFieldsAsync(vehicleId, ct);

        return CreatedAtAction(nameof(GetById), new { vehicleId, id = fillUp.Id }, MapToDto(fillUp));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.Write)]
    public async Task<ActionResult<FillUpDto>> Update(int vehicleId, int id, FillUpRequest request, CancellationToken ct)
    {
        if (!await VehicleExistsAsync(vehicleId, GetCurrentUserId(), ct))
            return NotFound();

        if (request.PricePerGallon is null && request.TotalCost is null)
            return ValidationProblem("At least one of pricePerGallon or totalCost must be provided.");

        var fillUp = await GetFillUpAsync(vehicleId, id, ct);
        if (fillUp is null)
            return NotFound();

        ApplyRequest(fillUp, request);

        await Db.SaveChangesAsync(ct);
        await fillUpService.RecalculateComputedFieldsAsync(vehicleId, ct);

        return Ok(MapToDto(fillUp));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.Write)]
    public async Task<IActionResult> Delete(int vehicleId, int id, CancellationToken ct)
    {
        if (!await VehicleExistsAsync(vehicleId, GetCurrentUserId(), ct))
            return NotFound();

        var fillUp = await GetFillUpAsync(vehicleId, id, ct);
        if (fillUp is null)
            return NotFound();

        Db.FillUps.Remove(fillUp);
        await Db.SaveChangesAsync(ct);
        await fillUpService.RecalculateComputedFieldsAsync(vehicleId, ct);

        return NoContent();
    }

    private Task<FillUp?> GetFillUpAsync(int vehicleId, int id, CancellationToken ct) =>
        Db.FillUps.FirstOrDefaultAsync(f => f.Id == id && f.VehicleId == vehicleId, ct);

    private static void ApplyRequest(FillUp fillUp, FillUpRequest request)
    {
        var price = request.PricePerGallon;
        var cost = request.TotalCost;

        if (price is null)
            price = Math.Round(cost!.Value / request.GallonsAdded, 3);
        else if (cost is null)
            cost = Math.Round(price.Value * request.GallonsAdded, 2);

        fillUp.FilledAt = request.FilledAt;
        fillUp.OdometerReading = request.OdometerReading;
        fillUp.GallonsAdded = request.GallonsAdded;
        fillUp.FuelGrade = ParseFuelGrade(request.FuelGrade);
        fillUp.PricePerGallon = price.Value;
        fillUp.TotalCost = cost.Value;
        fillUp.IsFullFillUp = request.IsFullFillUp;
        fillUp.StationName = request.StationName;
        fillUp.StationAddress = request.StationAddress;
        fillUp.Latitude = request.Latitude;
        fillUp.Longitude = request.Longitude;
        fillUp.Notes = request.Notes;
    }

    private static FuelGrade ParseFuelGrade(string? value) =>
        Enum.TryParse<FuelGrade>(value, ignoreCase: true, out var grade) ? grade : FuelGrade.MidGrade;

    private static FillUpDto MapToDto(FillUp f) => new()
    {
        Id = f.Id,
        VehicleId = f.VehicleId,
        FilledAt = f.FilledAt,
        OdometerReading = f.OdometerReading,
        GallonsAdded = f.GallonsAdded,
        FuelGrade = f.FuelGrade.ToString(),
        PricePerGallon = f.PricePerGallon,
        TotalCost = f.TotalCost,
        IsFullFillUp = f.IsFullFillUp,
        StationName = f.StationName,
        StationAddress = f.StationAddress,
        Latitude = f.Latitude,
        Longitude = f.Longitude,
        Notes = f.Notes,
        MilesSinceLastFillUp = f.MilesSinceLastFillUp,
        MpgThisFillUp = f.MpgThisFillUp,
        CostPerMile = f.MilesSinceLastFillUp > 0
            ? Math.Round(f.TotalCost / f.MilesSinceLastFillUp!.Value, 3)
            : null
    };
}
