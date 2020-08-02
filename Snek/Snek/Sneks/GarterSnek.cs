using Discord;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using MoonSharp.Interpreter;
using Snek.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Snek.Sneks
{
    public class GarterSnek : ISnek
    {
        private DiscordSocketClient client;

        public string Name { get; private set; }

        private string token;
        private string indicator;
        private string scalesPath;
        private List<Scale> scales;
        
        private PseudoINI config;
        private DateTime configLastWrite;
        private Mutex preUpdateMutex;



        public GarterSnek(string configPath)
        {
            scales = new List<Scale>();
            config = new PseudoINI(configPath);
            preUpdateMutex = new Mutex();

            configLastWrite = config.LastTimeWritten;
            this.Name = config.ReadItem("name");
            this.token = config.ReadItem("token");
            this.indicator = config.ReadItem("indicator");

            scalesPath = "scales/";
            if(!Directory.Exists(scalesPath))
                Directory.CreateDirectory(scalesPath);
        }


        // Bold of you to assume a snek can Run(), it has no legs! It *can* slither tho.
        public void Slither()
        {
            Coil().GetAwaiter().GetResult();
        }


        // Sneks are comfy in coils, so set stuff up while coiling
        private async Task Coil()
        {
            client = new DiscordSocketClient();
            client.MessageReceived += Client_MessageReceived;

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await Task.Delay(-1);
        }


        // Happens before a given command is run.
        private void LazyPreUpdate()
        {
            // Selective update for config if it's changed
            if (config.Exists)
            {
                if (config.LastTimeWritten != configLastWrite)
                    this.indicator = config.ReadItem("indicator");
            }

            // Look at our scales - add new ones, and update existing, and remove old.
            // Go from back to front to avoid issues.
            for (int i = scales.Count - 1; i > 0; i--)
            {
                // If it doesn't exist any more, remove it.
                if (!scales[i].Exists)
                    scales.RemoveAt(i);

                // If it changed, update it.
                if (scales[i].HasChanged)
                    scales[i].Refresh();
            }

            // See if any new scales were added. If so, add them.
            string[] filePaths = Directory.GetFiles(scalesPath);
            foreach (string filepath in filePaths)
            {
                string item = Path.GetFileNameWithoutExtension(filepath).Trim();
                bool found = false;
                foreach (Scale s in scales)
                {
                    if (s.Name.ToLowerInvariant() == item.ToLowerInvariant())
                    {
                        found = true;
                        break;
                    }
                }

                // Add scales we haven't seen yet
                if (!found)
                    scales.Add(new Scale(filepath));
            }
        }


        // When a message is sent in the discord, this is called. 
        // May be happening multiple times at once.
        private async Task Client_MessageReceived(SocketMessage raw)
        {
            // Perform our lazy update - only runs at the absolute last second.
            preUpdateMutex.WaitOne();
            LazyPreUpdate();
            preUpdateMutex.ReleaseMutex();

            // The snek doesn't care what it's doing itself, that's ok. It's just doin' what it do.
            if (raw.Author.Id == client.CurrentUser.Id)
                return;

            // Prep the message and make sure we have something.
            string message = raw.Content.Trim();
            if (message == null && message.Length <= 0)
                return;

            // First, see if we should even observe the command.
            if (message.StartsWith(indicator))
            {
                const string scaleKey = "scale";
                const string scaleKeyAlt = "scales";

                // Parse command and argument(s).
                string command = message.Split(null)[0].Trim().ToLowerInvariant().Substring(1);
                string args = "";
                if (message.Length - command.Length > 0)
                    args = message.Substring(command.Length + 1).Trim();

                string res = null;

                // See if we're calling 'scale' to make a new command. If not, try and run another command.
                if (command.Equals(scaleKey) || command.Equals(scaleKeyAlt))
                {
                    // parse subcommand and subargument(s)
                    string subCommand = args.Split(null)[0].Trim().ToLowerInvariant();
                    string subArgs = "";
                    if (args.Length - subCommand.Length > 0)
                         subArgs = args.Substring(subCommand.Length + 1).Trim();

                    res = Snek_DoScale(subCommand, subArgs);
                }
                else
                { 
                    // Look through other commands we have set up
                    foreach(Scale s in scales)
                    {
                        if(command == s.Name)
                            res = s.DoPlugin(args);
                    }
                }

                // Send a response if we've got one to send.
                if (res != null)
                    await raw.Channel.SendMessageAsync(res);
            }
        }


        // This is the only hardcoded command set - the group that lets you make scales.
        private string Snek_DoScale(string subCommand, string args)
        {
            switch (subCommand)
            {
                case "help":
                case "guide":
                case "how":
                case "howto":
                case "new":
                case "?":
                    {
                        string howTo = "To add a new scale, you'll want to make sure you use the `" + indicator + "scale add <name>` command.\n";
                        howTo += "Here's some boilerplate for adding a new scale that you can copy and paste!\n";
                        howTo += "` " + indicator + "scale add Poke`\n";
                        howTo += "```lua\n";
                        howTo += "-- Since this is named Poke, 'poke' is the required command word. It's case insensitive.\n";
                        howTo += "function plugin(msg)\n";
                        howTo += "\tif msg == \"snoot\" then\n";
                        howTo += "\t\treturn \"blep\";\n";
                        howTo += "\telse\n";
                        howTo += "\t\treturn \"hiss\";\n";
                        howTo += "\tend\n";
                        howTo += "end\n";
                        howTo += "```\n";

                        return howTo;
                    }
                    break;

                case "cat":
                case "type":
                case "print":
                case "dump":
                    {
                        if (args != null || args.Length > 0)
                        {
                            if (!args.Contains(".lua"))
                                args = args.Trim() + ".lua";

                            string path = Path.Combine(scalesPath, args);
                            if (File.Exists(path))
                            {
                                string toDump = "```lua\n";
                                toDump += File.ReadAllText(path);
                                toDump += "```";
                                return toDump;
                            }
                            else
                            {
                                return "I couldn't find a scale with the name `" + args + "`!";
                            }
                        }
                        else
                        {
                            return "I can't print out nothing! Try specifying a scale by name, like `" + indicator + "scale " + subCommand + " <name>`!";
                        }
                    }
                    break;

                // Adds scale by case insensitve name, with behavior as follows.
                case "add":
                    {
                        if (args == null)
                            return "Nothing to add! Try `" + indicator + "scale guide`!";

                        int start = args.IndexOf("`");
                        string name = args.Substring(0, start).Trim().ToLowerInvariant();
                        string body = args.Substring(start).Trim();

                        // Figure out how many leading characters to ignore.
                        int offsetForward = 0;
                        foreach (char c in body)
                        {
                            if (c != '`' && !string.IsNullOrWhiteSpace("" + c))
                            {
                                if (body.Substring(offsetForward).ToLowerInvariant().StartsWith("lua"))
                                {
                                    offsetForward += 3;
                                    continue;
                                }

                                break;
                            }

                            ++offsetForward;
                        }

                        // If the user was using syntax highlighting in the markdown, ignore that lua keyword
                        

                        int offsetBack = 0;
                        foreach(char c in body.Reverse())
                        {
                            if (c != '`' && !string.IsNullOrWhiteSpace("" + c))
                                break;

                            ++offsetBack;
                        }

                        body = body.Substring(offsetForward, body.Length - (offsetBack + offsetForward));

                        Script envTemp = new Script();
                        try
                        {
                            envTemp.DoString(body);
                        }
                        catch(Exception e)
                        {
                            return "Whoops, looks like there was an error in that code - I can't add that as a scale until it compiles!\n`" + e.Message + "`";
                        }

                        try
                        {
                            string toAppend = ".lua";
                            if (name.Contains(".lua"))
                                toAppend = "";

                            string path = Path.Combine(scalesPath, name.Trim() + toAppend);
                            File.WriteAllText(path, body);
                            Scale scale = new Scale(path);
                            scales.Add(scale);

                            return "A scale named `" + name + "` was created! Snek currently has " + scales.Count + (scales.Count > 1 ? " scales" : " scale") + "!";
                        }
                        catch(Exception e)
                        {
                            return "Hmm, I couldn't make that into a scale...\n" + e.Message;
                        }

                    }
                    break;


                    // Removes a scale by case insensive name
                case "remove":
                    {
                        if(args != null && args.Length > 0)
                        {
                            string name = args.ToLowerInvariant().Trim();
                            if (!args.Contains(".lua"))
                                args = name + ".lua";

                            string path = Path.Combine(scalesPath, args);
                            if (File.Exists(path))
                            {
                                bool removed = false;
                                for(int i = scales.Count - 1; i > 0; --i)
                                {
                                    if(scales[i].Name.ToLowerInvariant().Trim().Equals(name))
                                    {
                                        scales.RemoveAt(i);
                                        File.Delete(path);
                                        removed = true;
                                        break;
                                    }
                                }

                                if (!removed)
                                    return "Hmm, I found something at " + path + ", but it doesn't seem to be a scale so I won't delete it.";

                                return "I deleted my scale at `" + path + "`!\nCustom scales left: " + scales.Count;
                            }
                            else
                            {
                                return "I couldn't find a scale with the name `" + args + "`";
                            }
                        }
                        else
                        {
                            return "Whoops - That's not how you remove scales! Remember to specify a scale name! `" + indicator + "scale remove <name>`!";
                        }
                    }
                    break;


                    // Lists all registered scales that the snek has
                case "list":
                    {
                        string comp = "";

                        if (scales.Count > 0)
                        {
                            comp += "Found " + scales.Count + (scales.Count > 1 ? " scales" : " scale") + ": ```diff";
                            foreach (Scale s in scales)
                            {
                                comp += "\n+ " + s.Name;
                            }
                            comp += "\n```";
                            comp += "\n You can add and remove scales with `" + indicator + "scale add <name> <content>` and `" + indicator + "scale remove <name>`";
                        }
                        else
                        {
                            return "I only have my default scales! Try adding some with `" + indicator + "scale add <name> <content>`!";
                        }

                        return comp;
                    }


                    // Let people know how to interact with snek
                default:
                    {
                        return "To look at snek's scales, try `" + indicator + "scale list`!";
                    }
            }

            return null;
        }
    }
}
