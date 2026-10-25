using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class HomeMealPlanService : IHomeMealPlanService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<HomeMealPlanUpsertModel> _validator;
    private readonly ILogger<HomeMealPlanService> _logger;

    public HomeMealPlanService(
        IUnitOfWorkApp unitOfWork,
        ILogger<HomeMealPlanService> logger,
        IValidator<HomeMealPlanUpsertModel>? validator = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _validator = validator ?? new HomeMealPlanUpsertModelValidator();
    }

    public async Task<Result<HomeMealPlanModel>> GetPlanByWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = weekStartDate.HasValue
                ? TimeZoneDateHelper.GetWeekMonday(weekStartDate.Value)
                : TimeZoneDateHelper.GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

            var plan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);

            var overrides = await _unitOfWork.CafeteriaDayOverride.GetHistoryAsync(monday, monday.AddDays(6), cancellationToken);
            var cafeteriaFlags = overrides.ToDictionary(o => o.Date, o => o.HasCafeteria);

            var model = plan.ToModel(monday, cafeteriaFlags);
            return Result<HomeMealPlanModel>.Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar plano semanal para {WeekStartDate}", weekStartDate);
            return Result<HomeMealPlanModel>.Fail("Ocorreu um erro interno ao buscar o plano semanal.");
        }
    }

    public async Task<Result<HomeMealPlanModel>> UpsertPlanAsync(HomeMealPlanUpsertModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            if (model is null)
            {
                return Result<HomeMealPlanModel>.Fail("Os dados do plano semanal são obrigatórios.");
            }

            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<HomeMealPlanModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var monday = TimeZoneDateHelper.GetWeekMonday(model.WeekStartDate);
            if (monday != model.WeekStartDate)
            {
                return Result<HomeMealPlanModel>.Fail("A data de início do plano deve ser uma segunda-feira.");
            }

            var templateMap = new Dictionary<Guid, MealTemplateEntity>();
            var requestedTemplateUuids = model.Items.Select(i => i.MealTemplateUuid).Distinct();

            foreach (var templateUuid in requestedTemplateUuids)
            {
                var template = await _unitOfWork.MealTemplate.GetByUuidAsync(templateUuid, cancellationToken);
                if (template is null)
                {
                    return Result<HomeMealPlanModel>.Fail($"Template de refeição com ID '{templateUuid}' não foi encontrado.");
                }

                templateMap[templateUuid] = template;
            }

            _unitOfWork.BeginTransaction();

            var existingPlan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);
            int planId;

            if (existingPlan != null)
            {
                planId = existingPlan.Id;
                await _unitOfWork.HomeMealPlan.UpdateAsync(existingPlan, cancellationToken);
            }
            else
            {
                var newPlan = new HomeMealPlanEntity
                {
                    Uuid = Guid.NewGuid(),
                    WeekStartDate = monday
                };
                planId = await _unitOfWork.HomeMealPlan.CreateAsync(newPlan, cancellationToken);
            }

            var itemEntities = model.Items.Select(item => new HomeMealPlanItemEntity
            {
                Uuid = Guid.NewGuid(),
                HomeMealPlanId = planId,
                Date = item.Date,
                MealSlot = HomeMealSlots.Normalize(item.MealSlot),
                MealTemplateId = templateMap[item.MealTemplateUuid].Id
            }).ToList();

            await _unitOfWork.HomeMealPlan.ReplaceItemsAsync(planId, itemEntities, cancellationToken);
            _unitOfWork.Commit();

            return await GetPlanByWeekAsync(monday, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar plano semanal para {WeekStartDate}", model?.WeekStartDate);
            _unitOfWork.Rollback();
            return Result<HomeMealPlanModel>.Fail("Ocorreu um erro interno ao salvar o plano semanal.");
        }
    }

    public async Task<Result<bool>> DeletePlanAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await _unitOfWork.HomeMealPlan.GetByUuidAsync(uuid, cancellationToken);
            if (plan is null)
            {
                return Result<bool>.Fail("Plano semanal não encontrado.");
            }

            _unitOfWork.BeginTransaction();
            var deleted = await _unitOfWork.HomeMealPlan.DeleteAsync(uuid, cancellationToken);
            _unitOfWork.Commit();

            return Result<bool>.Ok(deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar plano semanal {Uuid}", uuid);
            _unitOfWork.Rollback();
            return Result<bool>.Fail("Ocorreu um erro interno ao deletar o plano semanal.");
        }
    }
}
