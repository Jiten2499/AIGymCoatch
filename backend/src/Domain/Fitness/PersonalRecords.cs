namespace LifeFitAI.Domain.Fitness;

public static class PersonalRecords
{
    public static decimal? EpleyOneRepMax(decimal? weight, int? reps)
    {
        if (weight is not > 0 || reps is not > 0)
        {
            return null;
        }

        if (reps == 1)
        {
            return weight;
        }

        return Math.Round(weight.Value * (1m + (reps.Value / 30m)), 2, MidpointRounding.AwayFromZero);
    }
}
