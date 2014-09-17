using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;
using common;
using NUnit.Framework;

namespace test
{
    [TestFixture]
    public class TestCore
    {
        private Cred cred;
        SqlConnection con;

        [TestFixtureSetUp]
        public void init()
        {
            cred = new Cred(@"D:\__kpi\kpi\bin\Debug");

            
            con = new SqlConnection(cred.connectionString());
            con.Open();


            /// delegate
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
            {
                string name = args.Name;

                using (SqlCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"SELECT dll FROM docscript WHERE name = @name";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@name", name);
                    object o = cmd.ExecuteScalar();

                    byte[] bytes = o as byte[];
                    if (bytes != null)
                    {
                        Assembly assm = System.Reflection.Assembly.Load(bytes);
                        return assm;
                    }
                    else
                    {
                        return null;
                    }
                }
            };
        }

        [Test]
        public void exportTest()
        {
            Core.export(cred,@"d:\__kpi\kpi\bin\Debug\kpi.dll");
        }
        
    }
}
