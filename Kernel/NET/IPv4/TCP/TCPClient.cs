﻿using MOOS.NET.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Helpers;
using System.Text;

namespace MOOS.NET.IPv4.TCP
{
    public class TcpClient //: IDisposable
    {
        /// <summary>
        /// Tcp State machine.
        /// </summary>
        public Tcp StateMachine;

        /// <summary>
        /// Create new instance of the <see cref="TcpClient"/> class.
        /// </summary>
        /// <param name="stateMachine">Tcp state machine.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="ArgumentException">Thrown if localPort already exists.</exception>
        internal TcpClient(Tcp stateMachine)
        {
            StateMachine = stateMachine;
        }

        /// <summary>
        /// Create new instance of the <see cref="TcpClient"/> class.
        /// </summary>
        /// <param name="localPort">Local port.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="ArgumentException">Thrown if localPort already exists.</exception>
      
        public TcpClient(int localPort)
        {
            StateMachine = new Tcp((ushort)localPort, 0, Address.Zero, Address.Zero);
            StateMachine.rxBuffer = new Queue<TCPPacket>(8);
            StateMachine.Status = Status.CLOSED;
        }
      
        /// <summary>
        /// Create new instance of the <see cref="TcpClient"/> class.
        /// </summary>
        /// <param name="dest">Destination address.</param>
        /// <param name="destPort">Destination port.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="ArgumentException">Thrown if TcpClient with localPort 0 exists.</exception>
        public TcpClient(Address dest, int destPort): this(0)
        {
            StateMachine.RemoteEndPoint.Address = dest;
            StateMachine.RemoteEndPoint.Port = (ushort)destPort;
        }

