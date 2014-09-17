using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;
using Atechnology.DBConnections2;
using Atechnology.ecad.Calc;
using Atechnology.Components;



namespace common
{
    public class Pinger : MarshalByRefObject
    {
        //
        public int i = 0;

        public int serv()
        {
            AtReflection.Load(dbconn._db);
            AtReflection.LoadDoc(dbconn._db);
            AtReflection.LoadOrderEvent(dbconn._db);
            AtReflection.LoadDesignerEvent(dbconn._db);
            return ++i;
        }

        public int serv(Cred cred)
        {
            // hook compile запускается в папке сборки проекта там где лажит .dll 
            // поэтому cred.cwd = /__RED/bin/Debug/
            // но все еще запутаннее! dll запускается от имени winDraw из /windraw
            // поэтому делаем путь полный сами
            string dllFileName = cred.cwd + "/" + cred.assm + ".dll";
            if (!File.Exists(dllFileName))
                throw new DllNotFoundException(dllFileName);

            byte[] buf = File.ReadAllBytes(dllFileName);
            Assembly assembly = Assembly.Load(buf);
            Type[] assemlyTypes = assembly.GetExportedTypes();
            List<string> assemlyTypeNames = new List<string>(assemlyTypes.Length);
            foreach (Type type in assemlyTypes)
                assemlyTypeNames.Add(type.FullName); // FullName ?

            modelscript(assembly, assemlyTypeNames);
            docscript(assembly, assemlyTypeNames);
            orderevent(assembly, assemlyTypeNames);
            designerevent(assembly, assemlyTypeNames);

            return ++i;
        }

        private static void modelscript(Assembly myAssembly, List<string> myNames)
        {
            AtReflection.script.Clear();
            using (SqlDataReader dataReader =dbconn._db.GetDataReader2("select numpos, name, modelpart_name, dll, typ, idversion from view_modelscript order by modelpart_numpos, numpos"))
            {

                while (dataReader.Read())
                {
                    AtScript atScript = new AtScript();
                    atScript.Name = dataReader["name"].ToString();
                    atScript.NumPos = Useful.GetInt32(dataReader["numpos"]);
                    atScript.ModelPart = dataReader["modelpart_name"].ToString();
                    atScript.ScriptType = Convert.ToInt32(dataReader["typ"]);
                    atScript.idversion = (int) dataReader["idversion"];

                    //
                    string typeFullName = Script.GetFullTypeName("modelscript",atScript.Name);
                    if (myNames.Contains(typeFullName))
                    {
                        atScript.scriptclass = myAssembly.CreateInstance(typeFullName);
                    }
                    else
                    {
                        Assembly assembly = Assembly.Load(Atechnology.Components.ZipArchiver.UnZip((byte[]) dataReader["dll"]));
                        atScript.scriptclass = assembly.CreateInstance("Atechnology.ecad.Calc.RunCalc");
                    }

                    Type type = atScript.scriptclass.GetType();
                    atScript.start = type.GetMethod("Run");
                    AtReflection.script.Add(atScript);
                }
            }
        }

        private static void docscript(Assembly myAssembly, List<string> myNames)
        {
            AtReflection.docscript.Clear();
            using (SqlDataReader dataReader = dbconn._db.GetDataReader2("select * from docscript where deleted is null and activescript = 1"))
            {
                while (dataReader.Read())
                {
                    if (dataReader["dll"] != DBNull.Value)
                    {
                        AtScript atScript = new AtScript();
                        atScript.Name = dataReader["name"].ToString();
                        atScript.NumPos = Useful.GetInt32(dataReader["numpos"]);
                        atScript.iddocscript = Useful.GetInt32(dataReader["iddocscript"]);

                        //
                        string typeFullName = Script.GetFullTypeName("docscript", atScript.Name);
                        if (myNames.Contains(typeFullName))
                        {
                            atScript.scriptclass = myAssembly.CreateInstance(typeFullName);
                        }
                        else
                        {
                            Assembly assembly = Assembly.Load(Atechnology.Components.ZipArchiver.UnZip((byte[]) dataReader["dll"]));
                            atScript.scriptclass = assembly.CreateInstance("Atechnology.ecad.Calc.RunCalc");
                        }

                        System.Type type = atScript.scriptclass.GetType();
                        atScript.start = type.GetMethod("Run");
                        AtReflection.docscript.Add(atScript);
                    }
                }
            }
        }

