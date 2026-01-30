namespace Core.Services
{
    public interface IService
    {
        void Initialize(ServiceContainer services);
        void Tick();
        void Dispose();
    }
}