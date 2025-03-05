using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EmpyrionPlaytimeRewardsShop
{
    /// <summary>
    /// Empyrion Playtime Reward Shop using Empyrion Mod Base (Simple Mod)
    /// </summary>
    public class PlaytimeRewardsShop : EmpyrionModBase
    {
        /// <summary>
        /// keep the reference on the game api
        /// </summary>
        public ModGameAPI DediAPI { get; private set; }

        /// <summary>
        /// weiß noch nicht genau was das ist... doku folgt
        /// </summary>
        public ConfigurationManager<PlaytimeRewardsShopConfiguration> Configuration { get; set; }

        /// <summary>
        /// Starting point when the mod is loaded
        /// </summary>
        /// <param name="dediAPI">reference on the game api</param>
        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI = dediAPI;
            LogLevel = LogLevel.Message;

            Log($"**PlaytimeRewardsShop: loaded");

            LoadConfiuration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.ChatCommandPrefix;

            ChatCommands.Add(new ChatCommand($"playtime help", (I, A) => DisplayHelp(I.playerId), $"help and commands for the playtime shop"));
            ChatCommands.Add(new ChatCommand($"points", (I, A) => ShowPoints(I, A), $"show the player's points"));
            ChatCommands.Add(new ChatCommand($"buy Neo", (I, A) => BuyOre(I, A), $"buy 100 Neodynium ore"));

            Event_Player_Connected += PlaytimeRewardsShop_Event_Player_Connected;
            Event_Player_Disconnected += PlaytimeRewardsShop_Event_Player_Disconnected;
        }

        private void PlaytimeRewardsShop_Event_Player_Connected(Id obj)
        {
            try
            {
                Task task = updateLoginTimestamp(obj);
            }
            catch (Exception)
            {

            }
        }

        private void PlaytimeRewardsShop_Event_Player_Disconnected(Id obj)
        {
            try
            {
                Task task = updatePoints(obj);
            }
            catch (Exception)
            {

            }
        }

        private async Task updateLoginTimestamp(Id obj)
        {
            var P = await Request_Player_Info(obj);

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            // update the current timestamp
            currentPlayerData.Current.loginTimestamp = DateTime.Now.Ticks;
            currentPlayerData.Save();
        }

        private async Task updatePoints(Id obj)
        {
            var P = await Request_Player_Info(obj);

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            // calculate the time difference
            long tickDifference = DateTime.Now.Ticks - currentPlayerData.Current.loginTimestamp;

            // update the points
            TimeSpan timeDiff = TimeSpan.FromTicks(tickDifference);
            currentPlayerData.Current.Points = Convert.ToInt32(timeDiff.TotalSeconds * 1000.0 / 300.0); // 1000 points per 5 minutes

            // update the timestamp
            currentPlayerData.Current.loginTimestamp = DateTime.Now.Ticks;
            currentPlayerData.Save();
        }

        private void LoadConfiuration()
        {
            Configuration = new ConfigurationManager<PlaytimeRewardsShopConfiguration>
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"Configuration.json")
            };

            Configuration.Load();
            Configuration.Save();
        }

        private async Task DisplayHelp(int playerId)
        {
            await DisplayHelp(playerId,
                $"Every {Configuration.Current.RewardPeriodInMinutes} minutes you get {Configuration.Current.RewardPointsPerPeriod} points\n" +
                "You can buy these items: \n" +
                "100 Neomydium Ore for 1000 points.\n");
        }

        private async Task BuyOre(ChatInfo info, Dictionary<string, string> args)
        {
            var P = await Request_Player_Info(info.playerId.ToId());

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            // 1. check if the player has enough points
            if (currentPlayerData.Current.Points < 100)
            {
                MessagePlayer(info.playerId, $"you only have {currentPlayerData.Current.Points} points.\nNot enough to buy the ore for 100 points.\nPlay more <3");
                return;
            }

            // 2. add 100 Neodynium Ore to the players inventory
            var giveReward = new ItemExchangeInfo()
            {
                buttonText = "close",
                desc = "Transfer the items into your inventory",
                id = info.playerId,
                items = (new ItemStack[] { new ItemStack(4300, 100)}),//.Concat(new ItemStack[7 * 7]).Take(7 * 7).ToArray(),
                title = $"Playtime Shop"
            };
            try
            {
                await Request_Player_ItemExchange(Timeouts.NoResponse, giveReward);
            }
            catch (Exception error)
            {
                Log($"transfer items failed for player {info.playerId} :{error}", LogLevel.Error);
                MessagePlayer(info.playerId, $"transfer items failed {error}");
            }
            

            // 3. remove the points from the player data
            currentPlayerData.Current.Points -= 100;
            currentPlayerData.Save();
        }

        private async Task ShowPoints(ChatInfo info, Dictionary<string, string> args)
        {
            var P = await Request_Player_Info(info.playerId.ToId());

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            MessagePlayer(info.playerId, $"you have {currentPlayerData.Current.Points} points.\nTo update the points logout.");
        }
    }
}
