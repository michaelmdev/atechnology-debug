using System;
using System.IO;

namespace common
{
    [Serializable]
    public class Cred
    {
        private const string HOOK = @"[hook]";      // секция в hgrc
        private const string FILE = @"/.hg/hgrc";   // путь до hgrc относительно корня проекта

        public string server { get; private set; }
        public string db { get; private set; }
        public string user { get; private set; }
        public string pass { get; private set; }
        public string windraw { get; private set; }
        public string cwd { get; private set; }
        public string assm { get; private set; }

        public string connectionString()
        {
            return @"Data Source=" + server + ";Initial Catalog=" + db + ";Persist Security Info=True;User ID=" + user + ";Password=" + pass;
        } 

        public Cred(string cwd)
        {
            this.cwd = cwd;

            string hgrc = cwd + FILE;
            try
            {
                if (!File.Exists(hgrc))
                {
                    // нас могут запустить не в корневой папке проекта
                    // попытаемся подняться вверх в поисках папки .hgrc не поднимаясь в самый корень диска
                    DirectoryInfo directoryInfo;
                    while ((directoryInfo = Directory.GetParent(cwd)) != null && directoryInfo.Parent != null)
                    {
                        cwd = directoryInfo.FullName;
                        hgrc = cwd + FILE;
                        if (File.Exists(hgrc))
                            break;  // нашлись
                    }
                }

                // теперь тока на выход
                if (!File.Exists(hgrc))
                    throw new HgrcException();

                string[] hgStrings = File.ReadAllLines(hgrc);

                bool f = false;
                foreach (string s in hgStrings)
                {
                    if (!f)
                    {
                        if (s == HOOK)
                            f = true;
                    }
                    else
                    {
                        if (s.StartsWith("[") && s.EndsWith("]"))    // начало следущей секции - абзац
                            break;      
                        
                        // обработка строки нашей конфигурации
                        string[] param = s.Split('=');
                        // требуем paramName = paramValue
                        if (param.Length == 2)
                        {
                            switch (param[0].Trim().ToLower())
                            {
                                case "server":
                                    server = param[1].Trim();
                                    break;
                                case "db":
                                    db = param[1].Trim();
                                    break;
                                case "user":
                                    user = param[1].Trim();
                                    break;
                                case "pass":
                                    pass = param[1].Trim();   // todo encript pass 
                                    break;
                                case "windraw":
                                    windraw = param[1].Trim();
                                    break;
                                case "assm":
                                    assm = param[1].Trim(); // имя сборки
                                    break;

                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw new HgrcException();
            }
            if (!isValid())
                throw new HgrcException();
        }

        public bool isValid()
        {
            return
                server != null
                && db != null
                && user != null
                && pass != null
                && windraw != null;
        }
    }
}
