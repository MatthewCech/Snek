using System;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace Snek
{
    public class SnekLauncher
    {
        public static Snek Instance = null;
        static void Main(string[] args)
        {
            Instance = new Snek();
            Instance.Run().GetAwaiter().GetResult();
        }
    }

    public class Snek
    {
        private DiscordSocketClient client;
        public async Task Run()
        {
            client = new DiscordSocketClient();
            await client.LoginAsync(TokenType.Bot, "TOKEN HERE");
            await client.StartAsync();
            await Task.Delay(-1);
        }
    }
}
