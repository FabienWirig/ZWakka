using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using System.IO.Ports;

namespace ZWakka.Actors
{
    public class SerialPortHandlerActor : ReceiveActor, IWithUnboundedStash
    {
        private readonly SerialPort _serialPort;
        public IStash Stash { get; set; }

        #region Messages
        public class MessageReceived
        {
            public MessageReceived(byte[] message)
            {
                Message = message;
            }

            public byte[] Message { get; private set; }
        }

        public class SendMessage
        {
            public SendMessage(byte[] message)
            {
                Message = message;
            }

            public byte[] Message { get; private set; }
        }

        public class AckReceived {}

        #endregion

        public SerialPortHandlerActor(SerialPort serialPort)
        {
            _serialPort = serialPort;
            NormalBehaviour();
        }

        private void NormalBehaviour()
        {
            Receive<MessageReceived>(mr => HandleMessageReceived(mr));
            Receive<SendMessage>(sm =>
            {
                HandleSendMessage(sm);
                Become(WaitingAckBehaviourBehaviour);
            });
            Receive<AckReceived>(sm => {
                Console.WriteLine("unexpected ack");
            });
        }

        private void WaitingAckBehaviourBehaviour()
        {
            Receive<MessageReceived>(mr => Stash.Stash());
            Receive<SendMessage>(sm => Stash.Stash());
            Receive<AckReceived>(sm => {
                Become(NormalBehaviour);
                Stash.UnstashAll();
            });
        }

        private void HandleMessageReceived(MessageReceived messageReceived)
        {
            Console.WriteLine($"Message received : {getDisplayableString(messageReceived.Message)}");

            //TODO : route messages
        }

        private void HandleSendMessage(SendMessage sendMessage)
        {
            var message = new List<byte>(sendMessage.Message);
            message.Add(GenerateChecksum(message));

            Console.WriteLine($"Message sent : {getDisplayableString(message)}");

            _serialPort.Write(message.ToArray(), 0, message.Count);
        }

        private string getDisplayableString(IEnumerable<byte> message)
        {
            return string.Join(" ", message.Select(x => $"{x:x2}"));
        }

        private byte GenerateChecksum(List<byte> data)
        {
            var result = data[1];
            for (var i = 2; i < data.Count; i++)
            {
                result ^= data[i];
            }
            result = (byte)(~result);
            return result;
        }
    }
}
