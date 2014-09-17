using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using Atechnology.ecad.Calc;

namespace common
{
    // исторически сложились немного другие имена
    public class Script
    {
        public const string GUID_TAG_OPEN  = "///HGOOK_GUID ";
        public const string GUID_TAG_CLOSE = "\n";   

        // исторические имена папок скриптов на файловом носителе
        public const string Calc_Scripts = "Calc_Scripts";
        public const string Doc_Events = "Doc_Events";
        public const string Doc_Scripts = "Doc_Scripts";
        public const string Designer_Events = "Designer_Events";

        // имена таблиц
        public const string modelscript = "modelscript";
        public const string orderevent = "orderevent";
        public const string docscript = "docscript";
        public const string designerevent = "designerevent";

        public string NameSpace { get; protected set; }
        public string TableName { get; protected set; }
        public string ScriptName { get; protected set; }
        public string PureCode { get; protected set; }
        public string VisualCode { get; protected set; }
        public string FileName { get; protected set; }

        public ScriptType ScriptType { get; protected set; }

        // сделать объект из кода VisualStudio
        public Script(string code)
        {
            VisualCode = code;
            parseVisualCode();
        }

        // создать из SQL кода 
        public Script(string code, string tableName, string scriptName)
        {
            PureCode = code;
            TableName = tableName;
            ScriptName = scriptName;
            parsePureCode();
        }

        private string getTableName()
        {
            switch (NameSpace)
            {
                case Calc_Scripts:
                    return modelscript;
                case Doc_Events:
                    return orderevent;
                case Doc_Scripts:
                    return docscript;
                case Designer_Events:
                    return designerevent;
                default:
                    throw new ArgumentException(NameSpace);
            }
        }

        private string getNameSpace()
        {
            switch (TableName)
            {
                case modelscript:
                    return Calc_Scripts;
                case orderevent:
                    return Doc_Events;
                case docscript:
                    return Doc_Scripts;
                case designerevent:
                    return Designer_Events;
                default:
                    throw new ArgumentException(TableName);
            }
        }

        internal static string GetFullTypeName(string tableName, string name)
        {
            const string AT = "Atechnology.ecad.Calc";
            const string CLASS = "RunCalc";
            string s = AT + "." +getNameSpace(tableName) + "." + ValidScriptName(name) + "." + CLASS;
            return s;
        }

        internal static string getNameSpace(string tableName)
        {
            switch (tableName)
            {
                case modelscript:
                    return Calc_Scripts;
                case orderevent:
                    return Doc_Events;
                case docscript:
                    return Doc_Scripts;
                case designerevent:
                    return Designer_Events;
                default:
                    throw new ArgumentException(tableName);
            }
        }

        private ScriptType getScriptType()
        {
            switch (TableName)
            {
                case modelscript:
                    return ScriptType.ModelScript;
                case orderevent:
                    return ScriptType.DocumentEvent;
                case docscript:
                    return ScriptType.DocScript;
                case designerevent:
                    return ScriptType.DesignerEvent;
                default:
                    throw new ArgumentException(TableName);
            }
        }


        //NameSpace = ? find
        //ScriptName = ? find
        //TableName = getTableName()
        //Guid =      getGuidFromCode();        
        //PureCode =  getPureCode();   
        private void parseVisualCode()
        {
//            Match guidMatch = Regex.Match(VisualCode, "(?<=" + GUID_TAG_OPEN + ").*");
//            if(!guidMatch.Success)
//                throw new ArgumentException(VisualCode);
//            Guid = guidMatch.Value.Trim();

            // find nameSpace & scriptName
            // 
            const string header = @"namespace\s+Atechnology\.ecad\.Calc\.";

            Match match = Regex.Match(VisualCode, header+".*");
            if (!match.Success)
                throw new ArgumentException(VisualCode);

            string nameSpace_scriptName = Regex.Replace(match.Value, header, "");  // могут остаться { а могут и не остаться
            string[] arr = Regex.Split(nameSpace_scriptName, @"[\.\s*{]");

            if(arr.Length<2)
                throw new ArgumentException(match.Value);

            NameSpace = arr[0].Trim();
            ScriptName = arr[1].Trim();
            TableName = getTableName();
            ScriptType = getScriptType();

            // remove HGOOK_GUID line
            // PureCode = Regex.Replace(VisualCode, @"///HGOOK_GUID\s+.*\n", "");
            // clean namespace line
            
            PureCode = Regex.Replace(VisualCode, @"namespace\s+Atechnology\.ecad\.Calc\..*\s*\{", "namespace Atechnology.ecad.Calc\n{");
        }


        internal static string ValidScriptName(string name)
        {
            // valid scriptName !!!
            Regex regex = new Regex(@"[(\s\/\\\~@#$%^&\*()!?,.+-]");
            return regex.Replace(name, "_");
        }

        // 1) repalce namespace
        private void parsePureCode()
        {
            ScriptName = ValidScriptName(ScriptName);

            NameSpace = getNameSpace();

            FileName = NameSpace + "/" + ScriptName + ".cs";

            VisualCode = PureCode.Replace("namespace Atechnology.ecad.Calc", "namespace Atechnology.ecad.Calc." + NameSpace + "." + ScriptName);
        }

        // компилирует в Mirrow папку в папке cwd
        public CompilerResults Compile(string cwd, string[] referencedAssemblies)
        {
            CSharpCodeProvider csharpCodeProvider = new CSharpCodeProvider();

            string mirrow = cwd + @"\" + ".mir";
            Directory.CreateDirectory(mirrow);

            Directory.CreateDirectory(mirrow + "/" + NameSpace);
            string srcFileName = mirrow + @"\" + NameSpace + @"\" + ScriptName + ".cs";
            string dllFileName = mirrow + @"\" + NameSpace + @"\" + ScriptName + ".dll";  // .pdb автоматом тоже

            File.WriteAllText(srcFileName, PureCode);

            CompilerParameters options = new CompilerParameters();
            options.ReferencedAssemblies.AddRange(referencedAssemblies);
            options.OutputAssembly = dllFileName;   
            options.IncludeDebugInformation = true;
            options.GenerateInMemory = false;
            options.CompilerOptions = "/target:library";
            options.GenerateExecutable = false;

            return csharpCodeProvider.CompileAssemblyFromFile(options, srcFileName);
        }
    }
}
