using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class NutritionProfile : BaseEntity
{
    private NutritionProfile()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public decimal DailyCalorieTarget { get; private set; }

    public decimal ProteinTarget { get; private set; }

    public decimal CarbTarget { get; private set; }

    public decimal FatTarget { get; private set; }

    public DietaryPreference DietaryPreference { get; private set; }

    public int MealFrequency { get; private set; }

    public static NutritionProfile Create(Guid id, Guid userId, Guid tenantId) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            DailyCalorieTarget = 2000,
            ProteinTarget = 120,
            CarbTarget = 200,
            FatTarget = 65,
            DietaryPreference = DietaryPreference.NonVegetarian,
            MealFrequency = 3,
        };

    public void Update(decimal calories, decimal protein, decimal carbs, decimal fat, DietaryPreference preference, int meals)
    {
        DailyCalorieTarget = calories;
        ProteinTarget = protein;
        CarbTarget = carbs;
        FatTarget = fat;
        DietaryPreference = preference;
        MealFrequency = meals;
    }
}

public sealed class NutritionLog
{
    private NutritionLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly LogDate { get; private set; }

    public MealType MealType { get; private set; }

    public string FoodName { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    public decimal Calories { get; private set; }

    public decimal Protein { get; private set; }

    public decimal Carbohydrates { get; private set; }

    public decimal Fat { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static NutritionLog Create(Guid id, Guid userId, Guid tenantId, DateOnly date, MealType meal, string food, decimal quantity, string unit, decimal calories, decimal protein, decimal carbs, decimal fat, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            LogDate = date,
            MealType = meal,
            FoodName = food.Trim(),
            Quantity = quantity,
            Unit = unit.Trim(),
            Calories = calories,
            Protein = protein,
            Carbohydrates = carbs,
            Fat = fat,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
}

public sealed class AiConversation : BaseEntity
{
    private AiConversation()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public static AiConversation Open(Guid id, Guid userId, Guid tenantId, string title) =>
        new()
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            Title = title.Trim(),
        };

    public void TouchTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(Title) || Title == "New conversation")
        {
            Title = title.Length <= 80 ? title : title[..80];
        }
    }
}

public sealed class AiMessage
{
    private AiMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public AiMessageRole Role { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public int? TokenUsage { get; private set; }

    public string? Provider { get; private set; }

    public string? Model { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static AiMessage Create(Guid id, Guid conversationId, AiMessageRole role, string content, int? tokens, string? provider, string? model, DateTime utcNow) =>
        new()
        {
            Id = id,
            ConversationId = conversationId,
            Role = role,
            Content = content.Length <= 4000 ? content : content[..4000],
            TokenUsage = tokens,
            Provider = provider,
            Model = model,
            CreatedAt = utcNow,
        };
}

public sealed class AiUsageRecord
{
    private AiUsageRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public AiRequestType RequestType { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public int TotalTokens { get; private set; }

    public decimal EstimatedCost { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static AiUsageRecord Create(Guid id, Guid userId, Guid tenantId, string provider, string model, AiRequestType type, int inputTokens, int outputTokens, decimal costPerThousand, DateTime utcNow)
    {
        var total = inputTokens + outputTokens;
        return new AiUsageRecord
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            Provider = provider,
            Model = model,
            RequestType = type,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = total,
            EstimatedCost = Math.Round(total / 1000m * costPerThousand, 6, MidpointRounding.AwayFromZero),
            CreatedAt = utcNow,
        };
    }
}
