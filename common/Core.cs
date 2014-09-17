using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atechnology.DBConnections2;
using common.Properties;

namespace common
{
    public class Core
    {
        static readonly String[] tableNames = { "modelscript", "docscript", "orderevent", "designerevent" };
        static readonly string   url = "hook";
        private const string IPC = "ipc://";

        public static void get(Cred cred, bool force = false)
        {
            // fetch scripts from db to local disc
            string connectionString = cred.connectionString();
            SqlConnection con = new SqlConnection(connectionString);

            // update hook = select * where (sql.file.sha1 != work_directory.file.sha1)
            SHA1 sha1 = SHA1.Create();

            //
            using (SqlCommand command = con.CreateCommand())
            {
                SqlDataAdapter adapter = new SqlDataAdapter(command);

                foreach (string tableName in tableNames)
                {
                    DataTable scriptTable = new DataTable(tableName);
                    //command.CommandText = "SELECT name, codescript, guid FROM " + tableName + " where deleted is null";
                    command.CommandText = "SELECT name, codescript FROM " + tableName + " where deleted is null";

                    adapter.Fill(scriptTable);
                    DataRow[] dataRows = scriptTable.Select();

                    // 
                    Dictionary<string,string> scripts = new Dictionary<string, string>();
                    foreach (DataRow row in dataRows)
                    {
                        string name = row["name"].ToString();      
                        string code = row["codescript"].ToString();
                        string valid = Script.ValidScriptName(name);
                        
                        if (scripts.ContainsKey(valid))
                            throw new DuplicateScriptNameException(tableName + "/" + name + " -> " + valid + "<-" + scripts[valid]);
                        
                        scripts.Add(valid, name);

                        Script script = new Script(code, tableName, name);

                        string fileName = cred.cwd + "/" + script.FileName;

                        // проверить sha
                        string code0 = "";
                        try
                        {
                            code0 = File.ReadAllText(fileName);
                        }
                        catch{}

                        byte[] hash1 = sha1.ComputeHash(Encoding.UTF8.GetBytes(script.VisualCode));
                        byte[] hash0 = sha1.ComputeHash(Encoding.UTF8.GetBytes(code0));

                        if (force || !bytesEqual(hash0, hash1))
                        {
                            Directory.CreateDirectory(cred.cwd + "/" + script.NameSpace);
                            File.WriteAllText(fileName, script.VisualCode); // 65001
                            Console.WriteLine(script.NameSpace+"."+script.ScriptName);  // non comm il faut
                        }
                    }
                }
            }



        }

        public static void compile(Cred cred)
        {
            // refresh AT assembly
            string name = cred.db;
            IpcClientChannel clientChannel = new IpcClientChannel();
            ChannelServices.RegisterChannel(clientChannel, false);
            RemotingConfiguration.RegisterWellKnownClientType(typeof (Pinger), IPC + name + "/" + url); // "ipc://remote/remote"
            Pinger pinger = new Pinger();
            pinger.serv(cred);
        }

        public static void commit(Cred cred)
        {
            // save scripts to db

            // IpcRefresher refresher = new IpcRefresher();

            // commit hook != compile;
            // нас запускают из корневой папки проекта

            // first get changed files 
            // hg log --debug -v -r tip
            string[] files = getHgLogFiles(cred);

            // next get file from repo ( not  working directory !)
            // hg cat --cwd d:\hg\new -o d:/hg/temp_file.cs -r tip 4.cs
            // hg cat -r tip $file 
            foreach (string file in files)
            {
                string code = getCode(cred, file);

                Script script = compile(cred, code);

                // send message to IPC Server // winDraw
                // refresher.refresh(script.ScriptType);
            }


        }

