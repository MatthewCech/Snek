using Discord;
using Discord.WebSocket;
using Snek.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Snek.Sneks
{
    public class SnekGarter : ISnek
    {
        private DiscordSocketClient client;

        public string Name { get; private set; }

        private string token;
        private string indicator;

        public SnekGarter(string configPath)
        {
            PseudoINI garterConfig = new PseudoINI(configPath);
            this.Name = garterConfig.ReadItem("name");
            this.token = garterConfig.ReadItem("token");
            this.indicator = garterConfig.ReadItem("indicator");
        }

        // Bold of you to assume a snek can Run(), it has no legs! It *can* slither tho.
        public void Slither()
        {
            Coil().GetAwaiter().GetResult();
        }

        private async Task Coil()
        {
            client = new DiscordSocketClient();
            client.MessageReceived += Client_MessageReceived;

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await Task.Delay(-1);
        }

        // When a message is sent in the discord, this is called. 
        // May be happening multiple times at once.
        private async Task Client_MessageReceived(SocketMessage raw)
        {
            // The snek doesn't care what it's doing itself, that's ok. It's just doin' what it do.
            if (raw.Author.Id == client.CurrentUser.Id)
                return;

            string message = raw.Content.Trim();
            if (message != null && message.Length > 0)
            {
                if (message.StartsWith(indicator))
                {
                    await raw.Channel.SendMessageAsync("*hiss*");
                }
            }
        }
    }
}
