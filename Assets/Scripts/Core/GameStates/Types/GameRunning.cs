using System;
using System.Collections;
using Core.Services;
using Network;

namespace Core.GameStates.Types
{
    public class GameRunning : GameState
    {
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();

            if (!_messageSender.IsJoined)
            {
                StartCoroutine(WaitForJoinRoom());
            }
            IEnumerator WaitForJoinRoom()
            {
                while (!_messageSender.IsJoined)
                {
                    yield return null;
                }
                
                _messageSender.SendReady();
            }
        }

        public override void TickState()
        { }

        public override void Stop()
        { }
    }
}