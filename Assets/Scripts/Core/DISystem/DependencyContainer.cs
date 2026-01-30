using System;
using System.Collections.Generic;

namespace Core.DISystem
{
    public class DependencyContainer : IDependencyContainer, IDependencyProvider
    {
        private readonly Dictionary<Type, List<IDependency>> _dependentObjectLinks = new();
        
        public void Register<TDependent>(TDependent dependent, List<IDependency> linkedDependencies) where TDependent : IDependentObject
        {
            if (_dependentObjectLinks.ContainsKey(typeof(TDependent)))
            {return;}
            
            _dependentObjectLinks.Add(typeof(TDependent), linkedDependencies);
            
            linkedDependencies.ForEach(dependency => dependency.Initialize());
            
            dependent.InjectDependencies(this);
            
            dependent.PostInjectionConstruct();
        }
        
        // from https://english.stackexchange.com/questions/25931/unregister-vs-deregister
        /*
         * *****"Unregister" vs "Deregister"*****
         * 
         * __Question:__
         * 
         * The concept of "undoing a registration" is widely used in my line of work.
         * While most dictionaries define unregister as the proper verb for it, several widely used and highly considered sources also use the verb deregister.
         * Do both verbs exist? Are they synonyms? Is there a slight difference in their meaning?
         *
         *
         * __Answer:__
         *
         *
         * This is a question that used to plague me for ages, until I finally sat down and thought it through.
         * 
         * As a programmer, I see both used a lot, and often interchangeably. For me, I like to think of the question by beginning with another question: What is the 'not registered' state called?
         * 
         * Let's assume you're a programmer, but keep in mind this is applies anywhere.
         * When you have a variable which represents some item that can be registered, what do you call the function to discover if it is registered?
         * In all likelihood, you'll call it 'isRegistered()'.
         * 
         * So in that sense, you make the problem into a boolean. i.e. is it registered, or is it NOT registered.
         * Then, from that logic, I believe your options simply become:
         * isRegistered() - false if the object is 'unregistered' - i.e. 'not registered' false == isRegistered().registerSomething() -
         * It has now moved from 'not registered' to 'registered'.deregisterSomething() - It has now moved from 'registered' to 'not registered'. i.e. 'unregistered'.
         *
         * This is why it's convention in programming to call an object that hasn't been 'initialised' as 'uninitialised', not 'deinitialised'.
         * This implies it was never initialised to begin with, so its initial state is 'uninitialised'.
         * If its initial state was called 'deinitialised' it would give the false impression that it was previously initialised.The bottom line for me is that you should define a convention for its use in your particular context, and stick to it.
         * The above convention is what I now use throughout my code.Urgh... Here is all of that in a single line ;)state=unregistered -> 'register' -> state=registered -> 'deregister' -> state=unregistered.
         *
         * -- Shane
         */
        
        public void Deregister<T>(IDependency dependency, List<IDependency> linkedDependencies)
        {
            if (!_dependentObjectLinks.ContainsKey(typeof(T)))
            {return;}
            
            linkedDependencies.ForEach(linkedDependency => linkedDependency.Deinitialize());

            _dependentObjectLinks.Remove(typeof(T));
        }
        
        public T GetDependency<T>()
        {
            foreach (var (key, dependencies) in _dependentObjectLinks)
            {
                foreach (var t in dependencies)
                {
                    if (t is T dependency)
                    {
                        return dependency;
                    }
                }
            }

            throw new InvalidOperationException($"Dependency of type {typeof(T)} not found. Please register it first.");
        }
        
        public void Tick(IDependency dependency)
        {
            dependency.Tick();
        }
    }
}