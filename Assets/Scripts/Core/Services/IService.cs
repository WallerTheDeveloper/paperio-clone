namespace Core.Services
{
    public interface IService
    {
        void Initialize(ServiceContainer services);
        /// <summary>
        /// Called in Update() of highest class in chain.
        /// Not every Service is obligated to implement it
        /// </summary>
        void Tick() {}
        /// <summary>
        /// Called in LateUpdate() of highest class in chain.
        /// Not every Service is obligated to implement it
        /// </summary>
        void TickLate() {}
        void Dispose();
    }
}