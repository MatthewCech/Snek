using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net.NetworkInformation;
using Discord;
using System.Text.Json;
using System;

namespace Snek.Utils
{
    // Quick and dirty INI read/write WITHOUT LABELS for configs and whatnot. Not to INI spec
    // This works as an INI file in other parsers, but other parsers may not output in this format.
    // - Keys are case insensitive, valeus are not.
    // - No multiline
    // - Vaguely thread safe
    public class PseudoINI
    {
        // Properties
        public string Path { get; private set; }
        public bool Exists => File.Exists(Path);
        public DateTime LastTimeWritten => File.GetLastWriteTimeUtc(Path);


        // Variables
        private string[] reserved = { "=", ";", "\n", "\r" };
        private static Mutex interacting;


        // Constructor
        public PseudoINI(string path)
        {
            interacting = new Mutex();
            interacting.WaitOne();
            
            Path = path;

            // Attempt to resolve path in parent if it doesn't exist. Only try 10 times.
            for(int i = 0; i < 10 && !Exists; ++i)
                Path = "../" + Path;

            interacting.ReleaseMutex();
        }


        // Sets an item if it's present, add if it's not.
        public bool WriteItem(string key, string value)
        {
            // Invalid characters not permitted.
            foreach (string s in reserved)
            {
                if (key.Contains(s))
                    return false;
            }

            try
            {
                List<KeyValuePair<string, string>> vals = Snapshot();

                // Case: no file / null
                if (vals == null)
                {
                    vals = new List<KeyValuePair<string, string>>();
                    vals.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    bool found = false;
                    for (int i = 0; i < vals.Count; ++i)
                    {
                        // Case: Existing key
                        if (KeyEquals(vals[i].Key, key))
                        {
                            vals[i] = new KeyValuePair<string, string>(key, value);
                            found = true;
                            break;
                        }
                    }

                    // Case: key doesn't exist but file does
                    if (!found)
                    {
                        vals.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                string toWrite = Compose(vals);

                interacting.WaitOne();
                File.WriteAllText(Path, toWrite);
                interacting.ReleaseMutex();
            }
            catch
            {
                return false;
            }

            return true;
        }


        // Gets the value of the specific key from the INI file. Returns 'null' if key or file isn't found.
        public string ReadItem(string key)
        {
            List<KeyValuePair<string, string>> snap = Snapshot();

            if (snap != null)
            {
                foreach (KeyValuePair<string, string> pair in snap)
                {
                    if (KeyEquals(pair.Key, key))
                        return pair.Value;
                }
            }

            return null;
        }


        // Consistent way to compare if two keys are the same
        private bool KeyEquals(string k1, string k2)
        {
            return k1.Trim().ToLowerInvariant().Equals(k2.Trim().ToLowerInvariant());
        }


        // Snapshot reading style - the file isn't kept open. This isn't particularly efficient.
        // May throw an error if it explodes.
        private List<KeyValuePair<string, string>> Snapshot()
        {
            if (!Exists)
                return null;

            interacting.WaitOne();
            try
            {
                List<KeyValuePair<string, string>> parsed = new List<KeyValuePair<string, string>>();
                string text = File.ReadAllText(Path).Trim();
                string[] split = text.Split('\n');

                foreach (string s in split)
                {
                    string[] line = s.Trim().Split('=');
                    parsed.Add(new KeyValuePair<string, string>(line[0], line[1]));
                }

                interacting.ReleaseMutex();
                return parsed;
            }
            catch(System.Exception)
            {
                interacting.ReleaseMutex();
                return null;
            }
        }

        // Constructs the formatted ini file in the event we wish to write it out.
        private string Compose(List<KeyValuePair<string, string>> toCompose)
        {
            string built = "";
            foreach (KeyValuePair<string, string> pair in toCompose)
            {
                built += pair.Key + "=" + pair.Value + "\n";
            }

            return built;
        }
    }
}