        /// <summary>
        /// Connect to client.
        /// </summary>
        /// <param name="dest">Destination address.</param>
        /// <param name="destPort">Destination port.</param>
        /// <exception cref="Exception">Thrown if TCP Status is not CLOSED.</exception>
        public bool Connect(Address dest, int destPort, int timeout = 5000)
        {
            if (StateMachine.Status == Status.ESTABLISHED)
            {
                //throw new Exception("Client must be closed before setting a new connection.");
                Console.WriteLine("Client must be closed before setting a new connection.");
                return false;
            }

            StateMachine.RemoteEndPoint.Address = dest;
            StateMachine.LocalEndPoint.Address = IPConfig.FindNetwork(dest);
            StateMachine.RemoteEndPoint.Port = (ushort)destPort;

            //Generate Random Sequence Number
            var rnd = new Random();
            var SequenceNumber = (uint)((rnd.Next(0, Int32.MaxValue)) << 32) | (uint)(rnd.Next(0, Int32.MaxValue));

            //Fill TCB
            StateMachine.TCB.SndUna = SequenceNumber;
            StateMachine.TCB.SndNxt = SequenceNumber;
            StateMachine.TCB.SndWnd = Tcp.TcpWindowSize;
            StateMachine.TCB.SndUp = 0;
            StateMachine.TCB.SndWl1 = 0;
            StateMachine.TCB.SndWl2 = 0;
            StateMachine.TCB.ISS = SequenceNumber;

            StateMachine.TCB.RcvNxt = 0;
            StateMachine.TCB.RcvWnd = Tcp.TcpWindowSize;
            StateMachine.TCB.RcvUp = 0;
            StateMachine.TCB.IRS = 0;

            Tcp.Connections.Add(StateMachine);

            StateMachine.SendEmptyPacket(Flags.SYN);
           
            StateMachine.Status = Status.SYN_SENT;

            if (StateMachine.WaitStatus(Status.ESTABLISHED, timeout) == false)
            {
               // throw new Exception("Failed to open TCP connection!");
                Console.WriteLine("Failed to open TCP connection!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Close connection.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="Exception">Thrown if TCP Status is CLOSED.</exception>
        public void Close()
        {
            if (StateMachine.Status == Status.ESTABLISHED)
            {
                StateMachine.SendEmptyPacket(Flags.FIN | Flags.ACK);

                StateMachine.TCB.SndNxt++;

                StateMachine.Status = Status.FIN_WAIT1;

                if (StateMachine.WaitStatus(Status.CLOSED, 5000) == false)
                {
                    //throw new Exception("Failed to close TCP connection!");
                    Console.WriteLine("Failed to close TCP connection!");
                    return;
                }
            }

            Tcp.RemoveConnection(StateMachine.LocalEndPoint.Port, StateMachine.RemoteEndPoint.Port, StateMachine.LocalEndPoint.Address, StateMachine.RemoteEndPoint.Address);
        }

        /// <summary>
        /// Send data to client.
        /// </summary>
        /// <param name="data">Data array to send.</param>
        /// <exception cref="Exception">Thrown if destination is null or destinationPort is 0.</exception>
        /// <exception cref="ArgumentException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="OverflowException">Thrown if data array length is greater than Int32.MaxValue.</exception>
        /// <exception cref="Sys.IO.IOException">Thrown on IO error.</exception>
        /// <exception cref="Exception">Thrown if TCP Status is not ESTABLISHED.</exception>
        public void Send(byte[] data)
        {
            if ((StateMachine.RemoteEndPoint.Address == null) || (StateMachine.RemoteEndPoint.Port == 0))
            {
                //throw new InvalidOperationException("Must establish a default remote host by calling Connect() before using this Send() overload");
                Console.WriteLine("Must establish a default remote host by calling Connect() before using this Send() overload");
                return;
            }
            if (StateMachine.Status != Status.ESTABLISHED)
            {
                //throw new Exception("Client must be connected before sending data.");
                Console.WriteLine("Client must be connected before sending data.");
                return;
            }

            if (data.Length > 536)
            {
                var chunks = ArrayHelper.ArraySplit(data, 536);

                for (int i = 0; i < chunks.Length; i++)
                {
                    var packet = new TCPPacket(StateMachine.LocalEndPoint.Address, StateMachine.RemoteEndPoint.Address, StateMachine.LocalEndPoint.Port, StateMachine.RemoteEndPoint.Port, StateMachine.TCB.SndNxt, StateMachine.TCB.RcvNxt, 20, i == chunks.Length - 2 ? (byte)(Flags.PSH | Flags.ACK) : (byte)(Flags.ACK), StateMachine.TCB.SndWnd, 0, chunks[i]);
                    OutgoingBuffer.AddPacket(packet);
                    NetworkStack.Update();

                    StateMachine.TCB.SndNxt += (uint)chunks[i].Length;
                }
            }
            else
            {
                var packet = new TCPPacket(StateMachine.LocalEndPoint.Address, StateMachine.RemoteEndPoint.Address, StateMachine.LocalEndPoint.Port, StateMachine.RemoteEndPoint.Port, StateMachine.TCB.SndNxt, StateMachine.TCB.RcvNxt, 20, (byte)(Flags.PSH | Flags.ACK), StateMachine.TCB.SndWnd, 0, data);
                OutgoingBuffer.AddPacket(packet);
                NetworkStack.Update();

                StateMachine.TCB.SndNxt += (uint)data.Length;
            }
        }

        /// <summary>
        /// Receive data from end point.
        /// </summary>
        /// <param name="source">Source end point.</param>
        /// <returns>byte array value.</returns>
        /// <exception cref="InvalidOperationException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="Exception">Thrown if TCP Status is not ESTABLISHED.</exception>
        public byte[] NonBlockingReceive(ref EndPoint source)
        {
            if (StateMachine.Status != Status.ESTABLISHED)
            {
                //throw new Exception("Client must be connected before receiving data.");
                Console.WriteLine("Client must be connected before receiving data.");
                return null;

            }
            if (StateMachine.rxBuffer.Count < 1)
            {
                return null;
            }

            var packet = StateMachine.rxBuffer.Dequeue();
            source.Address = packet.SourceIP;
            source.Port = packet.SourcePort;

            var tmp = StateMachine.Data;
            StateMachine.Data = null;
            return tmp;
        }

        /// <summary>
        /// Receive data from end point.
        /// </summary>
        /// <param name="source">Source end point.</param>
        /// <returns>byte array value.</returns>
        /// <exception cref="InvalidOperationException">Thrown on fatal error (contact support).</exception>
        /// <exception cref="Exception">Thrown if TCP Status is not ESTABLISHED.</exception>
        public byte[] Receive(ref EndPoint source)
        {
            while (StateMachine.rxBuffer.Count < 1)
            {
                if (StateMachine.Status != Status.ESTABLISHED)
                {
                    Console.WriteLine("Client must be connected before receiving data.");
                    return null;
                }
                Native.Hlt();
            }
 
            var packet = StateMachine.rxBuffer.Dequeue();
            source.Address = packet.SourceIP;
            source.Port = packet.SourcePort;

            byte[] tmp = new byte[StateMachine.Data.Length];

            for (int i = 0; i < tmp.Length; i++)
            {
                tmp[i] = StateMachine.Data[i];
            }

            StateMachine.Data.Dispose();

            return tmp;
        }

        /// <summary>
        /// Get distant computer EndPoint (IP adress and port).
        /// </summary>
        /// <returns>Remote EndPoint.</returns>
        public EndPoint RemoteEndPoint
        {
            get
            {
                return StateMachine.RemoteEndPoint;
            }
        }

        /// <summary>
        /// Get local computer EndPoint (IP adress and port).
        /// </summary>
        /// <returns>Remote EndPoint.</returns>
        public EndPoint LocalEndPoint
        {
            get
            {
                return StateMachine.LocalEndPoint;
            }
        }

        /// <summary>
        /// Is TCP Connected.
        /// </summary>
        /// <returns>Boolean value.</returns>
        public bool IsConnected()
        {
            return StateMachine.Status == Status.ESTABLISHED;
        }

        public override void Dispose()
        {
            Close();
            base.Dispose();
        }
    }
}
