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

        Thread recvThread;
        Thread sendThread;

        public IPAddress hostIP_;
        public Socket sender_;
        public IPEndPoint remoteEP_;
        public int port_;

        private bool senderSet_ = false;
        private bool remoteEPSet_ = false;
        private bool hostIPSet_ = false;
        private bool portSet_ = false;

        private bool encryptedMode_ = true;
        private bool temporaryNoEncryptMode_ = false;

        private bool connected_ = false;
        private bool readyToSend_ = false;
        private string msgToSend_;

        public bool Disconnect()
        {
            // Release the socket.

            if (recvThread.IsAlive)
                recvThread.Abort();
            if (sendThread.IsAlive)
                sendThread.Abort();

            sender_.Shutdown(SocketShutdown.Both);
            sender_.Close();

            connected_ = false;

            return true;
        }

        public bool IsConnected()
        {
            return connected_;
        }

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

        public void ActivateTemporaryNoEncryptMode()
        {
            temporaryNoEncryptMode_ = true;
        }

        public bool ToggleNoEncryptMode()
        {
            encryptedMode_ = !encryptedMode_;
            return encryptedMode_;
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
            msgToSend_ = message;
            readyToSend_ = true;
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

            string t;

            while (connected_)
            {
                try
                {
                    bytesRecv = sender_.Receive(recvBuffer);
                    if (encryptedMode_)
                    {
                        Decrypt(ref recvBuffer, bytesRecv);
                    }

                    t = Encoding.UTF8.GetString(recvBuffer, 0, bytesRecv);

                    if (t.Length > 1)
                        Console.WriteLine(t);
                }
                catch
                {
                    connected_ = false;
                }
            }
        }

        public void ThreadSend()
        {

            byte[] bytemsg = new byte[1024];

            int bytesSent;

            while (connected_)
            {
                if (readyToSend_)
                {

                    bytemsg = Encoding.UTF8.GetBytes(msgToSend_);
                    
                    if (encryptedMode_ && !temporaryNoEncryptMode_)
                    {
                        Encrypt(ref bytemsg, bytemsg.Length);
                    }
                    
                    Console.WriteLine(Encoding.UTF8.GetString(bytemsg));

                    try
                    {
                        bytesSent = sender_.Send(bytemsg);
                        readyToSend_ = false;
                        temporaryNoEncryptMode_ = false;
                    }
                    catch
                    {
                        connected_ = false;
                    }
                }
            }
        }

        public bool TryConnect()
        {
            bool success = false;

            Console.WriteLine("Attempting to connect...");

            if (senderSet_ && remoteEPSet_ && hostIPSet_ && portSet_)
            {
                try
                {
                    sender_.Connect(remoteEP_);

                    connected_ = true;

                    Console.WriteLine("Connected to " + hostIP_.ToString() + ":" + port_);

                    recvThread = new Thread(new ThreadStart(ThreadReceive));
                    sendThread = new Thread(new ThreadStart(ThreadSend));

                    recvThread.Start();
                    sendThread.Start();

                    success = true;
                    
                }
                catch (SocketException)
                {
                    Console.WriteLine("Error connecting to socket.");
                }
            }
            else
            {
                if (!hostIPSet_)
                {
                    Console.WriteLine("Host IP not set.");
                }
                if (!portSet_)
                {
                    Console.WriteLine("Port not set.");
                }
                if (!remoteEPSet_)
                {
                    Console.WriteLine("Remote endpoint not set");
                }
                if (!senderSet_)
                {
                    Console.WriteLine("Sender (socket) not set.");
                }
            }

            return success;
        }

        public Chat()
        {

        }

        ~Chat()
        {

        }
    }

    class Program
    {

        static Chat chat;
        static string userInput;

        static bool IsIP(string input)
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
            if (IsIP(input))
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


            if (!input.All(Char.IsDigit) || input.Length == 0)
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
            bool e = chat.ToggleNoEncryptMode();
            if (e)
                Console.WriteLine("Changed mode to encrypted r/w.");
            else
                Console.WriteLine("Changed mode to regular r/w.");
        }

        static void TemporaryNoEncryptWhisper()
        {
            chat.ActivateTemporaryNoEncryptMode();
        }

        static void TemporaryNoEncrypt()
        {
            userInput.Remove(0, 3);
            chat.ActivateTemporaryNoEncryptMode();
        }

        static void QuitChat()
        {
            chat.Disconnect();
        }

        static Dictionary<string, Action> settingCommands = new Dictionary<string, Action>
        {
            { ".mode", ChangeMode },
            { "quit()", QuitChat }
        };

        static Dictionary<string, Action> sendCommands = new Dictionary<string, Action>
        {
            { "/w", TemporaryNoEncryptWhisper },
            { "/noe", TemporaryNoEncrypt }
        };


        static bool HandleInput()
        {

            bool sendable = true;
            string key;

            if (userInput.Contains(' '))
            {
                key = userInput.Split(' ')[0];
            }
            else
            {
                key = userInput;
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
                // In the case that we have a 
                var action = sendCommands[key];
                action();
            }

            return sendable;
        }

        
        static void Main(string[] args)
        {

            chat = new Chat();
            
            bool going = true;
            bool sendableInput;

            while (going)
            {

                if (!chat.IsConnected())
                {
                    chat = new Chat();
                    NewChat(ref chat);
                    chat.TryConnect();
                }
                else
                {
                    userInput = Console.ReadLine();

                    // If we disconnect by time we enter our thing, it should not go further.
                    if (chat.IsConnected())
                    {
                        sendableInput = HandleInput();
                        if (sendableInput)
                        {
                            chat.SetMessage(userInput);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: not connected anymore.");
                    }
                }


            }
            
        }
    }
}
