namespace ComponentLogic
{
    public interface IFixedTickableComponent : IComponent
    {
        void FixedTick(float deltaTime);
    }
}
