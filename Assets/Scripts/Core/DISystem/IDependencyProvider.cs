namespace Core.DISystem
{
    public interface IDependencyProvider
    {
        T GetDependency<T>();
    }
}