        public static void inject(Cred cred)
        {
            try
            {
                SqlConnection con = new SqlConnection(cred.connectionString());
                con.Open();
                using (SqlCommand command = con.CreateCommand())
                {
                    command.CommandText = @"select name from orderevent where name ='Системные события.Запуск'";
                    object have = command.ExecuteScalar();
                    if (have == null)
                    {
                        command.CommandText = @"select top 1 idordereventgroup from ordereventgroup where isactive = 1";
                        object o = command.ExecuteScalar();
                        int idgroup = (int) o;

                        int id = 0; /// todo xxx

                        byte[] bytes = Resources.temp;

                        command.CommandText = @"insert orderevent(idorderevent,name,dll,compiled,idordereventgroup) values(@idorderevent,@name,@dll,@compiled,@idordereventgroup)";
                        command.Parameters.Clear();
                        command.Parameters.Add("idorderevent", SqlDbType.Int).Value = id;
                        command.Parameters.Add("name", SqlDbType.VarChar, 128).Value = "Системные события.Запуск";
                        command.Parameters.Add("dll", SqlDbType.VarBinary, bytes.Length).Value = bytes;
                        command.Parameters.Add("compiled", SqlDbType.SmallInt).Value = 1;
                        command.Parameters.Add("idordereventgroup", SqlDbType.Int).Value = idgroup;

                        command.ExecuteNonQuery();
                    }
                    else
                    {
                        throw new InjectOcuped();
                    }
                }
                con.Close();
            }
            catch (Exception ex)
            {
                throw new InjectEx();
            }
            //throw new NotImplementedException();
        }

        public static void eject(Cred cred)
        {
            throw new NotImplementedException();
        }

        public static void startServer() // ? имя канала (url) ? папка для поиска .dll
        {
            string name =  dbinit.db.Database;
            try
            {
                // Create and register an IPC channel
                IpcServerChannel serverChannel = new IpcServerChannel(name); // ? имя => ipc://{name}
                // ReSharper disable once CSharpWarnings::CS0618                // obsolette
                ChannelServices.RegisterChannel(serverChannel);
                // Expose an object
                RemotingConfiguration.RegisterWellKnownServiceType(typeof (Pinger), url, WellKnownObjectMode.Singleton); // ? ipc://name/{url}
                // MessageBox.Show("Listening on " + serverChannel.GetChannelUri());
            }
            catch (Exception)
            {
                throw new IpcException(IPC + name + "/" + url);
            }
        }

        public static void stopServer()
        {
            throw new NotImplementedException();
        }

