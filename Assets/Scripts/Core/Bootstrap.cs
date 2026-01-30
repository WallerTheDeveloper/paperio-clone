using Core.GameStates;
using Game;
using MonoSingleton;
using Network;
using UnityEngine;

namespace Core
{
    public class Bootstrap : MonoSingleton<Bootstrap>
    {
        [SerializeField] private MessageSender messageSender;
        [SerializeField] private GameStatesManager gameStatesManager;
        
        private void Awake()
        {
            MonoSingletonRegistry.InitializeSingletonsOnScene();

            ISystem[] gameSystems =
            {
                messageSender,
                gameStatesManager
            };
            
            foreach (var system in gameSystems)
            {
                system.Initialize();
            }
            
            foreach (var system in gameSystems)
            {
                system.Run();
            }
        }
    }
}