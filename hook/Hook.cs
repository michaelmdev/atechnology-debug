using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.Remoting;
using System.Security;
using System.Text;
using common;

namespace hook
{
    class Hook
    {
        static void Main(string[] args)
        {
            try
            {
                string cwd = Environment.CurrentDirectory;
                if (args.Length > 0)
                {
                    string cmd = args[0];

                    // заставит создавать файлы если их еще нет , ну или как-то иначе
                    bool force = args.Length > 1 && args[1].ToLower() == "-f";

                    // todo set -f пишет весь проект не смотря на hg log -v -r tip

                    Cred cred = new Cred(cwd);

                    switch (cmd)
                    {
                        case "get":
                            Core.get(cred, force);
                            break;

                        case "compile":
                            Core.compile(cred);
                            break;

                        case "commit": // commit;
                            Core.commit(cred);
                            break;

                        case "inject":
                            Core.inject(cred);
                            break;

                        case "eject":
                            Core.eject(cred);
                            break;
                        case "export":
                            if (args.Length > 1)
                            {
                                string exportDll = args[1].Trim();
                                Core.export(cred,exportDll);
                            }
                            else
                            {
                                throw  new IncorrectUsageEx();
                            }
                            break;

                    }
                }
                else
                {
                    throw new IncorrectUsageEx();
                }
            }
            catch (IncorrectUsageEx)
            {
                Console.WriteLine("incorrect usaget. hook { get [-f] | set | compile | inject | eject }");
                Environment.Exit(1);
            }
            catch (HgrcException)
            {
                Console.WriteLine(".hg/hgrc file problem. Exist ? Config ? RTFM.");
                Environment.Exit(2);
            }
            catch (NothingToDoEx)
            {
                Console.WriteLine("nothing to do");
                Environment.Exit(0);
            }
            catch (CompileExcexption ex)
            {
                Console.WriteLine("compile exception. check complieError.txt in project root");
                foreach (string s in ex.compilerResults.Output)
                    Console.WriteLine(s);

                Environment.Exit(3);
            }
            catch (DuplicateScriptNameException ex)
            {
                Console.WriteLine("duplicative script name, rename one script");
                Console.WriteLine(ex.name);
                Environment.Exit(4);
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine("file not found :" + ex.Message);
                Environment.Exit(5);
            }
            catch (IpcException ex)
            {
                Console.WriteLine("check ipc config at " + ex.Message);
                Environment.Exit(6);
            }
            catch (RemotingException ex)
            {
                Console.WriteLine("check ipc config at " + ex.Message);
                Environment.Exit(7);
            }
            catch (SecurityException ex)
            {
                Console.WriteLine("check ipc config at " + ex.Message);
                Environment.Exit(8);
            }
            catch (InjectEx ex)
            {
                Console.WriteLine("inject ex. call support");
                Environment.Exit(9);
            }
            catch (InjectOcuped ex)
            {
                Console.WriteLine("event already used. do it by hand");
                Environment.Exit(9);
            }


            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(-1);
            }


        }
    }
}
