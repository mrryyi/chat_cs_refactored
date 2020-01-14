using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace chat_server
{

    public static class MyExtensions
    {
        public static void RenameKey<TKey, TValue>(this IDictionary<TKey, TValue> dic,
                                      TKey fromKey, TKey toKey)
        {
            TValue value = dic[fromKey];
            dic.Remove(fromKey);
            dic[toKey] = value;
        }
    }

    class Message
    {

        public string Content_;
        public string FullMessage_;
        public string Originator_;
        public string Target_;
        public string Fix_;
        public DateTime Time_;

        public Message(string content, string originator, string target = "all")
        {
            this.Content_ = content;
            this.Originator_ = originator;
            this.Target_ = target;
            this.Time_ = DateTime.Now;
        }

        public Message(string content, string originator, DateTime time, string target = "all")
        {
            this.Content_ = content;
            this.Originator_ = originator;
            this.Target_ = target;
            this.Time_ = time;
        }

        public void AddFix(string fix)
        {
            this.Fix_ = " " + fix;
            AssembleFullMessage();
        }

        public void AssembleFullMessage()
        {
            string time = Time_.ToString("yyyy-MM-dd HH:mm:ss");
            FullMessage_ = Content_.Insert(0, "[" + time + "] " + Originator_ + Fix_ + ": ");
        }
    }

    
    class Program
    {
        
        static Dictionary<string, ClientHandler> clients = new Dictionary<string, ClientHandler>();
        private static string SqlLoginString = "server=localhost; userid=root; password=Mbmbmb999999999; database=kurs";
        private static MySqlConnection SqlConnection;

        public static void UpdateClientDictionary(string oldID, string newName)
        {
            ClientHandler cli = clients[oldID];
            MyExtensions.RenameKey(clients, oldID, newName);

        }

        public static void RemoveThisClient(string name)
        {
            clients.Remove(name);
        }

        public static bool ValidNewName(string name)
        {
            bool valid = true;
            if (clients.ContainsKey(name) && !name.Contains(' '))
            {
                valid = false;
            }

            return valid;
        }

        public static bool NameExists(string name)
        {
            return !ValidNewName(name);
        }

        public static void Broadcast(Message message)
        {
            SqlInsertMessage(message);
            foreach (var cli in clients)
            {
                if (!cli.Value.SendMessage(message))
                {
                    Console.WriteLine("Could not send message to " + cli.Value.GetName());
                }
            }

        }
        
        public static bool Whisper(Message message)
        {
            if (clients.ContainsKey(message.Target_))
            {
                SqlInsertMessage(message);
                message.AddFix("whispers");

                if (clients[message.Target_].SendMessage(message))
                {
                    return true;
                }
            }

            return false;
        }

        public static void DisplayMessagesFromDate(string date = "today")
        {
            Console.WriteLine("Display messages from that date, here.");
        }

        static int GetMaxHosts()
        {
            string input;
            int hostsDefault = 10;
            int hosts = 10;
            Console.WriteLine("Enter max hosts (1 to 100): ");

            input = Console.ReadLine();


            if (!input.All(Char.IsDigit) || input.Length == 0)
            {
                Console.WriteLine("Invalid. Setting max hosts to default: " + hostsDefault);
            }
            else
            {
                Int32.TryParse(input, out hosts);
                if (hosts > 100 || hosts < 1)
                {
                    hosts = hostsDefault;
                    Console.WriteLine("Invalid. Setting max hosts to default: " + hostsDefault);
                }
            }

            return hosts;
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

        static void ExecuteServer()
        {

            int maxHosts = GetMaxHosts();
            int port = GetPort();

            IPHostEntry hostIPEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress localAddr = hostIPEntry.AddressList[0];
            localAddr = IPAddress.Parse("127.0.0.1");
            Console.WriteLine(localAddr.ToString());
            TcpListener serverSocket = new TcpListener(localAddr, 1234);
            TcpClient clientSocket = default(TcpClient);

            serverSocket.Start();
            
            ClientHandler client;

            bool going = true;
            int id = 0;
            while (going)
            {
                id++;
                clientSocket = serverSocket.AcceptTcpClient();
                client = new ClientHandler(clientSocket, id.ToString());
                client.StartChat();
                clients[id.ToString()] = client;
                Console.WriteLine("New client");
            }
        }

        static void SqlInsertMessage(Message message)
        {
            string sqlQuery = "INSERT INTO message(Sender, Content, Target, Time) VALUES("
                            + "@s, "
                            + "@c, "
                            + "@ta, "
                            + "@t"
                            + ")";

            try
            {
                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@s", message.Originator_);
                command.Parameters.AddWithValue("@c", message.Content_);
                command.Parameters.AddWithValue("@ta", message.Target_);
                command.Parameters.AddWithValue("@t", message.Time_);
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        static void GetHistory(string clientName)
        {

        }

        static void InitiateSQL()
        {
            SqlConnection = new MySqlConnection(SqlLoginString);
            SqlConnection.Open();
        }

        static void Main(string[] args)
        {
            InitiateSQL();
            ExecuteServer();
        }

    }

    class ClientHandler
    {
        TcpClient ClientSocket_;
        string Id_;
        string Name_;

        private byte[] recvBuffer_ = new byte[1024];
        private string dataFromClient_;
        
        public string GetName()
        {
            return Name_;
        }

        public ClientHandler(TcpClient clientSocket, string id)
        {
            this.ClientSocket_ = clientSocket;
            this.Id_ = id;
        }

        public void StartChat()
        {
            Thread chatThread = new Thread(Chat);
            chatThread.Start();
        }

        private string GetMessage()
        {

            string data;

            try
            {
                Array.Clear(recvBuffer_, 0, recvBuffer_.Length);
                NetworkStream stream = ClientSocket_.GetStream();
                stream.Read(recvBuffer_, 0, 1024);
                data = Encoding.UTF8.GetString(recvBuffer_);
                data = data.Substring(0, data.IndexOf("\0"));
                stream.Flush();

                return data;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public bool SendMessage(Message message)
        {

            byte[] sendBuffer = new byte[1024];

            try
            {
                message.AssembleFullMessage();
                sendBuffer = Encoding.UTF8.GetBytes(message.FullMessage_);
                NetworkStream stream = ClientSocket_.GetStream();
                stream.Write(sendBuffer, 0, sendBuffer.Length);
                Console.WriteLine("Sent \"" + message.FullMessage_ + "\" to " + this.Name_);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            
        }

        public bool IsValidName(string nameCandidate)
        {
            return nameCandidate != Name_;
        }

        private void SetNewName(string newName)
        {
            this.Name_ = newName;
            Program.UpdateClientDictionary(this.Id_, newName);
        }

        private string AnalyzeMessage(string message)
        {

            string key;
            
            if (message.Contains(' '))
            {
                key = message.Split(' ')[0];

                if (key == "/w")
                {
                    return "whisper";
                }

                if (key == "/d")
                {
                    return "date";
                }
            }
            else
            {
                key = message;
            }

            return "broadcast";
        }

        static private string GetWhisperTarget (string message)
        {
            if (message.Contains(' '))
            {

                if (Program.NameExists(message.Split(' ')[1]))
                {
                    return message.Split(' ')[1];
                }
            }

            return null;
        }
        
        private void Chat()
        {

            bool askingForName = true;
            bool chatting = true;

            SendMessage(new Message("Enter name: ", "announcer"));
            while (askingForName)
            {
                dataFromClient_ = GetMessage();
                if (dataFromClient_ != null)
                {
                    if (Program.ValidNewName(dataFromClient_)){
                        Console.WriteLine("Name registered: " + this.Name_);
                        SetNewName(dataFromClient_);
                        Program.Broadcast(new Message("[" + this.Name_ + "] has entered the chat.", "announcer"));
                        askingForName = false;
                    }
                    else
                    {
                        SendMessage(new Message("Name already exists. Enter name: ", "announcer"));
                    }
                }
            }

            string messageMode = null;
            string whisperTarget = null;
            string whisperContent = null;
            string[] split;
            int secondSpaceIndex = -1;
            
            while (chatting)
            {
                dataFromClient_ = GetMessage();
                if (dataFromClient_ != null)
                {
                    messageMode = AnalyzeMessage(dataFromClient_);
                    if (messageMode == "broadcast")
                    {
                        Program.Broadcast(new Message(dataFromClient_, this.Name_));
                    }
                    else if (messageMode == "whisper")
                    {
                        split = dataFromClient_.Split(' ');
                        whisperTarget = split[1];
                        if (Program.NameExists(whisperTarget) && split.Length > 2)
                        {
                            secondSpaceIndex = dataFromClient_.IndexOf(' ', dataFromClient_.IndexOf(' ') + 1 );
                            whisperContent = dataFromClient_.Remove(0, secondSpaceIndex + 1);
                            Program.Whisper(new Message(whisperContent, this.Name_, whisperTarget));
                        }
                    } // end iffing messageMode
                } // end if dataFromClient_ != null
                else
                {
                    chatting = false;
                    
                    Program.RemoveThisClient(this.Name_);
                    this.ClientSocket_.Close();
                    Program.Broadcast(new Message("[" + this.Name_ + "] has left the chat.", "announcer"));
                }
            } // end while chatting
        } // end Chat
    } // end ClientHandler
}
