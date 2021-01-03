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
        public static class MagicValues
        {
            public static int MultiMessageDelayMS = 1000;
        }
        
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
            this.scalesPath = config.ReadItem("scalePath");

            Console.WriteLine($"Path for scales: {scalesPath}");
            
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
            }

            // See if any new scales were added. If so, add them.
            string[] filePaths = Directory.GetFiles(scalesPath);
            foreach (string filepath in filePaths)
            {
                string item = Path.GetFileNameWithoutExtension(filepath).Trim();
                bool isNewScale = true;
                foreach (Scale s in scales)
                {
                    if (s.Name.ToLowerInvariant() == item.ToLowerInvariant())
                    {
                        isNewScale = false;
                        break;
                    }
                }

                // Add scales we haven't seen yet
                if (isNewScale)
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

            // First, see if we should passively or actively observe the command.
            bool indicated = false;
            if (message.StartsWith(indicator))
                indicated = true;

            // Parse out the command word, and if it's present, the arguments.
            // Since we can have indicated or nonindicated commands, we have to adjust parsing accordingly.
            int offset = indicated ? indicator.Length : 0;
            string command = message.Split(null)[0].Trim().ToLowerInvariant().Substring(offset);
            string args = "";
            if (message.Length - command.Length > 0)
                args = message.Substring(command.Length + offset).Trim();

            // If indicated, we can act on it as a default C# scale *potentially*, tho it's still
            // valid if not internally defined. The result and what was or wasn't performed based on 
            // this first section determines a lot of the behavior of the lua scales.
            List<string> result = null;
            bool performedInternal = false;
            if (indicated)
            {
                const string scaleKey = "scale";
                const string scaleKeyAlt = "scales";

                // Log out some debug info
                Console.WriteLine($"Message: {message}");

                // See if we're calling 'scale' to make a new command. If not, try and run another command.
                if (command.Equals(scaleKey) || command.Equals(scaleKeyAlt))
                {
                    // parse subcommand and subargument(s)
                    string subCommand = args.Split(null)[0].Trim().ToLowerInvariant();
                    string subArgs = "";
                    if (args.Length - subCommand.Length > 0)
                        subArgs = args.Substring(subCommand.Length + 1).Trim();

                    List<string> output = new List<string>();
                    string res = Snek_DoScale(subCommand, subArgs);
                    
                    if(res != null)
                    {
                        output.Add(res);
                        result = output;
                    }

                    performedInternal = true;
                }
            }

            // If an integrated command wasn't performed, run the plugins.
            if (!performedInternal)
            {
                foreach (Scale s in scales)
                {
                    if (command == s.Name)
                    {
                        s.VerifyRefreshed();
                        result = s.DoPlugin(indicated, args);
                        break;
                    }
                }
            }

            // Send a response if we've got one to send, or if we have multiple queued up we send them with a delay.
            if (result != null)
            {
                for (int i = 0; i < result.Count; ++i)
                {
                    // Sleep a bit to prevent rate limiting if we recieved a set of responses.
                    if (i > 0)
                    {
                        Thread.Sleep(MagicValues.MultiMessageDelayMS);
                    }

                    await raw.Channel.SendMessageAsync(result[i]);
                }

                return;
            }
        }


        // This is the only hardcoded command set - the group that lets you make scales.
        private string Snek_DoScale(string subCommand, string args)
        {
            switch (subCommand)
            {
                // This displays the boilerplate for adding a new scale.
                case "help":
                case "guide":
                case "how":
                case "howto":
                case "new":
                case "?":
                    {
                        string howTo = $"For a list of all commands, use `{indicator}scale`\n\n";
                        howTo += "To add a new scale, you'll want to make sure you use the `" + indicator + "scale add <name>` command.\n";
                        howTo += "Here's some boilerplate for adding a new scale that you can copy and paste!\n\n";
                        howTo += "`" + indicator + "scale add Poke`\n";
                        howTo += "```lua\n";
                        howTo += "-- Since this is named Poke, 'poke' is the (case insensitive) command name.\n";
                        howTo += "-- We check the 'prefixed' bool to see if there was a leading `" + indicator + "`\n";
                        howTo += "function plugin(isPrefixed, args)\n";
                        howTo += "  if isPrefixed then\n";
                        howTo += "    if args == \"snoot\" then\n";
                        howTo += "      return \"*blep*\";\n";
                        howTo += "    else\n";
                        howTo += "      return \"*hiss*\";\n";
                        howTo += "    end\n";
                        howTo += "  end\n";
                        howTo += "end\n";
                        howTo += "```\n";

                        return howTo;
                    }

                // This is for dumping out the contents of an existing scale
                case "type":
                case "cat":
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

                // Changes the contents of an existing scale
                case "update":
                    {
                        string message;
                        KeyValuePair<string, string>? kv = ParseBodyAsLua(args, out message);
                        if (!kv.HasValue)
                            return message;

                        string name = kv.Value.Key;
                        string body = kv.Value.Value;

                        try
                        {
                            if (!name.Contains(".lua"))
                                name += ".lua";

                            string path = Path.Combine(scalesPath, name);
                            if (File.Exists(path))
                            {
                                File.WriteAllText(path, body);
                                return "Ok! I've updated the scale at `" + path + "` for you.";
                            }
                            else
                            {
                                return "Hmm, I can't find a scale at " + path + ". Perhaps the spelling was wrong?\nYou can check with `" + indicator + "scale list`";
                            }

                        }
                        catch(System.Exception e)
                        {
                            return "Something went wrong when trying to update the scale named " + name + "name.\n" + e.Message;
                        }
                    }

                // Adds scale by case insensitve name, with behavior as follows.
                case "add":
                    {
                        if (args == null || args.Trim().Length <= 0)
                            return "Nothing to add! Try `" + indicator + "scale guide`!";

                        string message;
                        KeyValuePair<string, string>? kv = ParseBodyAsLua(args, out message);
                        if (!kv.HasValue)
                            return message;

                        string name = kv.Value.Key;
                        string body = kv.Value.Value;

                        try
                        {
                            string toAppend = ".lua";
                            if (name.Contains(".lua"))
                                toAppend = "";

                            string path = Path.Combine(scalesPath, name + toAppend);
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
                                for(int i = scales.Count - 1; i >= 0; --i)
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

                                return "I deleted the scale at `" + path + "`!\nCustom scales left: `" + scales.Count + "`";
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


                // Lists all registered scales that the snek has
                case "list":
                    {
                        string comp = "";

                        if (scales.Count > 0)
                        {
                            comp += "Found `" + scales.Count + (scales.Count > 1 ? "` scales" : "` scale") + ": ```diff";
                            foreach (Scale s in scales)
                            {
                                comp += "\n+ " + s.Name;
                            }
                            comp += "\n```";
                            comp += "You can add and remove scales with `" + indicator + "scale add <name> <content>` and `" + indicator + "scale remove <name>`";
                        }
                        else
                        {
                            return "I only have my default scales! Try adding some with `" + indicator + "scale add <name> <content>`!";
                        }

                        return comp;
                    }

                case "shutdown":
                    {
                        System.Environment.Exit(0);
                        return "Putting scales away and shutting down";
                    }

                // Let people know how to interact with snek
                default:
                    {
                        string commandList = "";
                        commandList += $"- Get detialed examples with `{indicator}scale help`\n";
                        commandList += $"- To look at my scales, try `{indicator}scale list`\n";
                        commandList += $"- To add a scale, try `{indicator}scale add <scale name> <lua>`\n";
                        commandList += $"- To remove a scale, try `{indicator}scale add <scale name>`\n";
                        commandList += $"- To update a scale, try `{indicator}scale update <scale name> <lua>`\n";
                        commandList += $"- To view a scale, try `{indicator}scale type <scale name>`\n";
                        commandList += $"- You can stop me with `{indicator}scale shutdown`\n";

                        return commandList;
                    }
            }
        }

        // Returns name and body of a passed function parsed out as lua, with an associated (preceeding) filename.
        // If null is returned, something failed and there's a message pending.
        private KeyValuePair<string, string>? ParseBodyAsLua(string args, out string message)
        {
            message = "";
            int start = args.IndexOf("`");
            string name = args.Substring(0, start).Trim().ToLowerInvariant();
            string body = args.Substring(start).Trim();
            
            // If the user was using syntax highlighting in the markdown, ignore that lua keyword.
            // Also, ignore any cruft at the beginning.
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

            // Figure out how many trailing characters to ignore.
            int offsetBack = 0;
            foreach (char c in body.Reverse())
            {
                if (c != '`' && !string.IsNullOrWhiteSpace("" + c))
                    break;

                ++offsetBack;
            }

            // Apply these discovered offsets to the body
            body = body.Substring(offsetForward, body.Length - (offsetBack + offsetForward));

            // Attempt to compile the script in its own sandbox.
            Script envTemp = new Script();
            try
            {
                envTemp.DoString(body);
            }
            catch (Exception e)
            {
                message = "Whoops, looks like there was an error in that code and I can't use that - it needs to at least compile!\n`" + e.Message + "`";
                return null;
            }

            return new KeyValuePair<string, string>(name, body);
        }
    }
}
