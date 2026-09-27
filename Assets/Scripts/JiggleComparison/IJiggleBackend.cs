namespace JiggleComparison
{

    public interface IJiggleBackend
    {
        string DisplayName { get; }
        string Summary { get; }
        bool IsAvailable { get; }
        bool IsActive { get; }
        float LastCostMilliseconds { get; }
        void SetActive(bool active);
        void ResetSimulation();
    }
}
