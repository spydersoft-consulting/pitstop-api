using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Spydersoft.PitStop.Api.Controllers;
using Spydersoft.PitStop.Api.Services;
using Spydersoft.PitStop.Contracts.FillUps;
using Spydersoft.PitStop.Data;
using Spydersoft.PitStop.Data.Entities;

namespace Spydersoft.PitStop.Api.UnitTests.Controllers;

[TestFixture]
public class FillUpsControllerTests
{
    private const string TestUserId = "test-user";

    private SqliteConnection _connection = null!;
    private PitStopDbContext _db = null!;
    private FillUpService _service = null!;
    private FillUpsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PitStopDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new PitStopDbContext(options);
        _db.Database.EnsureCreated();
        _service = new FillUpService(_db);
        _controller = new FillUpsController(_db, _service)
        {
            ControllerContext = BuildControllerContext(TestUserId)
        };
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static ControllerContext BuildControllerContext(string? subClaim)
    {
        var identity = subClaim is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subClaim)], "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<Vehicle> CreateVehicleAsync(string ownerId = TestUserId)
    {
        var vehicle = new Vehicle
        {
            OwnerId = ownerId,
            Name = "Test Bronco",
            Year = 2024,
            Make = "Ford",
            Model = "Bronco",
            StartDate = new DateOnly(2024, 1, 1)
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        return vehicle;
    }

    [Test]
    public async Task GetAll_ReturnsOnlyFillUpsForVehicle_OwnedByCaller()
    {
        var v = await CreateVehicleAsync();
        _db.FillUps.Add(new FillUp
        {
            VehicleId = v.Id,
            FilledAt = DateTimeOffset.UtcNow,
            OdometerReading = 1000,
            GallonsAdded = 10,
            PricePerGallon = 3.50m,
            TotalCost = 35.00m,
            IsFullFillUp = true
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll(
            v.Id,
            new FillUpListQuery { OrderBy = "odometer" },
            CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var response = ok!.Value as FillUpListResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.TotalCount, Is.EqualTo(1));
        Assert.That(response.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetAll_ReturnsNotFound_WhenVehicleNotOwnedByCaller()
    {
        var v = await CreateVehicleAsync(ownerId: "someone-else");

        var result = await _controller.GetAll(
            v.Id,
            new FillUpListQuery { OrderBy = "odometer" },
            CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Create_ReturnsValidationProblem_WhenBothPriceAndTotalAreNull()
    {
        var v = await CreateVehicleAsync();

        var request = new CreateFillUpRequest
        {
            OdometerReading = 1000,
            GallonsAdded = 10,
            PricePerGallon = null,
            TotalCost = null
        };

        var result = await _controller.Create(v.Id, request, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
        var problem = ((ObjectResult)result.Result!).Value as ValidationProblemDetails;
        Assert.That(problem, Is.Not.Null);
    }

    [Test]
    public async Task Create_PersistsFillUp_WhenRequestIsValid()
    {
        var v = await CreateVehicleAsync();

        var request = new CreateFillUpRequest
        {
            OdometerReading = 1000,
            GallonsAdded = 10,
            PricePerGallon = 3.50m,
            IsFullFillUp = true
        };

        var result = await _controller.Create(v.Id, request, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<CreatedAtActionResult>());
        Assert.That(await _db.FillUps.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public void GetCurrentUserId_Throws_WhenNoSubClaim()
    {
        _controller.ControllerContext = BuildControllerContext(subClaim: null);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _controller.GetAll(
                vehicleId: 1,
                new FillUpListQuery { OrderBy = "odometer" },
                CancellationToken.None));
    }
}
