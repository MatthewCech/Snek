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
        public bool HasChanged => File.GetLastWriteTime(path) != lastRunTime; 

        private string path;
        private Script env;
        private DateTime lastRunTime;
        public Scale(string path)
        {
            this.path = path;
            Refresh();
        }

        public void Refresh()
        {
            this.env = new Script();
            string raw = File.ReadAllText(path);
            env.DoString(raw);
            lastRunTime = File.GetLastWriteTime(path);
        }

        public string DoPlugin(string message)
        {
            DynValue res = env.Call(env.Globals["plugin"], message);

            if (res.IsNil())
                return null;

            return res.CastToString();
        }
    }
}
