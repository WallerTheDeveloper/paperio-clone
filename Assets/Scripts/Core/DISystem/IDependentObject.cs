namespace Core.DISystem
{
    public interface IDependentObject
    {
        void InjectDependencies(IDependencyProvider provider);
        void PostInjectionConstruct();
    }
}