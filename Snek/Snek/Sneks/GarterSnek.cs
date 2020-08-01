using Discord;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using Snek.Utils;
using System;
using System.Collections.Generic;
using System.IO;
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
            for (int i = scales.Count; i > 0; i--)
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
                string command = message.Split(null)[0].Trim().ToLowerInvariant().Substring(1);
                string args = "";
                if (message.Length - command.Length > 0)
                    args = message.Substring(command.Length + 1).Trim();

                string res = null;

                // See if we're calling 'scale' to make a new command. If not, try and run another command.
                if (command.Equals(scaleKey))
                {
                    string subCommand = args.Split(null)[0].Trim().ToLowerInvariant();
                    string subArgs = null;
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
                case "new":
                    {
                        string howTo = "To add a new scale, you'll want to make sure you use the `" + indicator + "scale add <name>` command.\n";
                        howTo += "Here's some boilerplate for adding a new scale that you can copy and paste!\n";
                        howTo += "` " + indicator + "scale add DemoScale <code block below goes here>`\n";
                        howTo += "```";
                        howTo += "function plugin(msg)\n";
                        howTo += "\tif msg == boop then\n";
                        howTo += "\t\treturn \"hiss\"\n";
                        howTo += "\tend\n";
                        howTo += "end\n";
                        howTo += "```\n";


                        return howTo;
                    }
                    break;


                    // Adds scale by case insensitve name, with behavior as follows.
                case "add":
                    {
                        return args;
                    }
                    break;


                    // Removes a scale by case insensive name
                case "remove":
                    {
                        if(args != null && args.Length > 0)
                        {
                            string path = Path.Combine(scalesPath, args);
                            if (File.Exists(path))
                            {
                                File.Delete(path);
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
                            comp += "Found " + scales.Count + " Scales: ```diff";
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
