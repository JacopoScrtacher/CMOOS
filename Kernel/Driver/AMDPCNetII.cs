﻿using MOOS.IOGroup.Network;
using MOOS.Memory;
using MOOS.Misc;
using MOOS.NET;
using System;
using System.Collections.Generic;
using System.Common;
using System.Diagnostics;
using System.Text;
using static MOOS.Misc.Interrupts;

namespace MOOS.Driver
{
    public unsafe class AMDPCNetII : NetworkDevice
    {
        protected PCIDevice pciCard;
        protected AMDPCNetIIIOGroup io;
        protected MACAddress mac;
        protected bool mInitDone;

        protected ManagedMemoryBlock mInitBlock;
        protected ManagedMemoryBlock mRxDescriptor;
        protected ManagedMemoryBlock mTxDescriptor;
        protected List<ManagedMemoryBlock> mRxBuffers;
        protected List<ManagedMemoryBlock> mTxBuffers;
        protected Queue<byte[]> mRecvBuffer;
        protected Queue<byte[]> mTransmitBuffer;
        private int mNextTXDesc;

        static AMDPCNetII instance;
        public AMDPCNetII(PCIDevice device) : base()
        {
            if (device == null)
            {
                Console.WriteLine("PCI Device is null. Unable to get AMD PCNet card");
                return;
            }

            this.pciCard = device;
            this.pciCard.Claimed = true;
            this.pciCard.EnableDevice();

            this.io = new AMDPCNetIIIOGroup((ushort)this.pciCard.BaseAddressBar[0].BaseAddress);
            this.io.RegisterData.DWord = 0;

            // Get the EEPROM MAC Address and set it as the devices MAC
            byte[] eeprom_mac = new byte[6];
            uint result = io.MAC1.DWord;

            eeprom_mac[0] = BinaryHelper.GetByteFrom32bit(result, 0);
            eeprom_mac[1] = BinaryHelper.GetByteFrom32bit(result, 8);
            eeprom_mac[2] = BinaryHelper.GetByteFrom32bit(result, 16);
            eeprom_mac[3] = BinaryHelper.GetByteFrom32bit(result, 24);
            result = io.MAC2.DWord;
            eeprom_mac[4] = BinaryHelper.GetByteFrom32bit(result, 0);
            eeprom_mac[5] = BinaryHelper.GetByteFrom32bit(result, 8);

            mac = new MACAddress(eeprom_mac);

            mInitBlock = new ManagedMemoryBlock(28, 4);
            mRxDescriptor = new ManagedMemoryBlock(256, 16);
            mTxDescriptor = new ManagedMemoryBlock(256, 16);

            mInitBlock.Write32(0x00, (0x4 << 28) | (0x4 << 20));
            mInitBlock.Write32(0x04, (uint)(eeprom_mac[0] | (eeprom_mac[1] << 8) | (eeprom_mac[2] << 16) | (eeprom_mac[3] << 24)));
            mInitBlock.Write32(0x08, (uint)(eeprom_mac[4] | (eeprom_mac[5] << 8)));
            mInitBlock.Write32(0x0C, 0x0);
            mInitBlock.Write32(0x10, 0x0);
            mInitBlock.Write32(0x14, (uint)mRxDescriptor.Offset);
            mInitBlock.Write32(0x18, (uint)mTxDescriptor.Offset);

            InitializationBlockAddress = (uint)mInitBlock.Offset;
            SoftwareStyleRegister = 0x03;

            mRxBuffers = new List<ManagedMemoryBlock>();
            mTxBuffers = new List<ManagedMemoryBlock>();
            for (uint rxd = 0; rxd < 16; rxd++)
            {
                uint xOffset = rxd * 16;

                ManagedMemoryBlock buffer = new ManagedMemoryBlock(2048);
                mRxDescriptor.Write32(xOffset + 8, (uint)buffer.Offset);
                ushort buffer_len = (ushort)(~buffer.Size);
                buffer_len++;
                uint flags = (uint)(buffer_len & 0x0FFF) | 0xF000 | 0x80000000;
                mRxDescriptor.Write32(xOffset + 4, flags);
                mRxBuffers.Add(buffer);
            }
            for (uint txd = 0; txd < 16; txd++)
            {
                uint xOffset = txd * 16;

                ManagedMemoryBlock buffer = new ManagedMemoryBlock(2048);
                mTxDescriptor.Write32(xOffset + 8, (uint)buffer.Offset);
                mTxBuffers.Add(buffer);
            }

            mNextTXDesc = 0;

            // Setup our Receive and Transmit Queues
            mTransmitBuffer = new Queue<byte[]>();
            mRecvBuffer = new Queue<byte[]>();

            instance = this;

            // Setup interrupt handling
            Interrupts.EnableInterrupt((byte)(0x20 + device.IRQ), &OnInterrupt);
        }

        internal static void OnInterrupt()
        {
            uint cur_status = instance.StatusRegister;

            if ((cur_status & 0x100) != 0)
            {
                instance.mInitDone = true;
            }
            if ((cur_status & 0x200) != 0)
            {
                if (instance.mTransmitBuffer.Count > 0)
                {
                    byte[] data = instance.mTransmitBuffer.Peek();
                    if (instance.SendBytes(ref data) == true)
                    {
                        instance.mTransmitBuffer.Dequeue();
                    }
                }
            }
            if ((cur_status & 0x400) != 0)
            {
                instance.ReadRawData();
            }

            instance.StatusRegister = cur_status;
            PIC.EoiSlave();
        }

