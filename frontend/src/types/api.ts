export interface ApiResponse<T> {
  success: boolean;
  message?: string | null;
  data: T;
  errors?: string[];
}

export interface UserSettingsModel {
  uuid?: string;
  calorieTargetKcal: number;
  proteinTargetGPerKg: number;
  timeZoneId: string;
  lunchReminderTime: string;
  dinnerReminderTime: string;
}

export interface WeightLogModel {
  uuid: string;
  date: string;
  weightKg: number;
}

export interface CreatineLogModel {
  uuid: string;
  date: string;
  taken: boolean;
}

export interface FoodModel {
  uuid: string;
  name: string;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  isCustom: boolean;
  isFavorite: boolean;
  rawYieldFactor?: number | null;
}

export interface MealLogItemModel {
  uuid: string;
  foodUuid: string;
  foodName: string;
  servingGrams: number;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

export interface MealLogModel {
  uuid: string;
  date: string;
  loggedAt: string;
  name: string;
  totalCalories: number;
  totalProtein: number;
  totalCarbohydrates: number;
  totalFat: number;
  items: MealLogItemModel[];
}

export interface CafeteriaDailyGuaranteedModel {
  date: string;
  hasCafeteria: boolean;
  hasConfirmedMenu: boolean;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

/** Espelha DailyGapOverviewModel do backend (GET /api/MealLog/daily-gap). */
export interface DailyGapModel {
  date: string;
  calorieTarget: number;
  proteinTarget: number;
  latestWeightKg?: number | null;
  weightWarning?: string | null;
  consumedCalories: number;
  consumedProtein: number;
  consumedCarbohydrates: number;
  consumedFat: number;
  cafeteriaCalories: number;
  cafeteriaProtein: number;
  cafeteriaCarbohydrates: number;
  cafeteriaFat: number;
  calorieGap: number;
  proteinGap: number;
  meals: MealLogModel[];
  cafeteria?: CafeteriaDailyGuaranteedModel | null;
}

export interface PagedResultModel<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface CafeteriaMealItemModel {
  uuid: string;
  rawName: string;
  servingGrams: number;
  isPending: boolean;
  foodUuid?: string;
  matchedFoodName?: string;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

export interface CafeteriaMealModel {
  uuid: string;
  date: string;
  mealType: string;
  plateProfile: string; // Standard, Good, Poor
  items: CafeteriaMealItemModel[];
}

export interface CafeteriaMenuModel {
  uuid: string;
  weekStartDate: string;
  status: string; // Draft, Confirmed
  meals: CafeteriaMealModel[];
}

export interface CafeteriaDayOverrideModel {
  uuid: string;
  date: string;
  hasCafeteria: boolean;
  reason?: string | null;
}

export interface MealTemplateItemModel {
  uuid: string;
  foodUuid: string;
  foodName: string;
  servingGrams: number;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

export interface MealTemplateModel {
  uuid: string;
  name: string;
  mealType: string;
  totalCalories: number;
  totalProtein: number;
  totalCarbohydrates: number;
  totalFat: number;
  items: MealTemplateItemModel[];
}

export interface ShakeAdjustedIngredientModel {
  foodUuid: string;
  foodName: string;
  originalGrams: number;
  scaledGrams: number;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

export interface ShakeExtraItemModel {
  foodUuid: string;
  foodName: string;
  grams: number;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  reason: string;
}

/** Espelha ShakeAdjustmentSuggestionModel do backend. */
export interface ShakeAdjustmentModel {
  noAdjustmentNeeded: boolean;
  scaleFactor: number;
  baseCalories: number;
  baseProtein: number;
  scaledCalories: number;
  scaledProtein: number;
  ingredients: ShakeAdjustedIngredientModel[];
  extraItem?: ShakeExtraItemModel | null;
  resultingCalorieGap: number;
  resultingProteinGap: number;
}

export interface HomeMealPlanItemModel {
  uuid: string;
  date: string;
  mealSlot: string;
  mealTemplateUuid: string;
  mealTemplateName: string;
  mealTemplateType: string;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
}

export interface HomeMealPlanDayModel {
  date: string;
  dayOfWeek: number;
  hasCafeteria: boolean;
  requiredSlots: string[];
  missingSlots: string[];
  isComplete: boolean;
  totalCalories: number;
  totalProtein: number;
  totalCarbohydrates: number;
  totalFat: number;
  items: HomeMealPlanItemModel[];
}

export interface HomeMealPlanModel {
  uuid: string;
  weekStartDate: string;
  isWeekComplete: boolean;
  totalCalories: number;
  totalProtein: number;
  totalCarbohydrates: number;
  totalFat: number;
  days: HomeMealPlanDayModel[];
}

export interface ShoppingListItemModel {
  uuid: string;
  foodUuid: string;
  foodName: string;
  plannedGrams: number;
  rawGrams: number;
  rawYieldFactor?: number | null;
  isPurchased: boolean;
}

export interface ShoppingListModel {
  uuid: string;
  weekStartDate: string;
  totalItems: number;
  purchasedItems: number;
  isAllPurchased: boolean;
  totalPlannedGrams: number;
  totalRawGrams: number;
  items: ShoppingListItemModel[];
}

