using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text;
using System.IO;
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
        private static string SqlLoginString = "server=localhost; userid=root; password=WhatsupSlappers; database=kurs";
        private static MySqlConnection SqlConnection;

        static void Main(string[] args)
        {
            InitiateSQL();
            ExecuteServer();
        }

        static void InitiateSQL()
        {
            SqlConnection = new MySqlConnection(SqlLoginString);
            SqlConnection.Open();
        }

        public static void UpdateClientDictionary(string oldID, string newName)
        {
            ClientHandler cli = clients[oldID];
            MyExtensions.RenameKey(clients, oldID, newName);

        }

        public static void RemoveThisClient(string name)
        {
            clients.Remove(name);
            Console.WriteLine("Client " + name + " disconnect. Removing from list.");
            Program.Broadcast(new Message("[" + name + "] has left the chat.", "announcer"));
        }

        public static bool NameIsInUserDatabase(string name)
        {
            string sqlQuery = "SELECT COUNT(*) AS Count FROM user WHERE Name=@name";
            bool isInDatabase = false;
            try
            {
                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", name);
                MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {

                    Console.Write("_______READER.READ_______: ");
                    Console.WriteLine(reader["Count"].ToString());
                    if (reader["Count"].ToString() != "0")
                    {

                        Console.WriteLine("_______IS IN DATABASE_______");
                        isInDatabase = true;
                    }
                }
                reader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return isInDatabase;
        }

        public static bool IsValidPassword(string pw)
        {
            return true;
        }

        public static bool UserOnline(string name)
        {
            bool online = false;
            if (clients.ContainsKey(name))
            {
                online = true;
            }

            return online;
        }

        public static bool ValidNameSyntax(string name)
        {
            bool validSyntax = true;

            if (name.Length < 4 || name.Length > 45)
            {
                validSyntax = false;
            }

            string namePattern = "[^A-Za-z0-9]+";
            Match match = Regex.Match(name, namePattern);

            if (match.Success)
            {
                validSyntax = false;
            }

            return validSyntax;
        }

        public static bool ValidNewNameByExistingNames(string name)
        {
            bool valid = true;
            if (clients.ContainsKey(name))
            {
                valid = false;
            }
            else if (NameIsInUserDatabase(name))
            {
                valid = false;
            }

            return valid;
        }

        public static bool NameExists(string name)
        {
            return !ValidNewNameByExistingNames(name);
        }

        public static void Broadcast(Message message)
        {
            SqlInsertMessage(message);
            foreach (var cli in clients)
            {
                if (cli.Value.IsAuthenticated())
                {
                    if (!cli.Value.SendMessage(message))
                    {
                        Console.WriteLine("Could not send message to " + cli.Value.GetName());
                    }
                }
            }

        }
        
        public static bool Whisper(Message message)
        {
            // If user exists in database.
            if (NameIsInUserDatabase(message.Target_))
            {
                SqlInsertMessage(message);
            }

            // If user is online, basically.
            if (clients.ContainsKey(message.Target_))
            {
                if (clients[message.Target_].IsAuthenticated())
                {
                    message.AddFix("whispers");

                    if (clients[message.Target_].SendMessage(message))
                    {
                        return true;
                    }
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

        static string GetWebData(string urlAddress)
        {
            string webData = null;

            //try the URI, fail out if not successful 

            HttpWebRequest request;
            HttpWebResponse response;


            try
            {
                request = (HttpWebRequest)WebRequest.Create(urlAddress);
                //request.Headers["Accept"] = "application/json";
                response = (HttpWebResponse)request.GetResponse();
            }

            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //this could be modified for specific responses if needed
                return null;
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine("_____HTTP OK");
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;
                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                }

                webData = readStream.ReadToEnd();

                response.Close();
                readStream.Close();
            }
            return webData;
        }

        static void ChatBot()
        {

            DateTime nowTime = DateTime.Now;
            string description = "";
            string main = "";
            float temperature;
            string wholeWeather = "";
            
            while (true)
            {
                nowTime = DateTime.Now;
                if (nowTime.Minute % 5 == 0) {
                    Console.WriteLine("_____ATTEMPTING TO GET WEATHER_____");
                    string data = GetWebData("http://api.openweathermap.org/data/2.5/weather?q=Stockholm&appid=c5ee71f83ab0dcefd4908cad281a0597&units=metric");
                    if (data != null)
                    {
                        Console.WriteLine("_____DATA != NULL");
                        JObject o = JObject.Parse(data);
                        description = o["weather"][0]["description"].Value<String>();
                        main = o["weather"][0]["main"].Value<String>();
                        temperature = o["main"]["temp"].Value<float>();
                        wholeWeather = "Stockholm suffers from "
                                       + description
                                       + ", at a temperature of "
                                       + temperature.ToString()
                                       + "C.";
                        Console.WriteLine(wholeWeather);
                        Broadcast(new Message(wholeWeather, "WeatherBot"));
                        Thread.Sleep(300000 - 2000);
                    }
                    else
                    {
                        Console.WriteLine("_____DATA == NULL");
                    }
                    
                }
                else
                {
                    Thread.Sleep(1000);
                }

            }
        }

        static void ExecuteServer()
        {

            int maxHosts = GetMaxHosts();
            int port = GetPort();

            IPHostEntry hostIPEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress localAddr = hostIPEntry.AddressList[1];

            foreach (var addr in hostIPEntry.AddressList)
            {
                Console.WriteLine(addr.ToString());
            }
            
            //localAddr = IPAddress.Parse("127.0.0.1");
            Console.WriteLine(localAddr.ToString());
            TcpListener serverSocket = new TcpListener(localAddr, port);

            TcpClient clientSocket = default(TcpClient);

            serverSocket.Start();
            Thread chatBotThread = new Thread(ChatBot);
            chatBotThread.Start();
            
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

        public static bool CreateUser(string name, string password)
        {
            string sqlQuery = "INSERT INTO user(Name, Password) VALUES("
                            + "@name, "
                            + "@password"
                            + ")";

            try
            {
                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@password", password);
                command.ExecuteNonQuery();
                Console.WriteLine("______CREATE USER_______");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return false;
        }

        public static MySqlDataReader GetTodayHistory(string clientName)
        {
            if (clients.ContainsKey(clientName)) {
                string sqlQuery = "SELECT * FROM message WHERE (Target=@clientName OR Target='all') AND DATE(Time)=DATE(NOW()) ORDER BY Time ASC";
                
                try
                {
                    MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                    command.Parameters.AddWithValue("@clientName", clientName);
                    MySqlDataReader reader = command.ExecuteReader();
                    return reader;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                                 
            }

            return null;
        }
        
        public static bool LoginVerification(string username, string password)
        {

            bool verified = false;

            try
            {
                string sqlQuery = "SELECT * FROM user WHERE (Name=@name AND Password=@pass)";

                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", username);
                command.Parameters.AddWithValue("@pass", password);
                MySqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    Console.WriteLine("_______LOGIN VERIFIED_______");
                    verified = true;
                }
                reader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return verified;
        }

    }

    class ClientHandler
    {
        TcpClient ClientSocket_;
        string Id_;
        string Name_;

        private bool Authenticated_ = false;
        private byte[] recvBuffer_ = new byte[1024];
        private string dataFromClient_;
        private bool chatting = false;

        public ClientHandler(TcpClient clientSocket, string id)
        {
            this.ClientSocket_ = clientSocket;
            this.Id_ = id;
        }

        public ClientHandler() { }

        public void StartChat()
        {
            Thread chatThread = new Thread(Chat);
            chatThread.Start();
        }

        public bool IsAuthenticated()
        {
            return Authenticated_;
        }

        public string GetName()
        {
            return Name_;
        }

        public void StopSocket()
        {
            this.ClientSocket_.Close();
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
            catch
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
                Thread.Sleep(1); // So that the clients don't receive multiple messages.
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("_____" + message.FullMessage_ + "_____");
                Console.WriteLine("Disconnecting client: " + this.Name_);
                Quit();
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        private void SetNewName(string newName)
        {
            this.Name_ = newName;
            Program.UpdateClientDictionary(this.Id_, newName);
        }

        private void Quit()
        {
            chatting = false;
            Program.RemoveThisClient(this.Name_);
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
                
            }
            else
            {
                key = message;
            }

            if (key == "/d")
            {
                return "date";
            }

            if (key == "/quit" || key == "/q" || key == "/disconnect")
            {
                return "quit";
            }

            return "broadcast";
        }

        static private string GetWhisperTarget(string message)
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

        private void ReadHistory()
        {
            MySqlDataReader reader = Program.GetTodayHistory(this.Name_);
            if (reader != null)
            {
                Message msg;
                DateTime timeTemp;
                while (reader.Read())
                {
                    timeTemp = (DateTime)reader["Time"];
                    //Console

                    if (reader["Target"].ToString() == this.Name_)
                    {
                        msg = new Message(reader["Content"].ToString(), "(from) " + reader["Sender"].ToString(), (DateTime)reader["Time"]);
                        msg.AssembleFullMessage();
                        SendMessage(msg);
                    }
                    else if (reader["Target"].ToString() != "all")
                    {
                        msg = new Message(reader["Content"].ToString(), "(to) " + reader["Target"].ToString(), (DateTime)reader["Time"]);
                        msg.AssembleFullMessage();
                        SendMessage(msg);
                    }
                    else if (reader["Target"].ToString() == "all")
                    {
                        msg = new Message(reader["Content"].ToString(), reader["Sender"].ToString(), (DateTime)reader["Time"]);
                        msg.AssembleFullMessage();
                        SendMessage(msg);
                    }
                }
                reader.Close();
            }
        }

        private bool GetLoginOption()
        {

            SendMessage(new Message("You need to log in or create an account.", "LoginBot"));
            Thread.Sleep(4);
            SendMessage(new Message("1: Login", "LoginBot"));
            Thread.Sleep(4);
            SendMessage(new Message("2: Create Account", "LoginBot"));

            int input;
            bool isNumeric = false;
            while (true)
            {
                dataFromClient_ = GetMessage();
                isNumeric = int.TryParse(dataFromClient_, out input);
                if (isNumeric)
                {
                    if (input == 1)
                    {
                        return false;
                    }
                    else if (input == 2)
                    {
                        return true;
                    }
                }
            }
        }

        private string GetCreatePassword()
        {
            SendMessage(new Message("Create password:", "CreatorBot"));

            do
            {
                dataFromClient_ = GetMessage();

            } while (!Program.IsValidPassword(dataFromClient_));

            return dataFromClient_;
        }

        private bool InputCreateUser(ref string tempName, ref string tempPassword)
        {
            SendMessage(new Message("Create username: ", "CreatorBot"));
            tempName = GetMessage();

            bool success = false;

            if (Program.ValidNameSyntax(tempName))
            {
                if (Program.ValidNewNameByExistingNames(tempName))
                {
                    string password = GetCreatePassword();
                    Program.CreateUser(tempName, password);
                    success = true;
                }
                else
                {
                    SendMessage(new Message("Name already exists. Enter name: ", "CreatorBot"));
                }
            }
            else
            {
                SendMessage(new Message("Invalid syntax. Only letters and numbers!", "Annoyed CreatorBot"));
            }

            return success;
        }

        private bool LoggingIn () {
            bool creatingAccount = false;
            string tempName = null;
            string tempPassword = null;


            while (!this.Authenticated_)
            {
                creatingAccount = GetLoginOption();
                if (creatingAccount)
                {
                    this.Authenticated_ = InputCreateUser(ref tempName, ref tempPassword);
                }
                
                if (!this.Authenticated_ && !creatingAccount)
                {
                    SendMessage(new Message("Enter username: ", "LoginBot"));
                    tempName = GetMessage();
                    if (dataFromClient_ != null)
                    {
                        if (Program.NameIsInUserDatabase(tempName))
                        {
                            SendMessage(new Message("Enter password: ", "LoginBot"));
                            tempPassword = GetMessage();

                            if (Program.LoginVerification(tempName, tempPassword))
                            {
                                this.Authenticated_ = true;
                            }
                        }
                        else
                        {
                            SendMessage(new Message("Invalid username.", "LoginBot"));
                        }
                    }
                }
            }

            if (this.Authenticated_)
            {
                SetNewName(tempName);
                return true;
            }

            return false;
        }
        
        private void Chat()
        {

            if (LoggingIn())
            {
                ReadHistory();
                Program.Broadcast(new Message("[" + this.Name_ + "] has entered the chat.", "announcer"));
                chatting = true;
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
                            secondSpaceIndex = dataFromClient_.IndexOf(' ', dataFromClient_.IndexOf(' ') + 1);
                            whisperContent = dataFromClient_.Remove(0, secondSpaceIndex + 1);
                            Program.Whisper(new Message(whisperContent, this.Name_, whisperTarget));
                            
                            Message toSender = new Message(whisperContent, "To " + whisperTarget);
                            if (!Program.UserOnline(whisperTarget))
                            {
                                Console.WriteLine("USER NOT ONLINE");
                                toSender.AddFix("(offline)");
                            }

                            SendMessage(toSender);
                        }
                    }
                    else if (messageMode == "quit")
                    {
                        Quit();
                    }// end iffing messageMode
                } // end if dataFromClient_ != null
                else
                {
                    Quit();
                }
            } // end while chatting
        } // end Chat
    } // end ClientHandler
}
