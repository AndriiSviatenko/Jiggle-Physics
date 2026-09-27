namespace ComponentLogic
{
    public interface ITickableComponent : IComponent
    {
        void Tick(float deltaTime);
    }
}
