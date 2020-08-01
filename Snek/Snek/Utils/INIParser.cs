using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net.NetworkInformation;
using Discord;
using System.Text.Json;

namespace Snek.Utils
{
    // Quick and dirty INI read/write WITHOUT LABELS for configs and whatnot. Not to INI spec
    // This works as an INI file in other parsers, but other parsers may not output in this format.
    // - Not thread safe, but won't burn the whole program to the floor if it fails.
    // - Keys are case insensitive, valeus are not.
    // - No multiline
    public class PseudoINI
    {
        // Properties
        public string Path { get; private set; }
        public bool Exists => File.Exists(Path);

        private string[] reserved = { "=", ";", "\n" };
        
        public PseudoINI(string path)
        {
            Path = path;
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
                File.WriteAllText(Path, toWrite);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public string ReadItem(string key)
        {
            List<KeyValuePair<string, string>> snap = Snapshot();
            foreach(KeyValuePair<string, string> pair in snap)
            {
                if (KeyEquals(pair.Key, key))
                    return pair.Value;
            }

            return null;
        }

        private bool KeyEquals(string k1, string k2)
        {
            return k1.Trim().ToLower().Equals(k2.Trim().ToLower());
        }

        // Snapshot reading style - the file isn't kept open. This isn't particularly efficient.
        // May throw an error if it explodes.
        private List<KeyValuePair<string, string>> Snapshot()
        {
            if (!Exists)
                return null;

            List<KeyValuePair<string, string>> parsed = new List<KeyValuePair<string, string>>();
            string text = File.ReadAllText(Path).Trim();
            string[] split = text.Split('\n');
                
            foreach(string s in split)
            {
                string[] line = s.Trim().Split('=');
                parsed.Add(new KeyValuePair<string, string>(line[0], line[1]));
            }

            return parsed;
        }

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
