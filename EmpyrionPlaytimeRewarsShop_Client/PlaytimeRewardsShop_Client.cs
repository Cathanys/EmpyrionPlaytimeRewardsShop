using Eleon;
using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmpyrionPlaytimeRewarsShop_Client
{
    // Class implementing the new IMod interface as the legacy modding is not available on client side
    public class PlaytimeRewardsShop_Client : IMod, ModInterface, IDisposable
    {
        /// <summary>
        /// reference to the mod api 2
        /// </summary>
        internal static IModApi modApi;

        /// <summary>
        /// increase the number with each request
        /// </summary>
        private ushort requestNr = 0;

        /// <summary>
        /// string to the folder with the configurations
        /// </summary>
        private string saveGameModPath = string.Empty;

        /// <summary>
        /// shop offers, prices and points progress
        /// </summary>
        private PlaytimeRewardsShopConfiguration configuration = null;

        /// <summary>
        /// player points progress
        /// </summary>
        private PlayerData playerData = null;

        /// <summary>
        /// name of the config file
        /// </summary>
        private const string configFileName = "Configuration.json";

        /// <summary>
        /// name of the player data file
        /// </summary>
        private const string playerDataFileName = "playerData.json";

        // ----- IMod methods -------------------------------------------------

        /// <summary>
        /// Called once early when the host process starts - treat this like a constructor for your mod
        /// </summary>
        /// <param name="modAPI">mod api</param>
        public void Init(IModApi modAPI)
        {
            modApi = modAPI;

            modApi.Log("PlaytimeRewardsShop initialized");

            // get informed if game has been entered or left
            modApi.Application.GameEntered += OnGameEntered;

            // get informed if a player has sent a chat message
            modApi.Application.ChatMessageSent += OnChatMessageSent;

            // read the path to the configuration and player data files
            saveGameModPath = modApi.Application.GetPathFor(AppFolder.SaveGame) + @"\Mods\" + this.GetType().Name + @"\";
            modApi.Log($"PlaytimeRewardsShop Configuration Path: {saveGameModPath}");

            try
            {
                // check if the path exists
                if (!Directory.Exists(saveGameModPath))
                {
                    DirectoryInfo dirInfo = Directory.CreateDirectory(saveGameModPath);
                }

                // load the mod configuration
                readConfiguration();

                // load the player data
                readPlayerData();
            }
            catch (Exception error)
            {
                modApi.Log($"Inizialization failed :{error}");
            }
        }

        // Called once just before the game is shut down
        // You may use this like a Dispose method for your mod to release unmanaged resources
        public void Shutdown()
        {
            modApi.Application.GameEntered -= OnGameEntered;
            modApi.Application.ChatMessageSent -= OnChatMessageSent;

            updatePointsAndTimestamp();

            modApi.Log("PlaytimeRewardsShop mod shutdown");
        }

        private void readConfiguration()
        {
            string configFilePath = Path.Combine(saveGameModPath, configFileName);
            if (!File.Exists(configFilePath))
            {
                // create a new configuration
                this.configuration = new PlaytimeRewardsShopConfiguration();

                // add a few standard items
                ShopItem newItem = new ShopItem()
                {
                    Name = "neo",
                    Description = "Neodynium Ore",
                    price = 100,
                    quantity = 100,
                    itemId = 4300
                };
                this.configuration.RewardItems.Add(newItem);

                ShopStat newStat = new ShopStat()
                {
                    Name = "life",
                    Description = "Health",
                    price = 100,
                    quantity = 100,
                    maxStat = 2000
                };
                this.configuration.RewardStats.Add(newStat);

                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(this.configuration));
            }
            else
            {
                using (StreamReader configFile = File.OpenText(configFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    this.configuration = (PlaytimeRewardsShopConfiguration)serializer.Deserialize(configFile, typeof(PlaytimeRewardsShopConfiguration));
                }
            }
        }

        private void readPlayerData()
        {
            string playerDataFilePath = Path.Combine(saveGameModPath, playerDataFileName);

            if (!File.Exists(playerDataFilePath))
            {
                playerData = new PlayerData()
                {
                    Points = 0,
                    loginTimestamp = DateTime.Now
                };
            }
            else
            {
                using (StreamReader playerDataFile = File.OpenText(playerDataFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    this.playerData = (PlayerData)serializer.Deserialize(playerDataFile, typeof(PlayerData));
                }
            }
        }

        void OnGameEntered(bool entered)
        {
            updateLoginTimestamp();
        }

        void OnChatMessageSent(MessageData data)
        {
            modApi.Log($"Chat msg to {data.Channel}: {data.Text}");

            // if player wrote a 'echo' message to the server then we respond
            if (data.Channel == Eleon.MsgChannel.Server || data.Channel == Eleon.MsgChannel.Faction)
            {
                if(data.Channel == Eleon.MsgChannel.Server && data.Text == "echo")
                {
                    AnswerAsync();
                    modApi.Log("Triggered answer...");
                }
                else
                {
                    // if player wrote a command in the faction or server, we react
                    parseCommand(data);
                }

            }
        }

        async void AnswerAsync()
        {
            await DelayAsync();

            // Note: Here we are again in the main thread - which is required for communication with the game

            var chatMsg = new MessageData()
            {
                SenderType = Eleon.SenderType.ServerInfo,
                Channel = Eleon.MsgChannel.Server,
                Text = "This is the echo from the Mod"
            };

            modApi.Application.SendChatMessage(chatMsg);
        }

        Task DelayAsync()
        {
            return Task.Factory.StartNew(() => Thread.Sleep(1000)); // sleep in another thread
        }

        // Optional method to be executed via the "mod" console command
        public void ExecCommand(List<string> args)
        {
            if (args != null && args.Count > 0)
            {
                modApi.GUI.ShowGameMessage($"Demo mod console cmd execution: first arg = {args[0]}", prio: 1);
            }
            else
            {
                modApi.GUI.ShowGameMessage("Demo mod console cmd execution: no arguments", prio: 1);
            }
        }

        private void updateLoginTimestamp()
        {
            this.playerData.loginTimestamp = DateTime.Now;
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter file = File.CreateText(Path.Combine(saveGameModPath, playerDataFileName)))
            {
                serializer.Serialize(file, this.playerData);
            }
        }

        private void updatePointsAndTimestamp()
        {
            // calculate the points
            TimeSpan timeDiff = DateTime.Now - this.playerData.loginTimestamp;
            //timeDiff = timeDiff.Add(new TimeSpan(0,25,0));
            if (timeDiff.TotalSeconds > this.configuration.RewardPointsPerPeriod / (this.configuration.RewardPeriodInMinutes * 60.0))
            {
                this.playerData.Points += Convert.ToInt32(timeDiff.TotalSeconds * this.configuration.RewardPointsPerPeriod / (this.configuration.RewardPeriodInMinutes * 60.0));

                // update the timestamp
                this.playerData.loginTimestamp = DateTime.Now;
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new JavaScriptDateTimeConverter());
                serializer.NullValueHandling = NullValueHandling.Ignore;

                using (StreamWriter sw = new StreamWriter(Path.Combine(saveGameModPath, playerDataFileName)))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, this.playerData);
                }
            }
        }

        private void parseCommand(MessageData chatInfo)
        {
            if (modApi.GUI == null || !modApi.GUI.IsWorldVisible)
                return;

            string commandText = chatInfo.Text;

            // only commands with "\prs" will be accepted
            if (!commandText.StartsWith(@"\prs"))
                return;

            // after the command prefix, minimum one blank has to follow
            string[] commandSplit = commandText.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (commandSplit.Length < 2)
            {
                modApi.Log("Command too short");
                return;
            }

            if (commandSplit[1].StartsWith("help"))
            {
                // print the available commands
                string helpText = $"Every {this.configuration.RewardPeriodInMinutes} minutes you win {this.configuration.RewardPointsPerPeriod} points\n";
                helpText += "You can buy:\n";

                foreach(ShopItem item in this.configuration.RewardItems)
                {
                    helpText += $"{item.quantity} {item.Description} for {item.price} points\n";
                }
                foreach (ShopStat item in this.configuration.RewardStats)
                {
                    helpText += $"{item.quantity} {item.Description} for {item.price} points\n";
                }

                DialogConfig dialogConfig = new DialogConfig();
                dialogConfig.ButtonTexts = new string[] { "close" };
                dialogConfig.TitleText = "Playtime Shop";
                dialogConfig.BodyText = helpText;

                modApi.GUI.ShowDialog(dialogConfig, null, 0);
            }
            else if (commandSplit[1].ToLower().StartsWith("points"))
            {
                updatePointsAndTimestamp();

                modApi.GUI.ShowGameMessage($"You have {this.playerData.Points} points", prio: 1);
            }
            else if (commandSplit[1].ToLower().StartsWith("buy"))
            {
                if (commandSplit.Length < 3)
                    return;

                updatePointsAndTimestamp();

                foreach (ShopItem item in this.configuration.RewardItems)
                {
                    if (commandSplit[2].ToLower().StartsWith(item.Name))
                    {
                        // check if the player has enough points
                        if(this.playerData.Points < item.price)
                            modApi.GUI.ShowGameMessage($"You don't have enough {this.playerData.Points} points to buy the item for {item.price} points", prio: 1);
                        else
                        {
                            buyItem(chatInfo, item);
                            return; // item found in the list and transferred
                        }
                            
                    }
                }

                // if it is not an item, maybe it is a stat
                foreach (ShopStat stat in this.configuration.RewardStats)
                {
                    if (commandSplit[2].ToLower().StartsWith(stat.Name))
                    {
                        // check if the player has enough points
                        if (this.playerData.Points < stat.price)
                            modApi.GUI.ShowGameMessage($"You don't have enough {this.playerData.Points} points to buy the item for {stat.price} points", prio: 1);
                        else
                        {
                            buyStat(chatInfo, stat);
                            return; // stat found in the list and transferred
                        }

                    }
                }
            }
        }

        private void buyItem(MessageData chatInfo, ShopItem item)
        {
            try
            {
                modApi.Log($"Try to transfer item {item.Name} to player {chatInfo.SenderEntityId} or {modApi.Application.LocalPlayer.Id}");

                ItemExchangeInfo giveReward = new ItemExchangeInfo()
                {
                    buttonText = "close",
                    desc = "Transfer the items into your inventory",
                    id = chatInfo.SenderEntityId,
                    items = (new ItemStack[] { new ItemStack(item.itemId, item.quantity) }),//.Concat(new ItemStack[7 * 7]).Take(7 * 7).ToArray(),
                    title = "Playtime Shop"
                };

                if (null != DediLegacyMod)
                {
                    modApi.Log($"Try to transfer item over legacy mod");

                    _ = buyItemLegacy(chatInfo.SenderEntityId, giveReward);

                    // remove the points
                    this.playerData.Points -= item.price;                    
                }
                else if (null != gameApi)
                {
                    if (gameApi.Game_Request(CmdId.Request_Player_ItemExchange, requestNr, giveReward))
                    {
                        modApi.Log($"Item successfully transferred");

                        // remove the points
                        this.playerData.Points -= item.price;
                    }
                    else
                    {
                        modApi.Log($"Gameapi request failed");
                    }
                    requestNr++;
                }
                else
                {
                    modApi.Log($"Gameapi is not initialized");
                }
            }
            catch (Exception error)
            {
                modApi.Log($"Buy item failed :{error}");
            }
        }

        private async Task buyItemLegacy(int playerID, ItemExchangeInfo giveReward)
        {
            var P = await DediLegacyMod?.Request_Player_Info(new Id(playerID));

            try
            {
                await DediLegacyMod?.Request_Player_ItemExchange(Timeouts.NoResponse, giveReward);
            }
            catch (Exception error)
            {
                modApi.Log($"transfer items failed for player {playerID} :{error}");
                modApi.GUI.ShowGameMessage($"Item transfer failed {error}", prio: 1);
            }
        }

        private void buyStat(MessageData chatInfo, ShopStat stat)
        {
            try
            {
                modApi.Log($"Try to add stat {stat.Name} to player {chatInfo.SenderEntityId} or {modApi.Application.LocalPlayer.Id}");

                if (stat.Name.StartsWith("life"))
                {
                    // check if the max stat is already reached
                    if (modApi.Application.LocalPlayer.HealthMax + stat.quantity > stat.maxStat)
                        modApi.GUI.ShowGameMessage($"Maximum {stat.maxStat} {stat.Description} are allowed", prio: 1);
                }                
            }
            catch (Exception error)
            {
                modApi.Log($"Buy stat failed :{error}");
            }
        }

        // ----- ModInterface methods -----------------------------------------
        /// <summary>
        /// reference to the legacy mod api 1
        /// </summary>
        internal static ModGameAPI gameApi;

        public class DediLegacyModBase : EmpyrionModBase
        {
            public override void Initialize(ModGameAPI dediAPI) { }
        }

        public DediLegacyModBase DediLegacyMod { get; set; }

        /// <summary>
        /// Called once early when the host process starts - treat this like a constructor for your mod
        /// </summary>
        /// <param name="legacyModApi"></param>
        public void Game_Start(ModGameAPI legacyModApi)
        {
            gameApi = legacyModApi;
            gameApi?.Console_Write("PlaytimeRewardShop Mod started: Game_Start");

            DediLegacyMod = new DediLegacyModBase();
            DediLegacyMod?.Game_Start(gameApi);
        }

        public void Game_Update()
        {
            DediLegacyMod?.Game_Update();
        }

        public void Game_Exit()
        {
            modApi?.Log("PlaytimeRewardShop Mod exited:Game_Exit");

            DediLegacyMod?.Game_Exit();

            try
            {
                Shutdown();
            }
            catch (Exception error) { modApi?.Log($"Game_Exit: detach events: {error}"); }

            modApi?.Log("Mod exited:Game_Exit finished");
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            modApi?.Log($"EmpyrionScripting Mod: Game_Event {eventId} {seqNr} {data}");
            DediLegacyMod?.Game_Event(eventId, seqNr, data);
        }


        // ----- IDispose Interface methods -----------------------------------------
        public void Dispose()
        {
            modApi?.Log("EmpyrionScripting Mod: Dispose");
        }
    }
}
