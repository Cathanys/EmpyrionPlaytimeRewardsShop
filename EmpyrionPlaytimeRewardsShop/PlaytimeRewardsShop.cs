using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

            LoadConfiguration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.ChatCommandPrefix;

            ChatCommands.Add(new ChatCommand($"help", (I, A) => DisplayHelp(I.playerId), $"help and commands for the playtime shop"));
            ChatCommands.Add(new ChatCommand($"points", (I, A) => ShowPoints(I, A), $"update and show the player's points"));

            foreach (ShopItem item in this.Configuration.Current.RewardItems)
            {
                ChatCommands.Add(new ChatCommand($"buy {item.Name}", (I, A) => BuyItem(I, A, item), $"buy {item.quantity} {item.Description} for {item.price} points"));
            }            

            Event_Player_Connected += PlaytimeRewardsShop_Event_Player_Connected;
            Event_Player_Disconnected += PlaytimeRewardsShop_Event_Player_Disconnected;
        }

        private void PlaytimeRewardsShop_Event_Player_Connected(Id obj)
        {
            try
            {
                Task task = updateLoginTimestamp(obj);
            }
            catch(Exception error)
            {
                Log($"Update login timestamp failed :{error}", LogLevel.Error);
            }
        }

        private void PlaytimeRewardsShop_Event_Player_Disconnected(Id obj)
        {
            try
            {
                Task task = updatePointsAndTimestamp(obj);
            }
            catch (Exception error)
            {
                Log($"Update points failed :{error}", LogLevel.Error);
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
            currentPlayerData.Current.loginTimestamp = DateTime.Now;
            currentPlayerData.Save();
        }

        private async Task updatePointsAndTimestamp(Id obj)
        {
            var P = await Request_Player_Info(obj);

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            savePlayerPointsAndTimestamp(currentPlayerData);
        }

        private void LoadConfiguration()
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
            string helpString = $"Every {Configuration.Current.RewardPeriodInMinutes} minutes you get {Configuration.Current.RewardPointsPerPeriod} points\n";
            helpString += "You can buy these items: \n";
            foreach (ShopItem item in Configuration.Current.RewardItems)
            {
                helpString += $"{item.quantity} {item.Description} for {item.price} points.\n";
            }
            
            await DisplayHelp(playerId, helpString);
        }

        private async Task BuyItem(ChatInfo info, Dictionary<string, string> args, ShopItem shopItem)
        {
            var P = await Request_Player_Info(info.playerId.ToId());

            ConfigurationManager<PlayerData> currentPlayerData = new ConfigurationManager<PlayerData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(@"Personal\{0}.json", P.steamId))
            };
            currentPlayerData.Load();

            // 1. update the players points
            savePlayerPointsAndTimestamp(currentPlayerData);

            // 2. check if the player has enough points
            if (currentPlayerData.Current.Points < shopItem.price)
            {
                MessagePlayer(info.playerId, $"you only have {currentPlayerData.Current.Points} points.\nNot enough to buy the {shopItem.quantity} {shopItem.Description} for {shopItem.price} points.\nPlay more <3");
                return;
            }

            // 3. add the item to the players inventory
            var giveReward = new ItemExchangeInfo()
            {
                buttonText = "close",
                desc = "Transfer the items into your inventory",
                id = info.playerId,
                items = (new ItemStack[] { new ItemStack(shopItem.itemId, shopItem.quantity) }),//.Concat(new ItemStack[7 * 7]).Take(7 * 7).ToArray(),
                title = "Playtime Shop"
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

            // 4. remove the points from the player data and save
            currentPlayerData.Current.Points -= shopItem.price;
            currentPlayerData.Save();
        }

        private void savePlayerPointsAndTimestamp(ConfigurationManager<PlayerData> currentPlayerData)
        {
            // calculate the time difference
            TimeSpan timeDiff = DateTime.Now - currentPlayerData.Current.loginTimestamp;
            if (timeDiff.TotalSeconds > this.Configuration.Current.RewardPointsPerPeriod / (this.Configuration.Current.RewardPeriodInMinutes * 60.0))
            {
                // update the points
                currentPlayerData.Current.Points += Convert.ToInt32(timeDiff.TotalSeconds * this.Configuration.Current.RewardPointsPerPeriod / (this.Configuration.Current.RewardPeriodInMinutes * 60.0));

                // update the timestamp
                currentPlayerData.Current.loginTimestamp = DateTime.Now;
                currentPlayerData.Save();
            }
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