        /// <summary>
        /// Retrieve all AMD PCNetII network cards found on computer.
        /// </summary>
        public static void FindAll()
        {
            Console.WriteLine("Scanning for AMD PCNetII cards...");
            PCIDevice device = PCI.GetDevice(VendorID.AMD, DeviceID.PCNETII);
            if (device != null)
            {
                AMDPCNetII nic = new AMDPCNetII((PCIDevice)device);

                Console.WriteLine("Found AMD PCNetII NIC on PCI " + device.Bus + ":" + device.Slot + ":" + device.Function);
                Console.WriteLine("NIC IRQ: " + device.IRQ);
                Console.WriteLine("NIC MAC Address: " + nic.MACAddress.ToString());
            }
        }

        #region Register Access Properties

        protected uint StatusRegister
        {
            get
            {
                io.RegisterAddress.DWord = 0x00;
                return io.RegisterData.DWord;
            }
            set
            {
                io.RegisterAddress.DWord = 0x00;
                io.RegisterData.DWord = value;
            }
        }

        protected uint InitializationBlockAddress
        {
            get
            {
                uint result;

                io.RegisterAddress.DWord = 0x01;
                result = io.RegisterData.DWord;
                io.RegisterAddress.DWord = 0x02;
                result |= (io.RegisterData.DWord << 16);

                return result;
            }
            set
            {
                io.RegisterAddress.DWord = 0x01;
                io.RegisterData.DWord = (value & 0xFFFF);
                io.RegisterAddress.DWord = 0x02;
                io.RegisterData.DWord = (value >> 16);
            }
        }

        protected uint SoftwareStyleRegister
        {
            get
            {
                io.RegisterAddress.DWord = 0x14;
                return io.BusData.DWord;
            }
            set
            {
                io.RegisterAddress.DWord = 0x14;
                io.BusData.DWord = value;
            }
        }

        #endregion

        #region Network Device Implementation

        public override MACAddress MACAddress
        {
            get { return mac; }
        }

        public override bool Ready
        {
            get { return mInitDone; }
        }

        public override CardType CardType => CardType.Ethernet;

        public override string Name => "PCNETII";

        public override bool Enable()
        {
            StatusRegister = 0x43;
            return true;
        }

        /// <summary>
        /// Add bytes to the transmit buffer queue.
        /// </summary>
        /// <param name="buffer">bytes array to queue.</param>
        /// <param name="offset">Offset of the data in the buffer.</param>
        /// <param name="length">Data length.</param>
        /// <returns>TRUE on success.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error.</exception>
        /// <exception cref="OverflowException">Thrown if length is bigger than Int32.MaxValue.</exception>
        public override bool QueueBytes(byte[] buffer, int offset, int length)
        {
            byte[] data = new byte[length];
            for (int b = 0; b < length; b++)
            {
                data[b] = buffer[b + offset];
            }

            if (SendBytes(ref data) == false)
            {
                mTransmitBuffer.Enqueue(data);
            }

            return true;
        }

        public override bool ReceiveBytes(byte[] buffer, int offset, int max)
        {
           // throw new NotImplementedException();
            Console.WriteLine("NotImplementedException");
            return false;
        }

        public override byte[] ReceivePacket()
        {
            if (mRecvBuffer.Count < 1)
            {
                return null;
            }

            byte[] data = mRecvBuffer.Dequeue();
            return data;
        }

        public override int BytesAvailable()
        {
            if (mRecvBuffer.Count < 1)
            {
                return 0;
            }

            return mRecvBuffer.Peek().Length;
        }

        public override bool IsSendBufferFull()
        {
            return false;
        }

        public override bool IsReceiveBufferFull()
        {
            return false;
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Send bytes to memory block.
        /// </summary>
        /// <param name="aData">A data to send.</param>
        /// <returns>TRUE on success.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error.</exception>
        /// <exception cref="OverflowException">Thrown if aData length is bigger than Int32.MaxValue.</exception>
        protected bool SendBytes(ref byte[] aData)
        {
            int txd = mNextTXDesc++;
            if (mNextTXDesc >= 16)
            {
                mNextTXDesc = 0;
            }

            uint xOffset = (uint)(txd * 16);
            uint status = mTxDescriptor.Read32(xOffset + 4);
            if ((status & 0x80000000) == 0)
            {
                for (uint b = 0; b < aData.Length; b++)
                {
                    mTxBuffers[txd][b] = aData[b];
                }
                //UInt16 buffer_len = (UInt16)(aData.Length < 64 ? 64 : aData.Length);
                ushort buffer_len = (ushort)aData.Length;
                buffer_len = (ushort)(~buffer_len);
                buffer_len++;

                uint flags = (uint)((buffer_len) & 0x0FFF) | 0x0300F000 | 0x80000000;

                mTxDescriptor.Write32(xOffset + 4, flags);

                return true;
            }

            return false;
        }

        private void ReadRawData()
        {
            uint status;
            ushort recv_size;
            byte[] recv_data;

            for (int rxd = 0; rxd < 16; rxd++)
            {
                uint xOffset = (uint)(rxd * 16);
                status = mRxDescriptor.Read32(xOffset + 4);
                if ((status & 0x80000000) == 0)
                {
                    recv_size = (ushort)(mRxDescriptor.Read32(xOffset + 0) & 0xFFF);

                    if (recv_size > 64) // remove checksum
                    {
                        recv_size -= 4;
                    }

                    recv_data = new byte[recv_size];
                    for (uint b = 0; b < recv_size; b++)
                    {
                        recv_data[b] = mRxBuffers[rxd][b];
                    }

                    if (DataReceived != null)
                    {
                        DataReceived(recv_data);
                    }
                    else
                    {
                        mRecvBuffer.Enqueue(recv_data);
                    }

                    mRxDescriptor.Write32(xOffset + 4, status | 0x80000000);
                }
            }
        }

        #endregion
    }
}
