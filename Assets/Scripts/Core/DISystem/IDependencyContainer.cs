using System.Collections.Generic;

namespace Core.DISystem
{
    public interface IDependencyContainer
    {
        void Register<TDependent>(TDependent dependent, List<IDependency> linkedDependencies)
            where TDependent : IDependentObject;
        void Deregister<T>(IDependency dependency, List<IDependency> linkedDependencies);
        void Tick(IDependency dependency);
        
        // void TickAll();
    }
}