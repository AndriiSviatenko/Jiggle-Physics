namespace ComponentLogic
{
    public interface ILateTickableComponent : IComponent
    {
        void LateTick(float deltaTime);
    }
}
