using System;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Snek.Utils;
using System.Collections.Generic;

namespace Snek
{
    public interface ISnek
    {
        string Name { get; }
    }

    public class SnekBasket
    {
        public static List<ISnek> Basket = null;

        static void Main(string[] args)
        {
            Basket = new List<ISnek>();

            GarterSnek snek = new GarterSnek(configPath: "./garter.snek");
            snek.Slither().GetAwaiter().GetResult();
            Basket.Add(snek);
        }
    }

    public class GarterSnek : ISnek
    {
        private DiscordSocketClient client;

        public string Name { get; private set; }

        private string token;
        private string indicator;

        public GarterSnek(string configPath)
        {
            PseudoINI garterConfig = new PseudoINI(configPath);
            this.Name = garterConfig.ReadItem("name");
            this.token = garterConfig.ReadItem("token");
            this.indicator = garterConfig.ReadItem("indicator");
        }

        // Snakes can't run, but they can slither
        public async Task Slither()
        {
            client = new DiscordSocketClient();
            client.MessageReceived += Client_MessageReceived;

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            
            await Task.Delay(-1);
        }

        private Task Client_MessageReceived(SocketMessage message)
        {
            throw new NotImplementedException();
        }

        private async Task MessageReceivedAsync(SocketMessage raw)
        {
            // Ignore self
            if (raw.Author.Id == client.CurrentUser.Id)
                return;

            string message = raw.Content.Trim();
            if(message != null && message.Length > 0)
            {
                if(message.StartsWith(indicator))
                {
                    await raw.Channel.SendMessageAsync("*hiss*");
                }
            }
        }
    }
}
