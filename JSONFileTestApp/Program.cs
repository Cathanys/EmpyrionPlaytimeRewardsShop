using EmpyrionPlaytimeRewarsShop_Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            PlaytimeRewardShop playtimeRewardShop = new PlaytimeRewardShop();

            playtimeRewardShop.updateLoginTimestamp(1002);

            playtimeRewardShop.updatePoints(1002);
        }
    }
}