        private static bool bytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        private static string[] getHgLogFiles(Cred cred)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("hg");
            startInfo.WorkingDirectory = cred.cwd;
            startInfo.Arguments = "log --debug -v -r tip";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.GetEncoding(1251); /// Mercurial все вывыливает в кодировке системы тут : win1251, в Windows 7 может быть иначе
            Process process = Process.Start(startInfo);

            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                // parse
                Match match = Regex.Match(output, @"(?<=files:)(.*)");
                if (match.Success)
                {
                    string[] split = Regex.Split(match.Value.Trim(), @"\s+");
                    if (split.Length > 0)
                    {
                        return split;       // {1.c 2.c ...}
                    }
                }
            }
            throw new NothingToDoEx();
        }

        private static Script compile(Cred cred, string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            // populate list of external refs = dependancess
            List<string> refList = new List<string>();
            TextReader refReader = new StreamReader(Path.Combine(cred.windraw, @"scripts\ref"));

            string refName;
            while ((refName = refReader.ReadLine()) != null)
                if (refName.StartsWith("System"))
                    refList.Add(refName); // системные идут AS IS
                else
                    refList.Add(Path.Combine(cred.windraw, refName)); // добавить путь к своим

            // добавить пользовательскую библиотеку, а системный скрипт ????
            string systemUserLibrary = Path.Combine(cred.windraw, "Atechnology.ecad.Calc.SystemUserLibrary.dll");
            if (!refList.Contains(systemUserLibrary) && File.Exists(systemUserLibrary))
                refList.Add(systemUserLibrary);

            // поднять объекты
            Script script = new Script(code);

            //CompilerResults compilerResults = myCompiler.Compile(script.MirrowSource, script.MirrowDLL, refList.ToArray()); ///***
            CompilerResults compilerResults = script.Compile(cred.cwd, refList.ToArray());

            if (compilerResults.Errors.Count > 0)
                throw new CompileExcexption(compilerResults);

            // читаем dll
            string dllFileName = compilerResults.PathToAssembly;

            byte[] dll;
            using (FileStream fs = new FileStream(dllFileName, FileMode.Open))
            {
                dll = new byte[fs.Length];
                fs.Read(dll, 0, (int) fs.Length);
            }

            SqlConnection con = new SqlConnection(cred.connectionString());
            con.Open();

            string name = null;

            // поднять исходное имя скрипта
            using (SqlCommand cmd = con.CreateCommand())
            {
                cmd.CommandText = "select name from " + script.TableName;
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    name = reader[0].ToString();
                    if(Script.ValidScriptName(name).Equals(script.ScriptName))
                        break;
                }
                reader.Close();
                reader.Dispose();
            }

            // прорихтовать базу если нашли
            if(name != null)
                using (SqlCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"update " + script.TableName + " set codescript = @pureCode, dll = @dll, dtcompile = @dtcompile where name = @name";
                    cmd.Parameters.AddWithValue("@pureCode", script.PureCode);
                    byte[] zip = Atechnology.Components.ZipArchiver.Zip(dll);
                    cmd.Parameters.AddWithValue("@dll", zip);
                    cmd.Parameters.AddWithValue("@dtcompile", DateTime.Now);
                    cmd.Parameters.AddWithValue("@name", name); 

                    int ret = cmd.ExecuteNonQuery();

                    cmd.Dispose();
                }

            con.Close();

            return script;
        }

        /// <summary>
        /// hg cat -r tip $file 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static string getCode(Cred cred, string file)
        {
            //            проблемы с русскими именами файлов
            //
            //            ProcessStartInfo startInfo = new ProcessStartInfo("hg");
            //            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            //
            //            Encoding from = Encoding.Unicode;
            //            Encoding to   = Encoding.GetEncoding(1251);
            //            string file2 = to.GetString(from.GetBytes(file));
            //            
            //            startInfo.Arguments = "cat -r tip " + file2;
            //
            //            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            //            startInfo.CreateNoWindow = false;
            //            startInfo.UseShellExecute = false;
            //            startInfo.RedirectStandardOutput = true;
            //            startInfo.StandardOutputEncoding = Encoding.GetEncoding(65001); /// Mercurial все вывыливает в кодировке системы тут : win1251, в Windows 7 может быть иначе
            //            Process process = Process.Start(startInfo);
            //
            //            string output = process.StandardOutput.ReadToEnd();
            //
            //            process.WaitForExit();
            //
            //            return output;

            string workingDirectory = cred.cwd;
            string fileName = workingDirectory + "/" + file;
            string code = File.ReadAllText(fileName);

            return code;
        }

        // запхнуть свежую версию библиотечки в табличку скриптов
        public static void export(Cred cred, string dllFileName)
        {
            //string dllFileName = cred.cwd + "/" + cred.assm + ".dll";
            if (!File.Exists(dllFileName))
                throw new DllNotFoundException(dllFileName);

            byte[] bytes = File.ReadAllBytes(dllFileName);

            Assembly assm = System.Reflection.Assembly.ReflectionOnlyLoad(bytes);

            string name = assm.FullName;

            SqlConnection con = new SqlConnection(cred.connectionString());
            con.Open();
            using (SqlCommand cmd = con.CreateCommand())
            {
                /// ms sql merge очень крив, читаем потом insert или update

                cmd.CommandText = @"SELECT iddocscript FROM docscript WHERE name = @name";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@name", name);
                
                int id;
                object o = cmd.ExecuteScalar();
                if (o != null && int.TryParse(o.ToString(), out id))
                {
                    // update
                    cmd.CommandText = @"UPDATE docscript SET dll = @dll, dtcompile=@date WHERE iddocscript = @id";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@dll", bytes);
                    cmd.Parameters.AddWithValue("@date", DateTime.Now);
                    int n = cmd.ExecuteNonQuery();

                }
                else
                {
                    //get newid from underground
                    cmd.CommandText = @"SELECT ISNULL(min(iddocscript),-1) FROM docscript WHERE iddocscript <0";
                    cmd.Parameters.Clear();
                    id = (int) cmd.ExecuteScalar();

                    // insert
                    cmd.CommandText = @"insert docscript(iddocscript, name, dll, dtcompile) VALUES(@id, @name, @dll, @date)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@dll", bytes);
                    cmd.Parameters.AddWithValue("@date", DateTime.Now);
                    int n = cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
