using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace common
{
    public class IncorrectUsageEx : Exception { }
    public class NothingToDoEx : Exception { }
    public class CompileExcexption : Exception
    {
        public CompilerResults compilerResults;
        public CompileExcexption(CompilerResults res)
        {
            compilerResults = res;
        }
    }

    public class DuplicateScriptNameException : Exception
    {
        public string name { get; private set; }

        public DuplicateScriptNameException(string name)
        {
            this.name = name;
        }
    }

    public class HgrcException : Exception {}
    public class InjectEx : Exception { }
    public class InjectOcuped : Exception { }

    public class IpcException : Exception
    {
        public IpcException(string message)
            : base(message)
        {
        }
    }

}
