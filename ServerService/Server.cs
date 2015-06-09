﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedClasses;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;
using System.Configuration;
using System.ServiceModel;

namespace ServerService
{
    class Server
    {
        private Thread _connectionThread;
        private System.Diagnostics.EventLog events;
        private DBConnect db;
        public static List<String> clientList = new List<String>();

        private String ip = ConfigurationManager.AppSettings["listenerIP"];//"127.0.0.1";
        private int port = Convert.ToInt32(ConfigurationManager.AppSettings["listenerPort"]);//9191;
        
        private object ReceiveObject(Socket sock)
        {
            if (!sock.Connected) return null;
            NetworkStream stream = new NetworkStream(sock);
            IFormatter formatter = new BinaryFormatter();
            try
            {
                object o = (object)formatter.Deserialize(stream);
                return o;
            }
            catch (SocketException)
            {
                return null;
            }
            catch (IOException e)
            {
                if (e.InnerException is SocketException)
                {
                    IPEndPoint ipep = sock.RemoteEndPoint as IPEndPoint;
                    String ip = ipep.Address.ToString();
                    new DBConnect().addLogs("Client " + ip + " forcibly closed connection");
                    //events.WriteEntry("Client " + ip + " forcibly closed connection", System.Diagnostics.EventLogEntryType.Warning);
                    return null;
                }
            }
            return null;
        }

        private void connectionThreadFunc()
        {
            //Packet pck = null;
            IPAddress ipAddress = IPAddress.Parse(ip);
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(localEndPoint);
           // Console.WriteLine("Bind created");
            new DBConnect().addLogs("[" + ip + ":" + port + "] Bind created");
            listener.Listen(10);
            DBConnect db = new DBConnect();
            Console.WriteLine("[" + ip + ":" + port + "] Listening...");
            //db.addLogs("Listening");

            while(true){
                try
                {
                    Socket handler;
                    handler = listener.Accept();
                    Console.WriteLine("Connected before Thread");
                    //if (isConnected) connector.SendLiverEvent("Connected");
                    Thread clientService = new Thread(() => clientServiceThreadFunc(handler));
                    clientService.Start();
                }
                catch (SocketException se)
                {
                    new DBConnect().addLogs(se.ToString());
                   // Console.WriteLine(se.ToString());
                }
            }
        }

        private void clientServiceThreadFunc(Socket s)
        {
            IPEndPoint ipep = s.RemoteEndPoint as IPEndPoint;
            String ip = ipep.Address.ToString();
            clientList.Add(ip);
            /* obsługa każdego klienta - odczyt obiektów z socketa (funkcja ReceiveObject(Socket sock) )*/
          //  Console.WriteLine("[" + ip + "]Connected in Thread");
            new DBConnect().addLogs("[" + ip + "]" + "Connected in Thread");
            while (true)
            {
                Packet pck = null;
                pck = (Packet)ReceiveObject(s);
                if (pck == null)
                {
                    s.Close();
                    break;
                }
                if (pck.IsExitPacket())
                {
                    s.Close();
                    break;
                }
                Console.WriteLine(pck.getString());
                db.addPacket(pck, ip);
            }
           // Console.WriteLine("[" + ip + "]Thread Ended");
            new DBConnect().addLogs("[" + ip + "]Thread Ended");
            clientList.Remove(ip);
        }

        private void WCFThreadFunc()
        {
            
        }
        public Server(System.Diagnostics.EventLog events)
        {
            this.events = events;
            Console.WriteLine(ip + ":" + port);
            db = new DBConnect();
            _connectionThread = new Thread(connectionThreadFunc);
            _connectionThread.Name = "Server connection thread";
            _connectionThread.Start();
        }


    }
}
