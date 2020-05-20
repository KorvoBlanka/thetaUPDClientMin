using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UDPClient
{
    class Program
    {

        const ushort announcePort = 16061;
        //const string thetaNetName = "ttracker.local";

        const ushort thetaPort = 19920;
        const string thetaNetName = "ttracker.local";


        static byte[] rcvBuf;
        static BinaryReader rcvBufReader = new BinaryReader(new MemoryStream());
        static BinaryReader packetBufReader = new BinaryReader(new MemoryStream());


        static bool pageLost = false;
        static int prevPacketId = 0;
        static int packetId = 0;
        static int pagesTotal = 0;
        static int prevPage = 0;
        static int pageCur = 0;

        static void parsePacket(byte[] packet)
        {
            rcvBufReader.BaseStream.SetLength(0);                       // reset stream
            rcvBufReader.BaseStream.Write(packet, 0, packet.Length);    // write packet
            rcvBufReader.BaseStream.Position = 0;

            byte b1 = rcvBufReader.ReadByte();
            byte b2 = rcvBufReader.ReadByte();

            // confirmation packet
            if (b1 == 'o' && b2 == 'k')
            {
                return;
            }

            // data packet
            if (b1 != 'd' || b2 != 'p')
            {
                System.Console.WriteLine("bad packet");
                return;
            }

            //read header
            packetId = rcvBufReader.ReadUInt16();
            pageCur = rcvBufReader.ReadByte();
            pagesTotal = rcvBufReader.ReadByte();

            StringBuilder strBld = new StringBuilder(32);
            strBld.AppendFormat("p_id: {0:x2}; t: {1}; c: {2}", packetId, pagesTotal, pageCur);
            System.Console.WriteLine(strBld);

            if (packetId != prevPacketId)
            {
                System.Console.WriteLine("new packet!");
                pageLost = false;
                prevPage = -1;
                packetBufReader.BaseStream.SetLength(0);    // clear packet buffer
            }
            prevPacketId = packetId;
            packetBufReader.BaseStream.Write(packet, 6, packet.Length - 6); // write page to packet buffer, skip page header

            if (pageCur != prevPage + 1)
            {
                pageLost = true;
                System.Console.WriteLine("page lost!");
            }
            prevPage = pageCur;

            if (pagesTotal == pageCur + 1 && !pageLost)
            {
                packetBufReader.BaseStream.Position = 0;    // rewind

                byte[] opts = { 0, 0, 0 };
                ushort[] szs = { 0, 0, 0 };

                System.Console.WriteLine("parsing packet");
                for (int i = 0; i < 3; i++)
                {
                    opts[i] = packetBufReader.ReadByte();
                    System.Console.Write("" + opts[i] + " ");
                }
                System.Console.WriteLine();

                for (int i = 0; i < 3; i++)
                {
                    if (opts[i] > 0)
                    {
                        szs[i] = packetBufReader.ReadUInt16();
                    }
                    else
                    {
                        szs[i] = 0;
                    }
                    System.Console.Write("" + szs[i] + " ");
                }
                System.Console.WriteLine();

                // parse data block
                if (opts[0] > 0)
                {
                    //parseData(packet_buf);
                }
                // parse data_ext block
                if (opts[1] > 0)
                {
                    //parseDataExt(packet_buf);
                }
                // parse mapping block
                if (opts[2] > 0)
                {
                    //parseMapping(packet_buf, szs[2]);
                }
            }
        }


        public static void OnDataCallback(IAsyncResult result)
        {
            UdpClient socket = result.AsyncState as UdpClient;
            IPEndPoint source = new IPEndPoint(0, 0);
            byte[] data = socket.EndReceive(result, ref source);

            parsePacket(data);

            //Console.WriteLine("Got " + message.Length + " bytes from " + source);
            socket.BeginReceive(new AsyncCallback(OnDataCallback), socket);
        }


        public static void OnAnnounceCallback(IAsyncResult result)
        {
            UdpClient socket = result.AsyncState as UdpClient;
            IPEndPoint source = new IPEndPoint(0, 0);
            byte[] announce = socket.EndReceive(result, ref source);
            Console.WriteLine("Got " + announce.Length + " bytes from " + source);
            // parse json

            socket.BeginReceive(new AsyncCallback(OnAnnounceCallback), socket);
        }

        static void Main(string[] args)
        {
            //
            IPEndPoint announceAddress = new IPEndPoint(IPAddress.Any, announcePort);
            UdpClient announceClient = new UdpClient();
            announceClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            announceClient.ExclusiveAddressUse = false;
            announceClient.Client.Bind(announceAddress);
            announceClient.BeginReceive(new AsyncCallback(OnAnnounceCallback), announceClient);


            // drq - data request
            // 1 - data 
            // 1 - ext data (debug)
            // 1 - mapping, mapping will be sent once
            byte[] rqPacket = Encoding.ASCII.GetBytes("drq111");
            UdpClient thetaClient = new UdpClient();
            thetaClient.Connect(thetaNetName, thetaPort);

            // надо посылать drqnnn не реже 1 раз в 60 сек, чтобы тета не посчитала что клиент сдох
            thetaClient.BeginReceive(new AsyncCallback(OnDataCallback), thetaClient);

            // посылаем запрос по нажатию клавиши, данные будут приходить следующие 60 сек 
            // q для выхода
            while (Console.ReadKey().Key != ConsoleKey.Q)
            {
                thetaClient.Send(rqPacket, rqPacket.Length);
            }
        }
    }
}


// анонс выглядит следующим образом
// {"available_rev":"","cmd_srv_ver":"0.2.3","current_rev":"","data_mc_group":"239.255.255.252:6000","dbg_mc_group":"239.255.255.253:6001","info_mc_group":"239.255.255.251:5999","log_mc_group":"239.255.255.152:16062","theta_ver":"-1"}