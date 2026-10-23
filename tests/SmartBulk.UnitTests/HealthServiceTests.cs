using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class HealthServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock;
    private readonly Mock<IHealthRepository> _healthRepoMock;
    private readonly Mock<ILogger<HealthService>> _loggerMock;
    private readonly HealthService _service;

    public HealthServiceTests()
    {
        _uowMock = new Mock<IUnitOfWorkApp>();
        _healthRepoMock = new Mock<IHealthRepository>();
        _loggerMock = new Mock<ILogger<HealthService>>();

        _uowMock.Setup(u => u.Health).Returns(_healthRepoMock.Object);
        _service = new HealthService(_uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsAccessible_ReturnsHealthy()
    {
        _healthRepoMock.Setup(r => r.CanConnectAsync()).ReturnsAsync(true);
        _healthRepoMock.Setup(r => r.GetByKeyAsync("SystemStatus")).ReturnsAsync(new SystemControlEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            SystemKey = "SystemStatus",
            SystemValue = "Initialized",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        });

        var result = await _service.CheckHealthAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be("Healthy");
        result.Data.Database.Should().Be("Connected");
        result.Data.SystemStatus.Should().Be("Initialized");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCannotConnect_ReturnsFailure()
    {
        _healthRepoMock.Setup(r => r.CanConnectAsync()).ReturnsAsync(false);

        var result = await _service.CheckHealthAsync();

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.DatabaseConnectionError);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _healthRepoMock.Setup(r => r.CanConnectAsync()).ThrowsAsync(new InvalidOperationException("Connection failed"));

        var result = await _service.CheckHealthAsync();

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.DatabaseConnectionError);
    }
}

