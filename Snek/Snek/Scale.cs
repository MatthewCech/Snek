using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MoonSharp;
using MoonSharp.Interpreter;

namespace Snek
{
    class Scale
    {
        public string Name => Path.GetFileNameWithoutExtension(path).Trim();
        public bool Exists => File.Exists(path);

        private string path;
        private Script env;
        private int lastHash;


        public Scale(string path)
        {
            this.path = path;
            VerifyRefreshed();
        }

        public void VerifyRefreshed()
        {
            string raw = File.ReadAllText(path);
            int curhash = raw.GetHashCode();

            if (lastHash != curhash)
            {
                this.env = new Script();
                env.DoString(raw);
                lastHash = curhash;
            }
        }

        //  - isIndicated : Is this plugin being called explicitly? 
        //  - message   : the arguments sent with the command.
        public List<string> DoPlugin(bool isIndicated, string message)
        {
            // 'isIndexed' is referred to as 'prefixed' in lua examples.
            DynValue res = env.Call(env.Globals["plugin"], isIndicated, message);

            if (res.IsNil())
                return null;

            List<string> output = new List<string>();

            // See if we're a table, and cast out values to strings.
            if (res.Type == DataType.Table)
            {
                foreach(DynValue v in res.Table.Values)
                {
                    output.Add(v.CastToString());
                }
            }
            else
            {
                // By default, attempt to return whatever we got from the script as a string.
                output.Add(res.CastToString());
            }

            return output;
        }
    }
}
