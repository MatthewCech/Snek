using Discord;
using Discord.WebSocket;
using Snek.Utils;
using Snek.Sneks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Snek
{
    public class SnekBasket
    {
        public static List<ISnek> Basket = null;

        static void Main(string[] args)
        {
            // Place snakes in a basket...
            Basket = new List<ISnek>();
            Basket.Add(new SnekGarter(configPath: "garter.snek"));

            // ...now let the snakes do snake things!
            foreach (ISnek s in Basket)
                new Thread(() => { s.Slither(); }).Start();

            // Haha, snakes!
            Console.WriteLine("Lookit them go!");
        }
    }
}
