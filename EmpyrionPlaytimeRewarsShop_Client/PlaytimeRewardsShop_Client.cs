﻿using Eleon;
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

namespace EmpyrionPlaytimeRewardsShop_Client
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
        /// name of the config file
        /// </summary>
        private const string configFileName = "Configuration.json";

        /// <summary>
        /// template name of the player data file
        /// </summary>
        private string playerDataFileName = $"PlayerData{0}.json";

        // ----- IMod methods -------------------------------------------------

        /// <summary>
        /// Called once early when the host process starts - treat this like a constructor for your mod
        /// </summary>
        /// <param name="modAPI">mod api</param>
        public void Init(IModApi modAPI)
        {
            modApi = modAPI;

            modApi.Log("PlaytimeRewardsShop initialized");

            // dedicated server
            if (modApi.Application.Mode == ApplicationMode.DedicatedServer)
            {
                try
                {
                    modApi.Network.RegisterReceiverForClientPackets(ClientPacketsCallback);
                }
                catch (Exception error)
                {
                    modApi.Log($"RegisterReceiverForClientPackets failed: {error}");
                }
            }
            // single player
            else
            {                
                // get informed if game has been entered or left
                modApi.Application.GameEntered += OnGameEntered;

                // get informed if a player has sent a chat message
                modApi.Application.ChatMessageSent += OnChatMessageSent;
            }

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
            }
            catch (Exception error)
            {
                modApi.Log($"Inizialization failed :{error}");
            }
        }

        private void ClientPacketsCallback(string sender, int playerEntityId, byte[] data)
        {
            modApi?.Log($"Client {sender} Packet received ID {playerEntityId}");
        }

        // Called once just before the game is shut down
        // You may use this like a Dispose method for your mod to release unmanaged resources
        public void Shutdown()
        {
            if( modApi != null )
            {
                modApi.Application.GameEntered -= OnGameEntered;
                modApi.Application.ChatMessageSent -= OnChatMessageSent;

                updatePointsAndTimestamp(modApi.Application.LocalPlayer.Id);

                modApi.Log("PlaytimeRewardsShop mod shutdown");

                modApi = null;
            }
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

        private PlayerData readPlayerData(int playerID)
        {
            PlayerData playerData = null;
            string playerDataFilePath = Path.Combine(saveGameModPath, string.Format(playerDataFileName, playerID));

            if (!File.Exists(playerDataFilePath))
            {
                playerData = new PlayerData(); // 0 points and current date time
            }
            else
            {
                using (StreamReader playerDataFile = File.OpenText(playerDataFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    playerData = (PlayerData)serializer.Deserialize(playerDataFile, typeof(PlayerData));

                    // if file does not match the pattern -> create a new file
                    playerData = new PlayerData(); // 0 points and current date time
                }
            }

            return playerData;
        }

        private void savePlayerData(int playerID, PlayerData playerData)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter file = File.CreateText(Path.Combine(saveGameModPath, string.Format(playerDataFileName, playerID))))
            {
                serializer.Serialize(file, playerData);
            }
        }

        void OnGameEntered(bool entered)
        {
            // only called in single player
            updateLoginTimestamp(modApi.Application.LocalPlayer.Id);
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
                showIngameMessage(modApi.Application.LocalPlayer.Id, $"Demo mod console cmd execution: first arg = {args[0]}");
            }
            else
            {
                showIngameMessage(modApi.Application.LocalPlayer.Id, "Demo mod console cmd execution: no arguments");
            }
        }

        private void updateLoginTimestamp(int playerID)
        {
            // read or create the player data
            PlayerData playerData = readPlayerData(playerID);

            playerData.loginTimestamp = DateTime.Now;

            savePlayerData(playerID, playerData);
        }

        private int updatePointsAndTimestamp(int playerID)
        {
            // read or create the player data
            PlayerData playerData = readPlayerData(playerID);

            // calculate the points
            TimeSpan timeDiff = DateTime.Now - playerData.loginTimestamp;

            //timeDiff = timeDiff.Add(new TimeSpan(0,25,0));
            if (timeDiff.TotalSeconds > this.configuration.RewardPointsPerPeriod / (this.configuration.RewardPeriodInMinutes * 60.0))
            {
                playerData.Points += Convert.ToInt32(timeDiff.TotalSeconds * this.configuration.RewardPointsPerPeriod / (this.configuration.RewardPeriodInMinutes * 60.0));

                // update the timestamp
                playerData.loginTimestamp = DateTime.Now;

                savePlayerData(playerID, playerData);
            }

            return playerData.Points;
        }

        private void parseCommand(MessageData chatInfo)
        {
            int playerID = chatInfo.SenderEntityId;

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

                if(!modApi.Application.ShowDialogBox(playerID, dialogConfig, null, 0) && 
                    modApi.Application.Mode == ApplicationMode.SinglePlayer)
                {
                    modApi.Log("Show client dialog box");
                    modApi.GUI.ShowDialog(dialogConfig, null, 0);
                }                                                      
            }
            else if (commandSplit[1].ToLower().StartsWith("points"))
            {
                int points = updatePointsAndTimestamp(playerID);

                showIngameMessage(playerID, $"You have {points} points");
            }
            else if (commandSplit[1].ToLower().StartsWith("buy"))
            {
                if (commandSplit.Length < 3)
                    return;

                int points = updatePointsAndTimestamp(playerID);

                foreach (ShopItem item in this.configuration.RewardItems)
                {
                    if (commandSplit[2].ToLower().StartsWith(item.Name))
                    {
                        // check if the player has enough points
                        if(points < item.price)
                        {
                            showIngameMessage(playerID, $"You don't have enough {points} points to buy the item for {item.price} points");
                        }
                        else
                        {
                            buyItem(playerID, item);
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
                        if (points < stat.price)
                        {
                            showIngameMessage(playerID, $"You don't have enough {points} points to buy the item for {stat.price} points");
                        }
                        else
                        {
                            buyStat(playerID, stat);
                            return; // stat found in the list and transferred
                        }
                    }
                }
            }
        }

        private void showIngameMessage(int playerID, string message)
        {
            if (message == null)
                return;

            if (modApi.Application.Mode == ApplicationMode.SinglePlayer)
                modApi.GUI.ShowGameMessage(message, prio: 1);
            else
            {
                // at the moment i don't know how to send the messages on dedicated servers
                DialogConfig dialogConfig = new DialogConfig();
                dialogConfig.ButtonTexts = new string[] { "close" };
                dialogConfig.TitleText = "Playtime Shop";
                dialogConfig.BodyText = message;

                modApi.Application.ShowDialogBox(playerID, dialogConfig, null, 0);
            }
        }

        private bool buyItem(int playerID, ShopItem item)
        {
            try
            {
                modApi.Log($"Try to transfer item {item.Name} to player {playerID}");

                ItemExchangeInfo giveReward = new ItemExchangeInfo()
                {
                    buttonText = "close",
                    desc = "Transfer the items into your inventory",
                    id = playerID,
                    items = (new ItemStack[] { new ItemStack(item.itemId, item.quantity) }),//.Concat(new ItemStack[7 * 7]).Take(7 * 7).ToArray(),
                    title = "Playtime Shop"
                };

                if (null != DediLegacyMod)
                {
                    modApi.Log($"Try to transfer item over legacy mod");

                    if(buyItemLegacy(playerID, giveReward))
                    {
                        // remove the player points
                        PlayerData playerData = readPlayerData(playerID);
                        playerData.Points -= item.price;
                        savePlayerData(playerID, playerData);

                        return true;
                    }                 
                }
                else if (null != gameApi)
                {
                    if (gameApi.Game_Request(CmdId.Request_Player_ItemExchange, requestNr++, giveReward))
                    {
                        modApi.Log($"Item successfully transferred");

                        // remove the player points
                        PlayerData playerData = readPlayerData(playerID);
                        playerData.Points -= item.price;
                        savePlayerData(playerID, playerData);

                        return true;
                    }
                    else
                    {
                        modApi.Log($"Gameapi request failed");
                    }
                }
                else
                {
                    modApi.Log($"Gameapi is not initialized");
                }
            }
            catch (Exception error)
            {
                modApi.Log($"Buy item failed :{error}");
                return false;
            }

            return false;
        }

        private bool buyItemLegacy(int playerID, ItemExchangeInfo giveReward)
        {
            try
            {
                DediLegacyMod?.Request_Player_ItemExchange(Timeouts.NoResponse, giveReward).GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                modApi.Log($"transfer items failed for player {playerID} :{error}");
                showIngameMessage(playerID, $"Item transfer failed {error}");

                return false;
            }

            return true;
        }

        private void buyStat(int playerID, ShopStat stat)
        {
            try
            {
                modApi.Log($"Try to add stat {stat.quantity} {stat.Name} to player {playerID}");

                if(null != DediLegacyMod)
                {
                    PlayerInfo P = DediLegacyMod?.Request_Player_Info(new Id(playerID)).GetAwaiter().GetResult();

                    if (P.entityId != playerID)
                    {
                        modApi.Log($"Player ID from request player info {P.entityId} is different from the chat info {playerID}");
                        return;
                    }

                    PlayerInfoSet playerInfoSet = new PlayerInfoSet()
                    {
                        entityId = P.entityId
                    };

                    if (stat.Name.StartsWith("life"))
                    {
                        // check if the max stat is already reached
                        if (P.healthMax + stat.quantity > stat.maxStat)
                        {
                            showIngameMessage(playerID, $"Maximum {stat.maxStat} {stat.Description} are allowed");
                            return;
                        }

                        // We are aware that the API provides the maximum health that can currently be increased through food.
                        // How can we tell if maximum health has been temporarily increased by buffs/food?
                        playerInfoSet.healthMax += stat.quantity;
                    }
                    else if (stat.Name.Contains("xp"))
                    {
                        // check if the max stat is already reached
                        if (P.exp + stat.quantity > stat.maxStat)
                        {
                            showIngameMessage(playerID, $"Maximum {stat.maxStat} {stat.Description} are allowed");
                            return;
                        }

                        playerInfoSet.experiencePoints += stat.quantity;
                    }

                    modApi.Log($"Try to update the player stats with id {playerInfoSet.entityId}");

                    DediLegacyMod?.Request_Player_SetPlayerInfo(playerInfoSet).GetAwaiter().GetResult();
                }
                else
                {
                    modApi.Log($"Gameapi is not initialized");
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
            DediLegacyMod?.Game_Start(legacyModApi);
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
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            modApi?.Log($"PlaytimeRewardShop Mod: Game_Event {eventId} {seqNr} {data}");
            DediLegacyMod?.Game_Event(eventId, seqNr, data);
        }

        // ----- IDispose Interface methods -----------------------------------------
        public void Dispose()
        {
            modApi?.Log("PlaytimeRewardShop Mod: Dispose");
        }
    }
}
