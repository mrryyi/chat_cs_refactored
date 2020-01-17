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

        private bool encryptedMode_ = false;
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

        // Sets the sender according to the IPAddress family as a stream over TCP.
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

        // Sets endpoint according to IP and port.
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
        
        // Sets the message to send to server and indicates to a thread that
        // checks if a message is ready to send that a message is ready to send.
        public void SetMessage(string message)
        {
            msgToSend_ = message;
            readyToSend_ = true;
        }

        // Encrypts bytes by adding value one to the message.
        private void Encrypt(ref byte[] msg, int maxBytes)
        {
            for (int i = 0; i < maxBytes; i++)
            {
                msg[i] += 1;
            }
        }

        // Decrypts messages only if they are not whisper,,
        // welcome, Enter, Weather or Username. This is made
        // to work with Vidars server: https://github.com/GrodVidar/Haxr-Chat
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

        // Receives messages and just writes to console.
        // Decrypts if decrypt mode.
        // UTF8 standard.
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
                    {
                        Console.Write(t);
                        Console.Write('\n');
                    }
                }
                catch
                {
                    connected_ = false;
                }
            }
        }

        // Sends messages if ready to send.
        // Encodes to UTF8.
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

        // Attempts to connect to a server.
        public bool TryConnect()
        {
            bool success = false;

            Console.WriteLine("Attempting to connect...");

            // Only if we succeeded to set the connection information.
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

        // Uses a regex pattern to find if the input is an IP address. Returns true if so.
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

        // Validates IP syntax and attempts to connect.
        // IP is either valid syntax, or if the non-IP input resolves
        // through a DNS to a valid IP, in which case we grab one of those IP addresses.
        // So we can write a garbage non-connectable IP and it be correct and return true.
        // It just means we have something we CAN attempt a connection to.
        static bool ValidateIP(string input, ref IPAddress addr)
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

        // Gets port from user. Default is 1234.
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

        // The user gets stuck here until the enter a valid IP and port.
        // Exits NewChat if we got a valid IP address.
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
                valid = ValidateIP(ipInput, ref addr);
                if (valid)
                {
                    port = GetPort();
                }

            } while (!valid);

            // Sets chat to attempt a connection.
            chat.SetPort(port);
            chat.SetIP(addr);
            chat.SetEndPoint();
            chat.SetSender();
        }

        
        // Changes between encryption and regular read/write.
        static void ChangeMode()
        {
            Console.WriteLine("Changed Mode.");
            bool e = chat.ToggleNoEncryptMode();
            if (e)
                Console.WriteLine("Changed mode to encrypted r/w.");
            else
                Console.WriteLine("Changed mode to regular r/w.");
        }

        // Sets encrypt mode to be off for the next message.
        static void TemporaryNoEncryptWhisper()
        {
            chat.ActivateTemporaryNoEncryptMode();
        }

        // Made for the "/noe" send command. Removes "/noe" and doesn't
        // encrypt the next message.
        static void TemporaryNoEncrypt()
        {
            userInput.Remove(0, 3);
            chat.ActivateTemporaryNoEncryptMode();
        }

        // Disconnects the chat.
        static void QuitChat()
        {
            chat.Disconnect();
        }

        // Commands for settings in which the key is a string found in user input, and the value is a function 
        // tied to the command.
        static Dictionary<string, Action> settingCommands = new Dictionary<string, Action>
        {
            { ".mode", ChangeMode },
            { "quit()", QuitChat },
            { ".quit", QuitChat },
            { "disconnect()", QuitChat },
            { ".disconnect", QuitChat }
        };

        // Commands in how to send next message in which the key is a string found in user input,
        //and the value is a function tied to the command.
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

                // If chat is not connected we try to create a new chat, with new IP and port.
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
                            // We set the message such that the sending thread will pick up the
                            // message and send it through the connection.
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
