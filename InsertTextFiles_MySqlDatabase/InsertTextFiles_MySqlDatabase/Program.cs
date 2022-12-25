using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InsertTextFilesToSQL
{
    internal class Program
    {
        static void Main(string[] argc)
        {
            Dictionary<string, string> argd = getArgDictionary(argc);
            AddDefaultArguments(argd);
            OutputInitialMessage(argd);
            if (!AskToContinue()) return;
            //user should be given the option to set parameters here
            List<string> filePaths = getFilePaths(argd);

            MySqlConnection connection = null;
            try
            {
                string connectionString = getConnectionString(argd);
                connection = new MySqlConnection(connectionString);
                connection.Open();
                log("Connection to \"localhost\" opened!");

                foreach (string file in filePaths)
                {
                    string cur = file;
                    if (file.StartsWith(argd["dir"]))
                    {
                        try
                        {
                            cur = cur.Substring(argd["dir"].Length);
                            string text = File.ReadAllText(file);

                            cur = MySqlHelper.EscapeString(cur);
                            text = MySqlHelper.EscapeString(text);

                            MySqlCommand cmd = new MySqlCommand();
                            cmd.Connection = connection;
                            cmd.CommandText = "INSERT INTO mainspace(filename, content) VALUES('" + cur + "', '" + text + "')";
                            cmd.Prepare();

                            cmd.Parameters.AddWithValue("?filename", cur);
                            cmd.Parameters.AddWithValue("?content", text);
                            int res = cmd.ExecuteNonQuery();

                            string message;
                            if (res > 0)
                            {
                                message = "Successfully inserted file \"" + cur + "\" in database.";
                            }
                            else
                            {
                                message = "Failed to insert file \"" + cur + "\" in database.";
                            }
                            log(message);
                        }
                        catch (Exception ex)
                        {
                            string message = "Failed to insert file \"" + cur + "\" in database - " + ex.Message;
                            log(message);
                            continue;
                        }
                    }
                }
            }
            finally
            {
                if (connection != null) connection.Close();
            }

            log(Environment.NewLine + "Done! Press any key to exit!", true);
        }


        //program functionality
        private static void AddDefaultArguments(Dictionary<string, string> argd)
        {
            if (argd.ContainsKey("depth") == false)
            {
                argd.Add("depth", "0");
            }

            if (argd.ContainsKey("dir") == false)
            {
                string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\";
                argd.Add("dir", dir);
            }
        }
        private static void OutputInitialMessage(Dictionary<string, string> argd)
        {
            string message = "Going to start an operation that will take all the files in this directory: ";
            message += Environment.NewLine + "\"" + argd["dir"] + "\"" + Environment.NewLine;
            if (argd.ContainsKey("depth") && argd["depth"] == "1") message += "and all its subdirectories ";
            message += "and attempt to insert their contents in MySQL database:" + Environment.NewLine + Environment.NewLine;

            foreach (KeyValuePair<string, string> kvp in argd)
            {
                message += kvp.Key + " : \"" + kvp.Value + "\"" + Environment.NewLine;
            }

            message += Environment.NewLine + "Do you like to proceed(Y/N)?";
            Console.WriteLine(message);
        }
        private static bool AskToContinue()
        {
            string response = Console.ReadLine();
            if (response.ToLower() != "y")
            {
                Console.WriteLine("\"" + response + "\"" + " is not 'Y'." + Environment.NewLine + "Press any key to exit.");
                Console.ReadLine();
                return false;
            }
            return true;
        }
        private static List<string> getFilePaths(Dictionary<string, string> argd)
        {
            List<string> filePaths = new List<string>();
            if (argd["depth"] == "0")
            {
                filePaths = Directory.GetFiles(argd["dir"], "*", SearchOption.TopDirectoryOnly).ToList();
            }
            else
            {
                filePaths = Directory.GetFiles(argd["dir"], "*", SearchOption.AllDirectories).ToList();
            }

            // remove self path
            string thisexe = Path.GetFullPath(Assembly.GetEntryAssembly().Location);
            for(int i = 0; i < filePaths.Count; i++)
            {
                if(filePaths[i] == thisexe)
                {
                    filePaths.RemoveAt(i);
                    i--;
                }
                else if (filePaths[i].StartsWith("@"))
                {
                    filePaths.RemoveAt(i);
                    i--;
                }
            }

            log(Environment.NewLine + "Got " + filePaths.Count + " files:" + Environment.NewLine);
            foreach(string s in filePaths)
            {
                log(s);
            }

            Console.WriteLine();
            return filePaths;
        }
        static string getConnectionString(Dictionary<string, string> argd)
        {
            string connectionString = "";
            connectionString += "server=" + argd["server"];
            connectionString += ";userid=" + argd["user"];
            connectionString += ";password=" + argd["password"];
            connectionString += ";database=" + argd["database"];

            return connectionString;
        }
        static void log(string text, bool block = false)
        {
            Console.WriteLine(text);
            if(block) Console.ReadLine();
        }



        //getting args
        static Dictionary<string, string> getArgDictionary(string[] argc)
        {
            Dictionary<string, string> result = getFileBodyDictionary();

            Dictionary<string, string> fromName = getFileNameDictionary();
            foreach (KeyValuePair<string, string> kvp in fromName)
            {
                if (result.ContainsKey(kvp.Key)) result[kvp.Key] = kvp.Value;
                else result.Add(kvp.Key, kvp.Value);
            }

            Dictionary<string, string> fromCmd = getCmdLineDictionary(argc);
            foreach (KeyValuePair<string, string> kvp in fromCmd)
            {
                if (result.ContainsKey(kvp.Key)) result[kvp.Key] = kvp.Value;
                else result.Add(kvp.Key, kvp.Value);
            }

            return result;
        }
        static Dictionary<string, string> getCmdLineDictionary(string[] argc)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string arg in argc)
            {
                string[] separated = arg.Trim('-').Trim('/').Split('=');
                switch (separated[0])
                {
                    case "server":
                    case "s":
                        result.Add("server", separated[1]);
                        continue;
                    case "userid":
                    case "user":
                    case "uid":
                    case "u":
                        result.Add("user", separated[1]);
                        continue;
                    case "password":
                    case "pass":
                    case "pwd":
                    case "pw":
                    case "p":
                        result.Add("password", separated[1]);
                        continue;
                    case "database":
                    case "db":
                    case "d":
                        result.Add("database", separated[1]);
                        continue;
                    case "table":
                    case "tb":
                    case "t":
                        result.Add("table", separated[1]);
                        continue;
                    case "filename":
                    case "fname":
                    case "fn":
                    case "f":
                        result.Add("filename", separated[1]);
                        continue;
                    case "filecontent":
                    case "fcontent":
                    case "fc":
                    case "c":
                        result.Add("filecontent", separated[1]);
                        continue;
                    case "directory":
                    case "dir":
                    case "i":
                        result.Add("dir", separated[1]);
                        continue;
                    case "depth":
                    case "e":
                        result.Add("depth", separated[1]);
                        continue;
                }
            }
            return result;
        }
        static Dictionary<string, string> getFileNameDictionary()
        {
            string[] argn = getArgsFromFileName();
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (string arg in argn)
            {
                string[] separated = arg.Trim('-').Trim('/').Split('=');
                switch (separated[0])
                {
                    case "server":
                    case "s":
                        result.Add("server", separated[1]);
                        continue;
                    case "userid":
                    case "user":
                    case "uid":
                    case "u":
                        result.Add("user", separated[1]);
                        continue;
                    case "password":
                    case "pass":
                    case "pwd":
                    case "pw":
                    case "p":
                        result.Add("password", separated[1]);
                        continue;
                    case "database":
                    case "db":
                    case "d":
                        result.Add("database", separated[1]);
                        continue;
                    case "table":
                    case "tb":
                    case "t":
                        result.Add("table", separated[1]);
                        continue;
                    case "filename":
                    case "fname":
                    case "fn":
                    case "f":
                        result.Add("filename", separated[1]);
                        continue;
                    case "filecontent":
                    case "fcontent":
                    case "fc":
                    case "c":
                        result.Add("filecontent", separated[1]);
                        continue;
                    case "directory":
                    case "dir":
                    case "i":
                        result.Add("dir", separated[1]);
                        continue;
                    case "depth":
                    case "e":
                        result.Add("depth", separated[1]);
                        continue;
                }
            }
            return result;
        }
        static Dictionary<string, string> getFileBodyDictionary()
        {
            string[] argb = getArgsFromFileBody();
            Dictionary<string, string> result = new Dictionary<string, string>();

            //todo

            return result;
        }
        static string[] getArgsFromFileName()
        {
            string fname = Path.GetFileName(Assembly.GetEntryAssembly().Location);
            string[] args = fname.Split('(')[1].Split(')')[0].Split(',');
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = args[i].Trim();
            }

            return args;
        }
        static string[] getArgsFromFileBody()
        {
            string fname = Path.GetFileName(Assembly.GetEntryAssembly().Location);
            byte[] blob = File.ReadAllBytes(fname);

            //todo


            return new string[0];
        }
    }
}
//s=localhost, u=root, p=, d=worldlists, t=mainspace, f=filename, c=content
//InsertTextFiles_MySqlDatabase(s = localhost, u = root, p =, d = worldlists, t = mainspace, f = filename, c = content).exe
//Dictionary<string, string> arguments = new Dictionary<string, string>()
//{
//    { "server", "localhost" },
//    { "user", "root" },
//    { "pass", "" },

//    { "databaseName", "worldlists" },
//    { "tableName", "mainspace" },
//    { "fileNameField", "filename" },
//    { "fileContentField", "content" },

//    { "executeNow", "false" },
//    { "exitWhenDone", "false" },
//    { "outputToFile", "false" },

//    //multy-level deep
//    //filenames masks
//    //by date

//    //buffer size
//    //output file name "DEFAULT"
//};