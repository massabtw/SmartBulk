using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ILogger<ShoppingListService> _logger;

    public ShoppingListService(
        IUnitOfWorkApp unitOfWork,
        ILogger<ShoppingListService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ShoppingListModel?>> GetByWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = weekStartDate.HasValue
                ? TimeZoneDateHelper.GetWeekMonday(weekStartDate.Value)
                : TimeZoneDateHelper.GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

            var list = await _unitOfWork.ShoppingList.GetByWeekStartDateAsync(monday, cancellationToken);
            if (list is null)
            {
                return Result<ShoppingListModel?>.Ok(null);
            }

            return Result<ShoppingListModel?>.Ok(list.ToModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar lista de compras para semana {WeekStartDate}", weekStartDate);
            return Result<ShoppingListModel?>.Fail("Ocorreu um erro interno ao buscar a lista de compras.");
        }
    }

    public async Task<Result<ShoppingListModel>> GenerateFromPlanAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = weekStartDate.HasValue
                ? TimeZoneDateHelper.GetWeekMonday(weekStartDate.Value)
                : TimeZoneDateHelper.GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

            var plan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);
            if (plan is null || plan.Items == null || plan.Items.Count == 0)
            {
                return Result<ShoppingListModel>.Fail("Nenhuma refeição planejada encontrada para a semana informada.");
            }

            var plannedGramsByFood = new Dictionary<int, (FoodEntity Food, decimal PlannedGrams)>();

            foreach (var planItem in plan.Items)
            {
                if (planItem.MealTemplate?.Items == null) continue;

                foreach (var templateItem in planItem.MealTemplate.Items)
                {
                    if (templateItem.Food == null) continue;

                    var foodId = templateItem.FoodId;
                    if (plannedGramsByFood.TryGetValue(foodId, out var existing))
                    {
                        plannedGramsByFood[foodId] = (existing.Food, existing.PlannedGrams + templateItem.ServingGrams);
                    }
                    else
                    {
                        plannedGramsByFood[foodId] = (templateItem.Food, templateItem.ServingGrams);
                    }
                }
            }

            if (plannedGramsByFood.Count == 0)
            {
                return Result<ShoppingListModel>.Fail("O plano semanal não possui alimentos cadastrados nos templates.");
            }

            var itemEntities = new List<ShoppingListItemEntity>();
            foreach (var entry in plannedGramsByFood)
            {
                var food = entry.Value.Food;
                var plannedGrams = entry.Value.PlannedGrams;
                var rawYieldFactor = food.RawYieldFactor;

                var rawGrams = (rawYieldFactor.HasValue && rawYieldFactor.Value > 0)
                    ? Math.Round(plannedGrams / rawYieldFactor.Value, 2)
                    : plannedGrams;

                itemEntities.Add(new ShoppingListItemEntity
                {
                    Uuid = Guid.NewGuid(),
                    FoodId = entry.Key,
                    PlannedGrams = plannedGrams,
                    RawGrams = rawGrams,
                    IsPurchased = false
                });
            }

            _unitOfWork.BeginTransaction();

            var existingList = await _unitOfWork.ShoppingList.GetByWeekStartDateAsync(monday, cancellationToken);
            int listId;

            if (existingList != null)
            {
                listId = existingList.Id;
            }
            else
            {
                var newList = new ShoppingListEntity
                {
                    Uuid = Guid.NewGuid(),
                    WeekStartDate = monday
                };
                listId = await _unitOfWork.ShoppingList.CreateAsync(newList, cancellationToken);
            }

            await _unitOfWork.ShoppingList.SyncItemsAsync(listId, itemEntities, cancellationToken);
            _unitOfWork.Commit();

            var updatedList = await _unitOfWork.ShoppingList.GetByWeekStartDateAsync(monday, cancellationToken);
            return Result<ShoppingListModel>.Ok(updatedList!.ToModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar lista de compras para semana {WeekStartDate}", weekStartDate);
            _unitOfWork.Rollback();
            return Result<ShoppingListModel>.Fail("Ocorreu um erro interno ao gerar a lista de compras.");
        }
    }

    public async Task<Result<ShoppingListItemModel>> TogglePurchasedAsync(Guid itemUuid, bool isPurchased, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _unitOfWork.ShoppingList.GetItemByUuidAsync(itemUuid, cancellationToken);
            if (item is null)
            {
                return Result<ShoppingListItemModel>.Fail("Item da lista de compras não foi encontrado.");
            }

            _unitOfWork.BeginTransaction();
            var success = await _unitOfWork.ShoppingList.ToggleItemPurchasedAsync(itemUuid, isPurchased, cancellationToken);
            _unitOfWork.Commit();

            if (!success)
            {
                return Result<ShoppingListItemModel>.Fail("Não foi possível atualizar o status do item.");
            }

            var updated = await _unitOfWork.ShoppingList.GetItemByUuidAsync(itemUuid, cancellationToken);
            return Result<ShoppingListItemModel>.Ok(updated!.ToItemModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alternar status do item {ItemUuid}", itemUuid);
            _unitOfWork.Rollback();
            return Result<ShoppingListItemModel>.Fail("Ocorreu um erro interno ao atualizar o item da lista de compras.");
        }
    }

    public async Task<Result<bool>> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _unitOfWork.ShoppingList.GetByUuidAsync(uuid, cancellationToken);
            if (list is null)
            {
                return Result<bool>.Fail("Lista de compras não encontrada.");
            }

            _unitOfWork.BeginTransaction();
            var deleted = await _unitOfWork.ShoppingList.DeleteAsync(uuid, cancellationToken);
            _unitOfWork.Commit();

            return Result<bool>.Ok(deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar lista de compras {Uuid}", uuid);
            _unitOfWork.Rollback();
            return Result<bool>.Fail("Ocorreu um erro interno ao deletar a lista de compras.");
        }
    }
}

