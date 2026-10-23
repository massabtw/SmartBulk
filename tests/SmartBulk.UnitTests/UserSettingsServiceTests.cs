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

public class UserSettingsServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IUserSettingsRepository> _repoMock = new();
    private readonly IValidator<UserSettingsModel> _validator = new UserSettingsModelValidator();
    private readonly Mock<ILogger<UserSettingsService>> _loggerMock = new();

    public UserSettingsServiceTests()
    {
        _uowMock.Setup(u => u.UserSettings).Returns(_repoMock.Object);
    }

    [Fact]
    public async Task GetAsync_WhenSettingsExist_ReturnsSettingsSuccessfully()
    {
        var existing = new UserSettingsEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            CalorieTargetKcal = 3100,
            ProteinTargetGPerKg = 2.1m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = new TimeSpan(12, 30, 0),
            DinnerReminderTime = new TimeSpan(19, 30, 0)
        };

        _repoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = new UserSettingsService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.CalorieTargetKcal.Should().Be(3100);
        result.Data.ProteinTargetGPerKg.Should().Be(2.1m);
    }

    [Fact]
    public async Task GetAsync_WhenSettingsDoNotExist_CreatesDefaultSettingsAndReturnsThem()
    {
        _repoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserSettingsEntity?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<UserSettingsEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new UserSettingsService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.CalorieTargetKcal.Should().Be(3000);
        result.Data.ProteinTargetGPerKg.Should().Be(2.0m);
        result.Data.LunchReminderTime.Should().Be("12:30");
        result.Data.DinnerReminderTime.Should().Be("19:30");
        result.Data.TimeZoneId.Should().Be("America/Sao_Paulo");

        _repoMock.Verify(r => r.CreateAsync(It.Is<UserSettingsEntity>(e => e.CalorieTargetKcal == 3000), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenExceptionOccurs_ReturnsFailResult()
    {
        _repoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB Down"));

        var service = new UserSettingsService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetAsync();

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.UnknownError);
    }

    [Fact]
    public async Task UpdateAsync_WhenModelIsInvalid_ReturnsValidationErrorsWithoutUpdating()
    {
        var invalidModel = new UserSettingsModel
        {
            CalorieTargetKcal = -50,
            ProteinTargetGPerKg = 0,
            TimeZoneId = "",
            LunchReminderTime = "bad",
            DinnerReminderTime = "bad"
        };

        var service = new UserSettingsService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateAsync(invalidModel);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<UserSettingsEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<UserSettingsEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenSettingsExist_UpdatesAndReturnsUpdatedModel()
    {
        var current = new UserSettingsEntity
        {
            Id = 10,
            Uuid = Guid.NewGuid(),
            CalorieTargetKcal = 3000,
            ProteinTargetGPerKg = 2.0m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = new TimeSpan(12, 30, 0),
            DinnerReminderTime = new TimeSpan(19, 30, 0)
        };

        var updateModel = new UserSettingsModel
        {
            CalorieTargetKcal = 3500,
            ProteinTargetGPerKg = 2.4m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = "12:00",
            DinnerReminderTime = "20:00"
        };

        var updatedEntity = new UserSettingsEntity
        {
            Id = 10,
            Uuid = current.Uuid,
            CalorieTargetKcal = 3500,
            ProteinTargetGPerKg = 2.4m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = new TimeSpan(12, 0, 0),
            DinnerReminderTime = new TimeSpan(20, 0, 0)
        };

        _repoMock.SetupSequence(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(current)
            .ReturnsAsync(updatedEntity);

        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<UserSettingsEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new UserSettingsService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateAsync(updateModel);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.CalorieTargetKcal.Should().Be(3500);
        result.Data.ProteinTargetGPerKg.Should().Be(2.4m);
        result.Data.LunchReminderTime.Should().Be("12:00");
        result.Data.DinnerReminderTime.Should().Be("20:00");

        _repoMock.Verify(r => r.UpdateAsync(It.Is<UserSettingsEntity>(e => e.Id == 10 && e.CalorieTargetKcal == 3500), It.IsAny<CancellationToken>()), Times.Once);
    }
}
