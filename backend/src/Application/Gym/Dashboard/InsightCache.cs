namespace LifeFitAI.Application.Gym.Dashboard;

public interface IBusinessInsightCache
{
    bool TryGet(string key, out AiInsightSection? value);

    void Set(string key, AiInsightSection value);
}
