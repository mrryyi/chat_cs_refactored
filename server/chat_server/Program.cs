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

        // Constructs message with time to default to server time.
        public Message(string content, string originator, string target = "all")
        {
            this.Content_ = content;
            this.Originator_ = originator;
            this.Target_ = target;
            this.Time_ = DateTime.Now;
        }

        // Constructs message with time set to a known DateTime.
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

        // Assembles the message, combining dateTime, sender and an optional fix (like whispers)
        public void AssembleFullMessage()
        {
            string time = Time_.ToString("yyyy-MM-dd HH:mm:ss");
            FullMessage_ = Content_.Insert(0, "[" + time + "] " + Originator_ + Fix_ + ": ");
        }
    }

    
    class Program
    {
        // The dictionary that contains connected clients.
        static Dictionary<string, ClientHandler> clients = new Dictionary<string, ClientHandler>();

        // The one SQL login string.
        private static string SqlLoginString = "server=localhost; userid=root; password=WhatsupSlappers; database=kurs";

        // The only used MySqlConnection variable. Bad implementation when there needs to be several requests.
        // Could make a new connection per request, in future versions.
        private static MySqlConnection SqlConnection;

        static void Main(string[] args)
        {
            InitiateSQL();
            CreateTablesIfNotExists();
            ExecuteServer();
        }

        // Opens the connection to the server.
        static void InitiateSQL()
        {
            SqlConnection = new MySqlConnection(SqlLoginString);
            SqlConnection.Open();
        }

        // Creates databases if they do not exist.
        static void CreateTablesIfNotExists()
        {
            MySqlCommand createTable;
            
            // Could remove "IF NOT EXISTS" and just handle the exception.
            try
            {
                createTable = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `messages` ("
                +"`Id` int(10) unsigned NOT NULL AUTO_INCREMENT,"
                +"`Sender` varchar(45) NOT NULL,"
                +"`Content` varchar(1024) NOT NULL,"
                +"`Target` varchar(45) NOT NULL,"
                +"`Time` datetime NOT NULL,"
                +"PRIMARY KEY (`Id`)"
                +") ENGINE=InnoDB AUTO_INCREMENT=357 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci", SqlConnection);

                createTable.ExecuteNonQuery();
            }
            catch{}

            try
            {

                createTable = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `users` ("
                  + "`Id` int(10) unsigned NOT NULL AUTO_INCREMENT,"
                  + "`Name` varchar(45) NOT NULL,"
                  + "`Password` varchar(45) NOT NULL,"
                  + "PRIMARY KEY (`Id`),"
                  + "UNIQUE KEY `Name_UNIQUE` (`Name`)"
                  + ") ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci", SqlConnection);

                createTable.ExecuteNonQuery();
            }
            catch{}
        }

        // The main function that serves to connect clients.
        static void ExecuteServer()
        {

            int maxHosts = InputMaxHosts();
            int port = InputPort();

            IPHostEntry hostIPEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress localAddr = hostIPEntry.AddressList[1];
            
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
                if (clients.Count < maxHosts)
                {
                    clientSocket = serverSocket.AcceptTcpClient();
                    client = new ClientHandler(clientSocket, id.ToString());
                    client.StartChat();
                    // Temporary key for the client, such that we can refer to them
                    // in the code before they give a name. Clients online will be found using
                    // the name of the user like so: clients[name].
                    // At this point, the client has no name, just a key in this dictionary.
                    clients[id.ToString()] = client;
                }
            }
        }


        // Basically changes the name from a temporary ID to their name.
        // Makes a copy, calls RenameKey which deletes the old one and 
        // inserts the copy with the new name.
        public static void UpdateClientDictionary(string oldID, string newName)
        {
            ClientHandler cli = clients[oldID];
            MyExtensions.RenameKey(clients, oldID, newName);

        }

        // Removes client from the list of connected clients.
        // Does not need to be an authenticated client.
        public static void RemoveThisClient(string name)
        {
            if (ClientConnected(name))
            {
                clients.Remove(name);
                Console.WriteLine("Client " + name + " disconnect. Removing from list.");
                Program.Broadcast(new Message("(" + name + ") has left the chat.", "Announcer"));
            }
        }

        // Gets the count from users table for every Name==(parameter name)
        // Returns true if count != 0.
        public static bool NameIsInUserDatabase(string name)
        {
            string sqlQuery = "SELECT COUNT(*) AS Count FROM users WHERE Name=@name";
            bool isInDatabase = false;

            try
            {
                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", name);
                MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Console.WriteLine(reader["Count"].ToString());
                    // Count, therefore the name exists in the user database.
                    if (reader["Count"].ToString() != "0")
                    {
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

        // Returns true if client 
        public static bool ClientConnected(string name)
        {
            bool connected = false;

            //Online if in clients dictionary.
            if (clients.ContainsKey(name))
            {
                connected = true;
            }

            return connected;
        }

        // Returns true if password parameter is correct to the allowed syntax.
        public static bool ValidPasswordSyntax(string pw)
        {
            bool validSyntax = true;

            string passwordPattern = "^(?=.*\\d).{4,8}$";

            Match match = Regex.Match(pw, passwordPattern);
            if (!match.Success)
            {
                validSyntax = false;
            }

            return validSyntax;
        }

        // Returns true if name parameter is correct to the allowed syntax.
        public static bool ValidNameSyntax(string name)
        {
            bool validSyntax = true;

            if (name.Length < 4 || name.Length > 45)
            {
                validSyntax = false;
            }
            else {
                string namePattern = "[^A-Za-z0-9]+";
                Match match = Regex.Match(name, namePattern);

                if (match.Success)
                {
                    validSyntax = false;
                }
            }

            return validSyntax;
        }

        // Returns true if there is a connected client or
        // registered user by the name of the parameter name.
        public static bool ValidNewNameByExistingNames(string name)
        {
            bool valid = true;
            
            if (ClientConnected(name))
            {
                valid = false;
            }
            else if (NameIsInUserDatabase(name))
            {
                valid = false;
            }

            return valid;
        }

        // Recycles valid new name function such that if the parameter name
        // is not valid for a new user, then it exists in the database.
        // Returns true if user exists in database.
        public static bool NameExists(string name)
        {
            return !ValidNewNameByExistingNames(name);
        }

        // Sends a message to every client that is authenticated (logged in).
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
        
        // Sends a private message to a target if they exist.
        // Returns true if online and they got the message.
        // If returns false, it means that the target client
        // did not get it.
        public static bool Whisper(Message message)
        {
            // If user exists in database.
            if (NameIsInUserDatabase(message.Target_))
            {
                SqlInsertMessage(message);
            }

            // If user is online, basically.
            if (ClientConnected(message.Target_))
            {
                if (clients[message.Target_].IsAuthenticated())
                {
                    message.AddFix("whispers");

                    // True if no exception and the message got sent.
                    if (clients[message.Target_].SendMessage(message))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Unimplemented.
        public static void DisplayMessagesFromDate(string date = "today")
        {
            Console.WriteLine("Display messages from that date, here.");
        }

        // Get max hosts from input.
        static int InputMaxHosts()
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

        // Get port from input.
        static int InputPort()
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

        // Gets web data by HTTP protocol using parameter string URL.
        // Returns null if unsuccessful.
        static string GetWebData(string urlAddress)
        {
            string webData = null;

            HttpWebRequest request;
            HttpWebResponse response;

            try
            {
                request = (HttpWebRequest)WebRequest.Create(urlAddress);
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
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

        // Just a chat bot that gets weaher every 5 minutes as a json file and broadcasts a summary to all clients.
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
                if (nowTime.Minute % 5 == 0)
                {
                    string data = GetWebData("http://api.openweathermap.org/data/2.5/weather?q=Stockholm&appid=c5ee71f83ab0dcefd4908cad281a0597&units=metric");
                    if (data != null)
                    {
                        try
                        {
                            JObject o = JObject.Parse(data);
                            description  = o["weather"][0]["description"].Value<String>();
                            main         = o["weather"][0]["main"].Value<String>();
                            temperature  = o["main"]["temp"].Value<float>();
                            wholeWeather = "Stockholm suffers from "
                                           + description
                                           + ", at a temperature of "
                                           + temperature.ToString()
                                           + "C.";

                            Broadcast(new Message(wholeWeather, "Weather-announcer"));
                            // Save resources for 4 minutes and 55 seconds.
                            Thread.Sleep(300000 - 5000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }

                }
                else
                {
                    // Try every second when checking.
                    Thread.Sleep(1000);
                }

            }
        }

        // Creates and inserts a message into the messages table in the connected database.
        static void SqlInsertMessage(Message message)
        {
            string sqlQuery = "INSERT INTO messages(Sender, Content, Target, Time) VALUES("
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

        // Creates and inserts a user with name and passowrd into the users table.
        public static bool CreateUser(string name, string password)
        {
            string sqlQuery = "INSERT INTO users(Name, Password) VALUES("
                            + "@name, "
                            + "@password"
                            + ")";

            try
            {
                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@password", password);
                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return false;
        }

        // Selects messages from the messages table by matching Target to parameter clientName
        // or matching Target to "'all'" such that the client only gets messages intended for that client.
        // Will get targeted messages and broadcasted messages.
        // Returns reader if true, which could REALLY screw things up if the callers does NOT
        // close the reader.
        public static MySqlDataReader GetTodayHistory(string clientName)
        {
            if (clients.ContainsKey(clientName)) {
                string sqlQuery = "SELECT * FROM messages WHERE (Target=@clientName OR Target='all') AND DATE(Time)=DATE(NOW()) ORDER BY Time ASC";
                
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
        
        // Checks if parameter name and password exists in database on the same row.
        // Returns true if that is true.
        public static bool LoginVerification(string username, string password)
        {

            bool verified = false;

            try
            {
                string sqlQuery = "SELECT * FROM users WHERE (Name=@name AND Password=@pass)";

                MySqlCommand command = new MySqlCommand(sqlQuery, SqlConnection);
                command.Parameters.AddWithValue("@name", username);
                command.Parameters.AddWithValue("@pass", password);
                MySqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
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
        
        // Constructor that embeds a TcpClient.
        public ClientHandler(TcpClient clientSocket, string id)
        {
            this.ClientSocket_ = clientSocket;
            this.Id_ = id;
        }

        // Default constructor.
        public ClientHandler() { }

        public void StartChat()
        {
            Thread chatThread = new Thread(Chat);
            chatThread.Start();
        }

        // Basically "is logged in".
        // Can exist in Program.clients dictionary, but
        // would not be authenticated until logged in.
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

        // GetMessage which lstens to the client connection and returns string data unless exception.
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

        // Send message to the client that is connected to the socket instance in this ClientHandler.
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
                Thread.Sleep(4); // So that the clients don't receive multiple messages.
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Disconnecting client: " + this.Name_);
                Quit();
                return false;
            }
            catch
            {
                return false;
            }

        }

        // Sets a name for a client.
        // Used only once to set the name to the user login name.
        private void SetNewName(string newName)
        {
            this.Name_ = newName;
            Program.UpdateClientDictionary(this.Id_, newName);
        }

        // Removes client from online clients in Program.
        // Sets chatting to false to quit the main loop.
        // Sends a final message.
        // Stops socket.
        private void Quit()
        {
            chatting = false;
            Program.RemoveThisClient(this.Name_);
            StopSocket();
        }

        // Finds commands from a user.
        // If found command, then return a string with the speciality
        // of the message the user sent.
        // Default is broadcast.
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

        // Just splits a "/w target..." string such that we return target.
        // Doesn't mind if there is no message, we just get the target here.
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

        // Gets the history for this day.
        // Could be implemented to have parameter date and create another GetTodayHistory
        // such that we can find any date.
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

                    // We only write history messages that are intended to the client. Whispers, or broadcasted (Target="all").
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

        // Prompts the client to login or create account.
        private bool GetLoginOption()
        {

            SendMessage(new Message("You need to log in or create an account.", "LoginBot"));
            SendMessage(new Message("1: Login", "LoginBot"));
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

        // Prompts the client to create a password for a user.
        private string GetCreatePassword()
        {
            SendMessage(new Message("Create password:", "CreatorBot"));
            
            dataFromClient_ = GetMessage();

            if (Program.ValidPasswordSyntax(dataFromClient_))
            {
                return dataFromClient_;
            }

            return null;
        }

        // Get client input to create a user.
        // Returns true if valid username (not used) and password has valid syntax.
        private bool InputCreateUser(ref string tempName, ref string tempPassword)
        {
            bool success = false;

            SendMessage(new Message("Create username: ", "CreatorBot"));
            tempName = GetMessage();

            if (Program.ValidNameSyntax(tempName))
            {
                if (Program.ValidNewNameByExistingNames(tempName))
                {
                    string password = GetCreatePassword();

                    if (password != null)
                    {
                        Program.CreateUser(tempName, password);
                        success = true;
                    }
                    else
                    {
                        SendMessage(new Message("Password must be between 4 and 8 digits long and include at least one numeric digit.", "CreatorBot"));
                    }
                }
                else
                {
                    SendMessage(new Message("Name already exists.", "CreatorBot"));
                }
            }
            else
            {
                SendMessage(new Message("Invalid syntax. Only letters and numbers!", "CreatorBot"));
            }

            return success;
        }

        // Clients get stuck in this loop until they are authenticated, either by creating a user 
        // or by logging in to an existing one.
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

            // Redundant if statement, it is true at this point if it broke the while loop, but why not.
            if (this.Authenticated_)
            {
                // Sets the name for the client such that it can be referred to in 
                // operations to send to ONLINE users.
                SetNewName(tempName);
                return true;
            }

            return false;
        }
        
        private void Chat()
        {

            // Get the client logged in.
            if (LoggingIn())
            {
                // When logged in, we read the history and then broadcast that we are hereö
                ReadHistory();
                Program.Broadcast(new Message("[" + this.Name_ + "] has entered the chat.", "Announcer"));
                chatting = true;
            }

            string messageMode = null;
            string whisperTarget = null;
            string whisperContent = null;
            string[] split;
            int secondSpaceIndex = -1;
            
            while (chatting)
            {
                // Listens to client.
                dataFromClient_ = GetMessage();
                if (dataFromClient_ != null)
                {
                    // Analyses message.
                    messageMode = AnalyzeMessage(dataFromClient_);
                    // Broadcasts if mode is broadcast.
                    if (messageMode == "broadcast")
                    {
                        Program.Broadcast(new Message(dataFromClient_, this.Name_));
                    }
                    // Whispers if mode is whisper.
                    else if (messageMode == "whisper")
                    {
                        split = dataFromClient_.Split(' ');
                        // Who we whispers to. 
                        whisperTarget = split[1];

                        // If there is a target in the database, and there is a message after the name.
                        if (Program.NameExists(whisperTarget) && split.Length > 2)
                        {
                            secondSpaceIndex = dataFromClient_.IndexOf(' ', dataFromClient_.IndexOf(' ') + 1);

                            // Separates the content from the rest of the message.
                            whisperContent = dataFromClient_.Remove(0, secondSpaceIndex + 1);
                            bool successfulWhisper = Program.Whisper(new Message(whisperContent, this.Name_, whisperTarget));
                            
                            // If we successfully whisper
                            Message toSender = new Message(whisperContent, "To " + whisperTarget);
                            if (!Program.ClientConnected(whisperTarget) || !successfulWhisper)
                            {
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
