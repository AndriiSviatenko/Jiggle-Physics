namespace ComponentLogic
{
    public interface IPlayableComponent : IComponent
    {
        void Play();
        void Stop();
    }
}
