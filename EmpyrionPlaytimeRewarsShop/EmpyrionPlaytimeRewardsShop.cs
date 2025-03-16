using Eleon;
using Eleon.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace EmpyrionPlaytimeRewardsShop
{
    // Class implementing the new IMod interface as the legacy modding is not available on client side
    public class EmpyrionPlaytimeRewardsShop : IMod, ModInterface
    {
        /// <summary>
        /// reference to the mod api 2
        /// </summary>
        internal static IModApi modApi;

        /// <summary>
        /// increase the number with each request, starting with non zero
        /// will be incremented before use
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
        /// template name of the player data file. Use the {0} as placeholder for the player ID
        /// </summary>
        private string playerDataFileName = "PlayerData_{0}.json";

        /// <summary>
        /// store the requested command in this list to identify the corresponding event
        /// </summary>
        private List<EventItem> commandList = new List<EventItem> ();

        // ----- IMod methods -------------------------------------------------

        /// <summary>
        /// Called once early when the host process starts - treat this like a constructor for your mod
        /// </summary>
        /// <param name="modAPI">mod api</param>
        public void Init(IModApi modAPI)
        {
            modApi = modAPI;

            modApi.Log("PlaytimeRewardsShop initialized");

            // get informed if game has been entered or left ( only single player)
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
            if( modApi != null )
            {
                modApi.Application.GameEntered -= OnGameEntered;
                modApi.Application.ChatMessageSent -= OnChatMessageSent;

                if (ApplicationMode.SinglePlayer == modApi.Application.Mode)
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
                    Description = "Maximum Health",
                    price = 100,
                    quantity = 100,
                    maxStat = 2000
                };
                this.configuration.RewardStats.Add(newStat);

                ShopStat newStat2 = new ShopStat()
                {
                    Name = "exp",
                    Description = "Experience",
                    price = 100,
                    quantity = 1000,
                    maxStat = 500000
                };
                this.configuration.RewardStats.Add(newStat2);

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

            modApi.Log($"playerDataFilePath {playerDataFilePath} for player {playerID}");

            if (!File.Exists(playerDataFilePath))
            {
                playerData = new PlayerData(); // 0 points and current date time
                savePlayerData(playerID, playerData);
            }
            else
            {
                using (StreamReader playerDataFile = File.OpenText(playerDataFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;

                    try
                    {
                        playerData = (PlayerData)serializer.Deserialize(playerDataFile, typeof(PlayerData));
                    }
                    catch (Exception error)
                    {
                        modApi.Log($"Deserialize Player Data returned error {error}");
                    }
                    finally 
                    {
                        // if file does not match the pattern -> create a new file
                        if (playerData == null)
                        {
                            playerData = new PlayerData(); // 0 points and current date time
                            savePlayerData(playerID, playerData);
                        }
                    }
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
            modApi.Log($"OnGameEntered");

            // only called in single player
            if (ApplicationMode.SinglePlayer == modApi.Application.Mode)
                updateLoginTimestamp(modApi.Application.LocalPlayer.Id);
        }

        void OnChatMessageSent(MessageData data)
        {
            modApi.Log($"Chat msg to {data.Channel}: {data.Text}");

            // if player wrote a 'echo' message to the server then we respond
            if (data.Channel == Eleon.MsgChannel.Server || data.Channel == Eleon.MsgChannel.Faction)
            {
                 // if player wrote a command in the faction or server, we react
                 parseCommand(data);                
            }
        }

        private void updateLoginTimestamp(int playerID)
        {
            modApi.Log($"updateLoginTimestamp {playerID}");

            // read or create the player data
            PlayerData playerData = readPlayerData(playerID);

            playerData.loginTimestamp = DateTime.UtcNow;

            savePlayerData(playerID, playerData);
        }

        private int updatePointsAndTimestamp(int playerID)
        {
            // read or create the player data
            PlayerData playerData = readPlayerData(playerID);

            // calculate the points
            TimeSpan timeDiff = DateTime.UtcNow - playerData.loginTimestamp;

            if (timeDiff.TotalMinutes > this.configuration.RewardPeriodInMinutes)
            {
                int addPoints = Convert.ToInt32((timeDiff.TotalMinutes * (double)this.configuration.RewardPointsPerPeriod) / (double)this.configuration.RewardPeriodInMinutes);
                playerData.Points += addPoints;

                modApi.Log($"{addPoints} points added to player {playerID} with {timeDiff.TotalMinutes} minutes new playtime");

                // update the timestamp
                playerData.loginTimestamp = DateTime.UtcNow;

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
                string helpText = $"Every {this.configuration.RewardPeriodInMinutes} minutes you win {this.configuration.RewardPointsPerPeriod} points\n\n\n";
                helpText += "Commands for the faction or server chat:\n\n";

                foreach(ShopItem item in this.configuration.RewardItems)
                {
                    helpText += $"{this.configuration.ChatCommandPrefix.Remove(0,1)} buy {item.Name}\t\t{item.quantity} {item.Description} for {item.price} points\n";
                }
                foreach (ShopStat item in this.configuration.RewardStats)
                {
                    helpText += $"{this.configuration.ChatCommandPrefix.Remove(0,1)} buy {item.Name}\t\t{item.quantity} {item.Description} for {item.price} points\n";
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
                if (null != gameApi)
                {
                    var outMsg = new IdMsgPrio()
                    {
                        id = playerID,
                        msg = message,
                        prio = 1
                    };
                    gameApi.Game_Request(CmdId.Request_InGameMessage_SinglePlayer, ++requestNr, outMsg);
                }
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
                
                if (null != gameApi)
                {
                    modApi.Log($"Try to transfer item over game api");

                    try
                    {

                        if (gameApi.Game_Request(CmdId.Request_Player_ItemExchange, ++requestNr, giveReward))
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
                    catch (Exception error)
                    {
                        modApi.Log($"Gameapi request failed with error {error}");
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


        private void buyStat(int playerID, ShopStat stat)
        {
            try
            {
                modApi.Log($"Try to add stat {stat.quantity} {stat.Name} to player {playerID}");

                if (null != gameApi)
                {
                    modApi.Log($"Try to get player info over game api");

                    if (gameApi.Game_Request(CmdId.Request_Player_Info, ++requestNr, new Id(playerID)))
                    {
                        // we are done here and wait for the response in Game_Event
                        EventItem eventItem = new EventItem()
                        {
                            Nr          = requestNr,
                            PlayerId    = playerID,
                            CommandID   = CmdId.Request_Player_Info,
                            Stat        = stat
                        };

                        commandList.Add( eventItem );
                    }
                    else
                        modApi.Log($"Getting the player info did not work for player {playerID}");
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
        ModGameAPI gameApi = null;

        /// <summary>
        /// Called once early when the host process starts - treat this like a constructor for your mod
        /// </summary>
        /// <param name="legacyModApi"></param>
        public void Game_Start(ModGameAPI legacyModApi)
        {
            gameApi = legacyModApi;
            gameApi?.Console_Write("PlaytimeRewardShop legacy mod started: Game_Start");
        }

        public void Game_Update()
        {

        }

        public void Game_Exit()
        {
            modApi?.Log("PlaytimeRewardShop Mod exited:Game_Exit");

            try
            {
                Shutdown();
            }
            catch (Exception error) { modApi?.Log($"Game_Exit: detach events: {error}"); }
        }

        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            modApi?.Log($"PlaytimeRewardShop Mod: Game_Event {eventId} {seqNr} {data}");

            switch (eventId)
            {
                case CmdId.Event_Player_Connected:
                    if (data is Id)
                    {
                        Id playerID  = data as Id;
                        if (playerID != null)
                            updateLoginTimestamp(playerID.id);
                    }
                    break;
                case CmdId.Event_Player_Disconnected:
                    if (data is Id)
                    {
                        Id playerID = data as Id;
                        if (playerID != null)
                            updatePointsAndTimestamp(playerID.id);
                    }
                    break;

                case CmdId.Event_Player_Info:
                    if(data is PlayerInfo)
                    {
                        PlayerInfo playerInfo = data as PlayerInfo;

                        //search for our event
                        foreach (EventItem item in commandList)
                        {
                            if( item.PlayerId == playerInfo.entityId &&
                                item.Nr == seqNr)
                            {
                                updateStatLegacy(item.PlayerId, playerInfo, item.Stat);

                                // remove the event from the list
                                commandList.Remove(item);
                                break;
                            }
                        }

                        modApi.Log($"Command List entries:{commandList.Count}");
                    }
                    break;
            }
        }

        private void updateStatLegacy(int playerID, PlayerInfo playerInfo, ShopStat stat)
        {
            int newPlayerStat = 0;

            modApi.Log($"Try to increase stat with game api");

            PlayerInfoSet playerInfoSet = new PlayerInfoSet()
            {
                entityId = playerID
            };

            if (stat.Name.StartsWith("life"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.healthMax), ref newPlayerStat))
                    return;

                // We are aware that the API provides the maximum health that can currently be increased through food.
                // How can we tell if maximum health has been temporarily increased by buffs/food?
                playerInfoSet.healthMax = newPlayerStat;
            }
            else if (stat.Name.StartsWith("exp"))
            {
                if (!checkAndIncreaseStat(playerID, stat, playerInfo.exp, ref newPlayerStat))
                    return;

                playerInfoSet.experiencePoints = newPlayerStat;
            }
            else if (stat.Name.StartsWith("food"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.foodMax), ref newPlayerStat))
                    return;

                playerInfoSet.foodMax = newPlayerStat;
            }
            else if (stat.Name.StartsWith("stamina"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.staminaMax), ref newPlayerStat))
                    return;

                playerInfoSet.staminaMax = newPlayerStat;
            }
            else if (stat.Name.StartsWith("oxy"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.oxygenMax), ref newPlayerStat))
                    return;

                playerInfoSet.oxygenMax = newPlayerStat;
            }
            else if (stat.Name.StartsWith("rad"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.radiationMax), ref newPlayerStat))
                    return;

                playerInfoSet.radiationMax = newPlayerStat;
            }
            else if (stat.Name.StartsWith("temp"))
            {
                if (!checkAndIncreaseStat(playerID, stat, Convert.ToInt32(playerInfo.bodyTempMax), ref newPlayerStat))
                    return;

                playerInfoSet.bodyTempMax = newPlayerStat;
            }

            modApi.Log($"Try to update the player stats with id {playerInfoSet.entityId}");

            if (gameApi.Game_Request(CmdId.Request_Player_SetPlayerInfo, ++requestNr, playerInfoSet))
            {
                // remove the player points
                PlayerData playerData = readPlayerData(playerID);
                playerData.Points -= stat.price;
                savePlayerData(playerID, playerData);
            }
            else
                modApi.Log($"Set player stat did not work for player {playerInfoSet.entityId}");
        }

        private bool checkAndIncreaseStat(int playerID, ShopStat stat, int playerCurrentStat, ref int playerSetStat)
        {
            // check if the maximum has been reached
            if (playerCurrentStat + stat.quantity > stat.maxStat)
            {
                modApi.Log($"{stat.Description} of player {playerID} already at max {stat.maxStat}");
                showIngameMessage(playerID, $"You already have maximum {stat.Description}");
                return false;
            }

            playerSetStat = playerCurrentStat + stat.quantity;

            return true;
        }
    }
}
