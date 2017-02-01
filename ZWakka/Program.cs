using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Akka.Actor;
using ZWakka.Actors;

namespace ZWakka
{
    internal class Program
    {
        public static ActorSystem MyActorSystem;
        private readonly Queue<byte> _readBuffer = new Queue<byte>();
        private List<byte> _currentMessage = new List<byte>();
        private readonly IActorRef _serialPortHandlerActor;

        private static void Main(string[] args)
        {
            var p = new Program();
        }

        public Program()
        {
            var serialPort = new SerialPort
            {
                PortName = "COM6",
                BaudRate = 115200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = System.Environment.NewLine
            };

            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.Open();
            
            MyActorSystem = ActorSystem.Create("MyActorSystem");
            _serialPortHandlerActor = MyActorSystem.ActorOf(Props.Create(() => new SerialPortHandlerActor(serialPort)));
            _serialPortHandlerActor.Tell(new SerialPortHandlerActor.SendMessage(new byte[] { 0x01, 0x03, 0x00, 0x20 }));

            MyActorSystem.WhenTerminated.Wait();
            serialPort.Close();
        }

        private void TrameReceived(SerialPort sp, byte[] message)
        {
            sp.Write(new byte[] { 0x06 }, 0, 1);
            _serialPortHandlerActor.Tell(new SerialPortHandlerActor.MessageReceived(message));
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort) sender;
            ReadBytes(sp).ToList().ForEach(x => _readBuffer.Enqueue(x));
            while (_readBuffer.Count != 0)
            {
                ProcessReceivedBytes(sp);
            }
        }

        private byte[] ReadBytes(SerialPort port)
        {
            byte[] result;
            if (port.IsOpen)
            {
                result = new byte[port.BytesToRead];
                if (result.Length > 0)
                {
                    port.Read(result, 0, result.Length);
                }
            }
            else
            {
                result = new byte[0];
            }
            return result;
        }

        private void ProcessReceivedBytes(SerialPort sp)
        {
            if (_readBuffer.Count > 0) _currentMessage = BuildMessage(_readBuffer, _currentMessage);
            
            byte[] result;
            if (_currentMessage.Count > 1 && _currentMessage.Count == _currentMessage[1] + 2)
            {
                result = _currentMessage.ToArray();
            }
            else if (_currentMessage.Count == 1 && (_currentMessage[0] == 0x06 || _currentMessage[0] == 0x15))
            {
                result = _currentMessage.ToArray();
            }
            else
            {
                result = new byte[0];
            }
            
            if (result.Length != 0)
            {
                _currentMessage = new List<byte>();

                if (result[0] == 0x15) Console.WriteLine("Nack received !!!");
                else if (result[0] == 0x06)
                {
                    _serialPortHandlerActor.Tell(new SerialPortHandlerActor.AckReceived());
                } else
                {
                    TrameReceived(sp, result);
                }
            }
        }
        private List<byte> BuildMessage(Queue<byte> readBufer, List<byte> currentMessage)
        {
            if (currentMessage.Count == 0)
            {
                currentMessage.Add(readBufer.Dequeue());
            }
            
            if (currentMessage.First() == 0x01)
            {
                if (currentMessage.Count == 1 && readBufer.Count >= 1)
                {
                    currentMessage.Add(readBufer.Dequeue());
                }
                
                for (var i = 0; i < readBufer.Count && currentMessage.Count != currentMessage[1] + 2; i++)
                {
                    currentMessage.Add(readBufer.Dequeue());
                }
            }
            return currentMessage;
        }
    }
}
