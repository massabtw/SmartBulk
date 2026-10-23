using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class CreatineLogServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<ICreatineLogRepository> _repoMock = new();
    private readonly Mock<ILogger<CreatineLogService>> _loggerMock = new();

    public CreatineLogServiceTests()
    {
        _uowMock.Setup(u => u.CreatineLog).Returns(_repoMock.Object);
    }

    [Fact]
    public async Task SetCreatineAsync_UpsertsAndReturnsUpdatedModel()
    {
        var date = new DateOnly(2026, 10, 23);
        var savedEntity = new CreatineLogEntity
        {
            Id = 3,
            Uuid = Guid.NewGuid(),
            Date = date,
            Taken = true
        };

        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<CreatineLogEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _repoMock.Setup(r => r.GetByDateAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedEntity);

        var service = new CreatineLogService(_uowMock.Object, _loggerMock.Object);
        var result = await service.SetCreatineAsync(date, true);

        result.Success.Should().BeTrue();
        result.Data!.Taken.Should().BeTrue();
        _repoMock.Verify(r => r.UpsertAsync(It.Is<CreatineLogEntity>(e => e.Taken == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByDateAsync_WhenNoEntryExists_ReturnsModelWithTakenFalse()
    {
        var date = new DateOnly(2026, 10, 23);
        _repoMock.Setup(r => r.GetByDateAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatineLogEntity?)null);

        var service = new CreatineLogService(_uowMock.Object, _loggerMock.Object);
        var result = await service.GetByDateAsync(date);

        result.Success.Should().BeTrue();
        result.Data!.Taken.Should().BeFalse();
        result.Data.Date.Should().Be(date);
    }
}
