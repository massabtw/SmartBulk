using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class WeightLogServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IWeightLogRepository> _repoMock = new();
    private readonly IValidator<WeightLogModel> _validator = new WeightLogModelValidator();
    private readonly Mock<ILogger<WeightLogService>> _loggerMock = new();

    public WeightLogServiceTests()
    {
        _uowMock.Setup(u => u.WeightLog).Returns(_repoMock.Object);
    }

    [Fact]
    public async Task LogWeightAsync_WhenValid_UpsertsAndReturnsModel()
    {
        var date = new DateOnly(2026, 10, 23);
        var model = new WeightLogModel
        {
            Date = date,
            WeightKg = 81.5m
        };

        var savedEntity = new WeightLogEntity
        {
            Id = 5,
            Uuid = Guid.NewGuid(),
            Date = date,
            WeightKg = 81.5m
        };

        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<WeightLogEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _repoMock.Setup(r => r.GetByDateAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedEntity);

        var service = new WeightLogService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.LogWeightAsync(model);

        result.Success.Should().BeTrue();
        result.Data!.WeightKg.Should().Be(81.5m);
        _repoMock.Verify(r => r.UpsertAsync(It.Is<WeightLogEntity>(e => e.WeightKg == 81.5m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogWeightAsync_WhenInvalidWeight_ReturnsValidationErrors()
    {
        var model = new WeightLogModel
        {
            Date = new DateOnly(2026, 10, 23),
            WeightKg = 15.0m // Implausible
        };

        var service = new WeightLogService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.LogWeightAsync(model);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        _repoMock.Verify(r => r.UpsertAsync(It.IsAny<WeightLogEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLatestAsync_WhenFound_ReturnsLatestWeight()
    {
        var entity = new WeightLogEntity
        {
            Id = 1,
            Date = new DateOnly(2026, 10, 23),
            WeightKg = 82.0m
        };

        _repoMock.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var service = new WeightLogService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetLatestAsync();

        result.Success.Should().BeTrue();
        result.Data!.WeightKg.Should().Be(82.0m);
    }

    [Fact]
    public async Task GetLatestAsync_WhenNoneFound_ReturnsEntityNotFound()
    {
        _repoMock.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeightLogEntity?)null);

        var service = new WeightLogService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetLatestAsync();

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsHistoryList()
    {
        var start = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 10, 23);

        var entities = new List<WeightLogEntity>
        {
            new() { Date = new DateOnly(2026, 10, 5), WeightKg = 80.0m },
            new() { Date = new DateOnly(2026, 10, 20), WeightKg = 81.0m }
        };

        _repoMock.Setup(r => r.GetHistoryAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        var service = new WeightLogService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetHistoryAsync(start, end);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }
}
