using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using IoTServer.Servers;
using DTS.Helpers;
using DTS.CreateData;
using System.Diagnostics;

namespace DTS
{
    /// <summary>
    /// ModBusTcp 服务端模拟
    /// </summary>
    public class ModBusTcpServer : ServerSocketBase
    {
        private Socket _socketServer;
        public string _ip;
        public int _port;
        private List<Socket> _sockets = new List<Socket>();
        public bool connectsuccessflag;
        public string equipnum;
        public bool startFlag;

        public ModBusTcpServer(int port, string ip = null)
        {
            this._ip = ip;
            this._port = port;
            connectsuccessflag = false;
        }

        public ModBusTcpServer()
        {
            string name = Dns.GetHostName();
            IPAddress[] ipadrlist = Dns.GetHostAddresses(name);
            foreach (IPAddress ip in ipadrlist)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    this._ip = ip.ToString();
                    break;
                }
            }
            connectsuccessflag = false;
        }

        public void Init(string ip)
        {
            this._ip = ip;
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            _socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ipaddress = string.IsNullOrWhiteSpace(_ip) ? IPAddress.Any : IPAddress.Parse(_ip);
            IPEndPoint ipEndPoint = new IPEndPoint(ipaddress, _port);
            _socketServer.Bind(ipEndPoint);
            _socketServer.Listen(10);
            connectsuccessflag = true;
            startFlag = true;

            Task.Factory.StartNew(() => { Accept(_socketServer); });
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            startFlag = false;
            connectsuccessflag = false;
            if (_socketServer?.Connected ?? false) _socketServer.Shutdown(SocketShutdown.Both);
            _socketServer?.Close();                       
        }

        /// <summary>
        /// 客户端连接到服务端
        /// </summary>
        /// <param name="socket"></param>
        void Accept(Socket socket)
        {
            while (startFlag)
            {
               // if (connectsuccessflag)
                {
                    try
                    {
                        Socket newSocket = null;
                        try
                        {
                            newSocket = socket.Accept();
                            _sockets.Add(newSocket);
                        }
                        catch (SocketException ex)
                        {
                            foreach (var item in _sockets)
                            {
                                if (item.Connected) item.Shutdown(SocketShutdown.Both);
                                item.Close();
                            }
                        }
                        Task.Factory.StartNew(() => { Receive(newSocket); });
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.Interrupted)
                            throw ex;
                    }
                }
            }
        }

        /// <summary>
        /// 接收客户端发送的消息
        /// </summary>
        /// <param name="newSocket"></param>
        void Receive(Socket newSocket)
        {
            int oldadd = 0;
            int newadd = 0;
            RegistData rd = null;
            while (newSocket!=null && newSocket.Connected)
            {                
                try
                {                    
                    byte[] requetData1 = new byte[8];     //Map报文头+功能码               
                    requetData1 = SocketRead(newSocket, requetData1.Length);
                    byte[] requetData2 = new byte[requetData1[5] - 2];       //数据
                    requetData2 = SocketRead(newSocket, requetData2.Length);
                    var requetData = requetData1.Concat(requetData2).ToArray();      //完整的数据请求报文
                    byte[] responseData1 = new byte[8];
                    Buffer.BlockCopy(requetData, 0, responseData1, 0, responseData1.Length);
                    string stationNumberKey = string.Format("{0}", requetData[6]);//站号
                    var address = requetData[8] * 256 + requetData[9];//寄存器地址
                    var registerLenght = requetData[10] * 256 + requetData[11];//寄存器的长度                    
                    switch (requetData[7])   //功能码
                    {                       
                        //读保持寄存器
                        case 3:
                            {
                                /*
                                //当前位置到最后的长度
                                responseData1[4] = (byte)((3 + registerLenght * 2) / 256);
                                responseData1[5] = (byte)((3 + registerLenght * 2) % 256);
                                byte[] responseData2 = new byte[1 + registerLenght * 2];
                                responseData2[0] = (byte)(registerLenght * 2);
                                for (int i = 1; i < responseData2.Length; i++)
                                    responseData2[i] = 0xff;
                                if (address >= 0 && address < 6000)
                                {
                                    RegistData rd = RegistDatas.Create().PosRegistdata(stationNumberKey);
                                    if (rd != null)
                                    {
                                        byte[] byteArray = rd.holdRegistData;
                                        Buffer.BlockCopy(byteArray, address * 2, responseData2, 1, registerLenght * 2);

                                        var responseData = responseData1.Concat(responseData2).ToArray();
                                        newSocket.Send(responseData);
                                    }                                    
                                }  
                                */
                            }
                            break;
                        //读输入寄存器
                        case 4:
                            {        
                                //当前位置到最后的长度
                                responseData1[4] = (byte)((3 + registerLenght * 2) / 256);
                                responseData1[5] = (byte)((3 + registerLenght * 2) % 256);
                                byte[] responseData2 = new byte[1 + registerLenght * 2];
                                responseData2[0] = (byte)(registerLenght * 2);
                                for (int i = 1; i < responseData2.Length; i++)
                                    responseData2[i] = 0xff;                                    
                                if (address >= 0 && address < 65536)
                                {
                                    newadd = address;
                                    rd = ServerStart.Create().PosRegistdata(equipnum, stationNumberKey);
                                    if (rd != null)
                                    {
                                        byte[] byteArray = rd.inputRegistData;
                                            
                                        if (address * 2 < byteArray.Length)
                                        {
                                            if ((address + registerLenght) * 2 <= byteArray.Length)
                                            {
                                                Buffer.BlockCopy(byteArray, address * 2, responseData2, 1, registerLenght * 2);
                                            }
                                            else
                                            {
                                                int len = ((address + registerLenght) * 2) - byteArray.Length;
                                                byte[] b = new byte[len];
                                                for (int i = 0; i < len; i++)
                                                    b[i] = 0xff;

                                                byte[] temp = byteArray.Concat(b).ToArray();
                                                Buffer.BlockCopy(temp, address * 2, responseData2, 1, registerLenght * 2);
                                            }                                                
                                            var responseData = responseData1.Concat(responseData2).ToArray();
                                            newSocket.Send(responseData);

                                            oldadd = address;
                                                
                                        }
                                    }
                                }                                
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    connectsuccessflag = false;
                    if (newSocket?.Connected ?? false) newSocket?.Shutdown(SocketShutdown.Both);
                     newSocket?.Close();
                }
            }
        }
    }
}
