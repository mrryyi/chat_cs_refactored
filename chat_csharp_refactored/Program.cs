using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace chat_csharp_refactored
{
    class Chat
    {

        public IPAddress hostIP_;
        public Socket sender_;
        public IPEndPoint remoteEP_;
        public int port_;

        private bool senderSet_ = false;
        private bool remoteEPSet_ = false;
        private bool hostIPSet_ = false;
        private bool portSet_ = false;

        private bool encryptedMode = true;
        private bool temporaryNoEncryptMode = false;

        private bool chatting = false;
        private bool readyToSend = false;
        private string msgToSend;

        
        public void SetPort(int port)
        {
            port_ = port;
            portSet_ = true;
            Console.WriteLine("Port set.");
        }
        
        public void SetIP(IPAddress IP)
        {
            hostIP_ = IP;
            hostIPSet_ = true;
            Console.WriteLine("Host IP set.");
        }

        public void activateTemporaryNoEncryptMode()
        {
            temporaryNoEncryptMode = true;
        }

        public void toggleNoEncryptMode()
        {
            encryptedMode = !encryptedMode;
        }

        public void SetSender()
        {
            if (hostIPSet_) {
                sender_ = new Socket(hostIP_.AddressFamily,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);
                senderSet_ = true;

                Console.WriteLine("Socket set.");
            }
            else
            {
                Console.WriteLine("Host IP not set.");
            }
        }

        public void SetEndPoint()
        {
            if (hostIPSet_ && portSet_)
            {
                remoteEP_ = new IPEndPoint(hostIP_, port_);
                remoteEPSet_ = true;

                Console.WriteLine("Endpoint set.");
            }
            else
            {
                Console.WriteLine("Host IP or port not set.");
            }
        }
        
        public void SetMessage(string message)
        {
            msgToSend = message;
            readyToSend = true;
        }

        private void Encrypt(ref byte[] msg, int maxBytes)
        {
            for (int i = 0; i < maxBytes; i++)
            {
                msg[i] += 1;
            }
        }

        private void Decrypt(ref byte[] msg, int maxBytes)
        {

            if (maxBytes > 0)
            {
                String str = Encoding.UTF8.GetString(msg, 0, maxBytes);
                String[] words = str.Split(' ');

                if (words.Length > 1)
                {
                    if (!str.Contains("whisper") &&
                        words[0] != "welcome" &&
                        words[0] != "Enter" &&
                        !words[1].Contains("Weather") && 
                        words[0] != "Username")
                    {
                        int secondSpaceIndex = str.IndexOf(' ', str.IndexOf(' ') + 1);
                        for (int i = secondSpaceIndex + 1; i < maxBytes; i++)
                        {
                            if (msg[i] - 1 >= 0)
                            {
                                msg[i] -= 1;
                            }
                        }
                    }
                }
            }
        }

        public void ThreadReceive()
        {
            byte[] recvBuffer = new byte[1024];

            int bytesRecv;

            while (chatting)
            {
                try
                {
                    bytesRecv = sender_.Receive(recvBuffer);
                    if (encryptedMode)
                    {
                        Decrypt(ref recvBuffer, bytesRecv);
                    }
                    Console.WriteLine(Encoding.UTF8.GetString(recvBuffer, 0, bytesRecv));
                }
                catch
                {
                    Console.WriteLine("Server or socket error.");
                }
            }
        }

        public void ThreadSend()
        {

            byte[] bytemsg = new byte[1024];

            int bytesSent;

            while (chatting)
            {
                if (readyToSend)
                {

                    bytemsg = Encoding.UTF8.GetBytes(msgToSend);
                    
                    if (encryptedMode && !temporaryNoEncryptMode)
                    {
                        Encrypt(ref bytemsg, bytemsg.Length);
                    }
                    
                    Console.WriteLine(Encoding.UTF8.GetString(bytemsg));


                    bytesSent = sender_.Send(bytemsg);
                    readyToSend = false;
                    temporaryNoEncryptMode = false;
                }
            }
        }

        public bool TryConnect()
        {
            bool success = false;

            if (senderSet_ && remoteEPSet_ && hostIPSet_ && portSet_)
            {
                try
                {
                    sender_.Connect(remoteEP_);

                    chatting = true;

                    Thread recvThread = new Thread(new ThreadStart(ThreadReceive));
                    Thread sendThread = new Thread(new ThreadStart(ThreadSend));

                    recvThread.Start();
                    sendThread.Start();

                    success = true;
                }
                catch (SocketException)
                {
                    Console.WriteLine("Error connecting to socket.");
                }
            }

            return success;
        }

        public Chat()
        {

        }
    }

    class Program
    {

        static Chat chat;

        static bool isIP(string input)
        {
            string ipAddressPattern = "^(?=.*[^\\.]$)((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.?){4}$";
            Match match = Regex.Match(input, ipAddressPattern);

            if (match.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool GetIP(string input, ref IPAddress addr)
        {
            if (isIP(input))
            {
                Console.WriteLine(input + " is valid IP address.");
                addr = IPAddress.Parse(input);
            }
            else
            {
                Console.WriteLine("Is not IP Address, attempting DNS resolve... ");

                try
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(input);
                    addr = ipHostInfo.AddressList[0];
                    Console.WriteLine("Resolved to: " + addr.ToString());
                }
                catch 
                {
                    Console.WriteLine("Couldn't resolve hostname.");
                    return false;
                }

            }

            return true;
        }

        static int GetPort()
        {
            string input;
            int port = 1234;
            Console.WriteLine("Enter port: ");
           
            input = Console.ReadLine();


            if (!input.All(Char.IsDigit))
            {
                Console.WriteLine("Invalid. Setting port to default: " + port);
            }
            else
            {
                Int32.TryParse(input, out port);
            }

            return port;
        }

        static void NewChat(ref Chat chat)
        {
            bool valid = false;
            string ipInput;
            int port = 1234;

            IPAddress addr = null;

            do
            {
                
                Console.WriteLine("Enter an IP or hostname: ");
                ipInput = Console.ReadLine();
                valid = GetIP(ipInput, ref addr);
                if (valid)
                {
                    port = GetPort();
                }

            } while (!valid);

            chat.SetPort(port);
            chat.SetIP(addr);
            chat.SetEndPoint();
            chat.SetSender();
        }

        

        static void ChangeMode()
        {
            Console.WriteLine("Changed Mode.");
            chat.toggleNoEncryptMode();
        }

        static void TemporaryNoEncryptMode()
        {
            chat.activateTemporaryNoEncryptMode();
        }

        static Dictionary<string, Action> settingCommands = new Dictionary<string, Action>
        {
            { ".mode", ChangeMode }
        };

        static Dictionary<string, Action> sendCommands = new Dictionary<string, Action>
        {
            { "/w", TemporaryNoEncryptMode },
            { "/noe", TemporaryNoEncryptMode }
        };


        static bool HandleInput(string message)
        {

            bool sendable = true;
            string key;

            if (message.Contains(' '))
            {
                key = message.Split(' ')[0];
            }
            else
            {
                key = message;
            }

            // If we just want to do a setting, then don't send anything,
            // just do the setting.
            if (settingCommands.ContainsKey(key))
            {
                var action = settingCommands[key];
                action();
                sendable = false;
            }

            if (sendCommands.ContainsKey(key))
            {
                var action = sendCommands[key];
                action();
            }

            return sendable;
        }

        
        static void Main(string[] args)
        {

            chat = new Chat();

            bool chatActive = false;
            bool going = true;
            bool sendableInput;

            string message;

            while (going)
            {
                if (!chatActive)
                {
                    chat = new Chat();
                    NewChat(ref chat);
                    chatActive = chat.TryConnect();
                }
                else
                {
                    message = Console.ReadLine();
                    sendableInput = HandleInput(message);
                    if (sendableInput)
                    {
                        chat.SetMessage(message);
                    }
                }


            }
            
        }
    }
}
