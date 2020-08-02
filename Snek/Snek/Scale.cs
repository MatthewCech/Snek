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
        public string DoPlugin(bool isIndicated, string message)
        {
            // 'isIndexed' is referred to as 'prefixed' in lua examples.
            DynValue res = env.Call(env.Globals["plugin"], isIndicated, message);

            if (res.IsNil())
                return null;

            return res.CastToString();
        }
    }
}