        private static void orderevent(Assembly myAssembly, List<string> myNames)
        {
            AtReflection.orderevent.Clear();
            using (SqlDataReader dataReader = dbconn._db.GetDataReader2("select * from orderevent where deleted is null and compiled = 1 and idordereventgroup = (select top 1 idordereventgroup from ordereventgroup where isactive = 1)"))
            {
                while (dataReader.Read())
                {
                    if (dataReader["dll"] != DBNull.Value)
                    {
                        AtScript atScript = new AtScript();
                        atScript.Name = dataReader["name"].ToString();
                        atScript.NumPos = Useful.GetInt32(dataReader["numpos"]);
                        atScript.idorderevent = Useful.GetInt32(dataReader["idorderevent"]);

                        //
                        string typeFullName = Script.GetFullTypeName("orderevent", atScript.Name);
                        if (myNames.Contains(typeFullName))
                        {
                            atScript.scriptclass = myAssembly.CreateInstance(typeFullName);
                        }
                        else
                        {
                            Assembly assembly = Assembly.Load(Atechnology.Components.ZipArchiver.UnZip((byte[]) dataReader["dll"]));
                            atScript.scriptclass = assembly.CreateInstance("Atechnology.ecad.Calc.RunCalc");
                        }

                        Type type = atScript.scriptclass.GetType();
                        atScript.start = type.GetMethod("Run");
                        AtReflection.orderevent.Add(atScript);
                    }
                }
            }
        }

        private static void designerevent(Assembly myAssembly, List<string> myNames)
        {
            AtReflection.designerevent.Clear();
            using (SqlDataReader dataReader = dbconn._db.GetDataReader2("select * from designerevent where deleted is null and compiled = 1"))
            {
                while (dataReader.Read())
                {
                    if (dataReader["dll"] != DBNull.Value)
                    {
                        AtScript atScript = new AtScript();
                        atScript.Name = dataReader["name"].ToString();
                        atScript.iddesignerevent = Useful.GetInt32(dataReader["iddesignerevent"]);
                        atScript.isactive = Convert.ToInt32(dataReader["isactive"]);

                        //
                        string typeFullName = Script.GetFullTypeName("designerevent", atScript.Name);
                        if (myNames.Contains(typeFullName))
                        {
                            atScript.scriptclass = myAssembly.CreateInstance(typeFullName);
                        }
                        else
                        {
                            Assembly assembly = Assembly.Load(Atechnology.Components.ZipArchiver.UnZip((byte[]) dataReader["dll"]));
                            atScript.scriptclass = assembly.CreateInstance("Atechnology.ecad.Calc.RunCalc");
                        }

                        Type type = atScript.scriptclass.GetType();
                        atScript.start = type.GetMethod("Run");
                        AtReflection.designerevent.Add(atScript);
                    }
                }
            }
        }
    
    
    
    }
}

                //                finally
                //                {
                //                    if (dataReader2 != null)
                //                        dataReader2.Close();
                //                    db.CloseDB();
                //                }

        /*
            AtReflection.designerevent.Clear();	
            byte[] buf = System.IO.File.ReadAllBytes(@"D:\__RED\bin\Debug\__RED.dll");
            AtScript atScript = new AtScript();
            atScript.Name = "Designer";
            atScript.iddesignerevent = 0;
            atScript.isactive = 1;
            Assembly assembly = Assembly.Load(buf);
            string classFullName = @"Atechnology.ecad.Calc.Designer_Events.дизайн.RunCalc";
            atScript.scriptclass = assembly.CreateInstance(classFullName);
            System.Type type = atScript.scriptclass.GetType();
            atScript.start = type.GetMethod("Run");
            AtReflection.designerevent.Add(atScript);
        */




