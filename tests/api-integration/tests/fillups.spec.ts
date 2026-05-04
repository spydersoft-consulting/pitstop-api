import { test, expect } from '@playwright/test';
import type { VehicleDto, FillUpDto, CreateFillUpRequest, FillUpRequest, FillUpListResponse } from './types';

let vehicleId: number;

test.beforeEach(async ({ request }) => {
  const response = await request.post('/api/v1/vehicles', {
    data: {
      name: `FillUp Test Vehicle ${crypto.randomUUID().replace(/-/g, '')}`,
      year: 2024,
      make: 'Ford',
      model: 'Bronco',
      startDate: '2024-01-01',
    },
  });
  const vehicle: VehicleDto = await response.json();
  vehicleId = vehicle.id;
});

test.afterEach(async ({ request }) => {
  await request.delete(`/api/v1/vehicles/${vehicleId}`);
});

function testFillUp(odometer: number): CreateFillUpRequest {
  return {
    odometerReading: odometer,
    gallonsAdded: 12.0,
    pricePerGallon: 3.5,
    totalCost: 42.0,
    isFullFillUp: true,
  };
}

test('CreateFillUp_Returns201WithComputedFields', async ({ request }) => {
  const response = await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, {
    data: testFillUp(1000),
  });

  expect(response.status()).toBe(201);
  const dto: FillUpDto = await response.json();
  expect(dto.id).toBeGreaterThan(0);
  expect(dto.vehicleId).toBe(vehicleId);
});

test('TwoFillUps_SecondHasMilesAndMpg', async ({ request }) => {
  await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(1000) });

  const response = await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(1240) });

  const dto: FillUpDto = await response.json();
  expect(dto.milesSinceLastFillUp).toBe(240);
  expect(dto.mpgThisFillUp).not.toBeNull(); // 240 / 12.0 = 20
});

test('GetFillUp_ById_ReturnsExpectedFields', async ({ request }) => {
  const created: FillUpDto = await (
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(2000) })
  ).json();

  const response = await request.get(`/api/v1/vehicles/${vehicleId}/fillups/${created.id}`);

  expect(response.status()).toBe(200);
  const dto: FillUpDto = await response.json();
  expect(dto.id).toBe(created.id);
  expect(dto.odometerReading).toBe(2000);
});

test('ListFillUps_ReturnsPaginatedResults', async ({ request }) => {
  for (let i = 0; i < 3; i++) {
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(3000 + i * 200) });
  }

  const response = await request.get(`/api/v1/vehicles/${vehicleId}/fillups`, {
    params: { pageSize: 2, page: 1 },
  });

  expect(response.status()).toBe(200);
  const list: FillUpListResponse = await response.json();
  expect(list.items).toHaveLength(2);
  expect(list.totalCount).toBeGreaterThanOrEqual(3);
});

test('UpdateFillUp_ReturnsUpdatedOdometer', async ({ request }) => {
  const created: FillUpDto = await (
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(5000) })
  ).json();

  const update: FillUpRequest = {
    filledAt: created.filledAt,
    odometerReading: 5100,
    gallonsAdded: 12.0,
    pricePerGallon: 3.5,
    totalCost: 42.0,
    isFullFillUp: true,
  };
  const response = await request.put(`/api/v1/vehicles/${vehicleId}/fillups/${created.id}`, { data: update });

  expect(response.status()).toBe(200);
  const updated: FillUpDto = await response.json();
  expect(updated.odometerReading).toBe(5100);
});

test('DeleteFillUp_Returns204ThenNotFound', async ({ request }) => {
  const created: FillUpDto = await (
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(9000) })
  ).json();

  expect((await request.delete(`/api/v1/vehicles/${vehicleId}/fillups/${created.id}`)).status()).toBe(204);
  expect((await request.get(`/api/v1/vehicles/${vehicleId}/fillups/${created.id}`)).status()).toBe(404);
});

test('CreateFillUp_FuelGrade_DefaultsMidGrade', async ({ request }) => {
  const dto: FillUpDto = await (
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: testFillUp(10000) })
  ).json();

  expect(dto.fuelGrade).toBe('MidGrade');
});

test('CreateFillUp_ExplicitFuelGrade_IsStored', async ({ request }) => {
  const req: CreateFillUpRequest = { ...testFillUp(11000), fuelGrade: 'Premium' };
  const dto: FillUpDto = await (
    await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: req })
  ).json();

  expect(dto.fuelGrade).toBe('Premium');
});

test('CreateFillUp_OnlyTotalCost_DerivesPricePerGallon', async ({ request }) => {
  const req: CreateFillUpRequest = {
    odometerReading: 12000,
    gallonsAdded: 10.0,
    totalCost: 35.0,
  };
  const response = await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: req });

  expect(response.status()).toBe(201);
  const dto: FillUpDto = await response.json();
  expect(dto.pricePerGallon).toBe(3.5);
  expect(dto.totalCost).toBe(35.0);
});

test('CreateFillUp_OnlyPricePerGallon_DerivesTotalCost', async ({ request }) => {
  const req: CreateFillUpRequest = {
    odometerReading: 13000,
    gallonsAdded: 10.0,
    pricePerGallon: 3.5,
  };
  const response = await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: req });

  expect(response.status()).toBe(201);
  const dto: FillUpDto = await response.json();
  expect(dto.pricePerGallon).toBe(3.5);
  expect(dto.totalCost).toBe(35.0);
});

test('CreateFillUp_NoCostFields_Returns400', async ({ request }) => {
  const req: CreateFillUpRequest = {
    odometerReading: 14000,
    gallonsAdded: 10.0,
  };
  const response = await request.post(`/api/v1/vehicles/${vehicleId}/fillups`, { data: req });

  expect(response.status()).toBe(400);
});
