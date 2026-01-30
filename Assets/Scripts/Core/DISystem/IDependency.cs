namespace Core.DISystem
{
    public interface IDependency
    {
        void Initialize();
        void Deinitialize();
        void Tick();
    }
}