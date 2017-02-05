using System;
using System.Collections.Generic;
using System.Linq;
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
                Become(WaitingAckBehaviour);
            });
            Receive<AckReceived>(sm => {
                Console.WriteLine("unexpected ack");
            });
        }

        private void WaitingAckBehaviour()
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


            //dirty selection
            if (messageReceived.Message[3] == 0x02)
            {
                var nodeIds = new List<int>();
                var startPosition = 7;
                var currentPos = startPosition;
                var currentByte = messageReceived.Message[currentPos];
                while (currentByte > 0)
                {
                    var bin = 1;
                    for (var i = 1; i <= 8; i++)
                    {
                        if ((currentByte & bin) > 0)
                            nodeIds.Add(8*(currentPos - startPosition) + i);
                        bin *= 2;
                    }
                    currentPos++;
                    currentByte = messageReceived.Message[currentPos];
                }

                nodeIds.ForEach(id =>
                    {
                        var message = new byte[] {0x01, 0x05, 0x00, 0x41, (byte)id, 0x31};
                        Self.Tell(new SerialPortHandlerActor.SendMessage(message));
                    }
                );
            }


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
