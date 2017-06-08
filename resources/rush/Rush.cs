using System;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

namespace Rush
{
    class RushGamemode : Script
    {
        private static readonly Random random = new Random((int)DateTimeOffset.Now.ToUnixTimeSeconds());

        private readonly List<Vector3> spawns = new List<Vector3>()
        {
            new Vector3(1251.126f, 2730.8f, 38.49897f),
                new Vector3(1166.687f, 2610.64f, 40.21446f),
                new Vector3(1025.074f, 2656.377f, 38.93264f),
                new Vector3(970.7188f, 2726.338f, 38.86796f)
        };

        private readonly List<Vector3> objectives = new List<Vector3>()
        {
            new Vector3(1177.246f, 2712.256f, 37.59777f),
                new Vector3(1093.128f, 2627.242f, 37.63914f),
                new Vector3(984.7851f, 2647.099f, 39.56121f),
        };

        private readonly Dictionary<int, List<PedHash>> skins = new Dictionary<int, List<PedHash>>()
        {
            {
                0,
                new List<PedHash>()
                {
                    PedHash.Blackops01SMY,
                        PedHash.Blackops02SMY,
                        PedHash.Blackops03SMY,
                        PedHash.Marine01SMM,
                        PedHash.Marine01SMY,
                        PedHash.Marine03SMY,
                }
            },
            {
                1,
                new List<PedHash>()
                {
                    PedHash.Ashley,
                        PedHash.BikerChic,
                        PedHash.Clay,
                        PedHash.Lost01GMY,
                        PedHash.Lost02GMY,
                        PedHash.Lost03GMY,
                        PedHash.Terry,
                }
            },
        };

        private List<Client> attackers = new List<Client>() { };
        private List<Client> defenders = new List<Client>() { };

        private Dictionary<Client, DateTime> deadPlayers = new Dictionary<Client, DateTime>() { };
        private Dictionary<Client, DateTime> exitingPlayers = new Dictionary<Client, DateTime>() { };

        private int respawnTimer = 15;
        private int desertionTimer = 10;

        private int time = 0;

        private int phase = 0;

        private readonly Dictionary<string, BoxPosition> bounds = new Dictionary<string, BoxPosition>()
        {
            {
                "soft",
                new BoxPosition(new Vector3(915.9402f, 2585.65f, 60f), new Vector3(1295.73f, 2760.169f, 35f))
            },
            {
                "hard",
                new BoxPosition(new Vector3(874.257f, 2548.797f, 34f), new Vector3(1319.016f, 2779.176f, 61f))
            }
        };

        ColShape softBounds;
        ColShape hardBounds;

        public RushGamemode()
        {
            API.onResourceStart += OnStart;
            API.onPlayerFinishedDownload += OnDownload;
            API.onPlayerDeath += OnDeath;
            API.onUpdate += OnTick;
        }

        private void OnDownload(Client player)
        {
            SyncTime(player);
            PickTeam(player);
            SpawnPlayer(player);
            // SendUIData(player);
            player.triggerEvent("uiSetup", API.toJson(objectives));
        }

        private void OnStart()
        {
            BuildObjects();
            BuildBounds();
            SetTime();
            API.sendChatMessageToAll("~p~Rush started!~w~");
        }

        private void OnDeath(Client player, NetHandle entityKiller, int weapon)
        {
            lock (exitingPlayers) exitingPlayers.Remove(player);
            player.removeAllWeapons();
            player.triggerEvent("spawned", false);
            lock (deadPlayers) deadPlayers.Add(player, DateTime.Now);
            API.sendNativeToPlayer(player, Hash._RESET_LOCALPLAYER_STATE, player);
            API.sendNativeToPlayer(player, Hash.RESET_PLAYER_ARREST_STATE, player);

            API.sendNativeToPlayer(player, Hash.IGNORE_NEXT_RESTART, true);
            API.sendNativeToPlayer(player, Hash._DISABLE_AUTOMATIC_RESPAWN, true);

            API.sendNativeToPlayer(player, Hash.SET_FADE_IN_AFTER_DEATH_ARREST, true);
            API.sendNativeToPlayer(player, Hash.SET_FADE_OUT_AFTER_DEATH, false);
            API.sendNativeToPlayer(player, Hash.NETWORK_REQUEST_CONTROL_OF_ENTITY, player);

            API.sendNativeToPlayer(player, Hash.FREEZE_ENTITY_POSITION, player, false);
            API.sendNativeToPlayer(player, Hash.NETWORK_RESURRECT_LOCAL_PLAYER, player.position.X, player.position.Y, player.position.Z, player.rotation.Z, false, false);
            API.sendNativeToPlayer(player, Hash.RESURRECT_PED, player);
            player.invincible = true;
            player.freezePosition = true;
            player.health = 0;
        }

        private void OnTick()
        {
            ProcessDeaths();
            ProcessDeserters();
        }

        /// <summary>
        /// Sets the time and freezes it.
        /// </summary>
        private void SetTime()
        {
            time = random.Next(2400);
            int hours = time / 100 % 24;
            int minutes = time % 100 % 60;

            API.setTime(hours, minutes);
        }

        /// <summary>
        /// Sets the time for a specific player, freezes it.
        /// </summary>
        private void SyncTime(Client player)
        {
            int hours = time / 100 % 24;
            int minutes = time % 100 % 60;

            API.setTime(hours, minutes);
            API.freezePlayerTime(player, true);
        }

        /// <summary>
        /// Processes deaths, respawns a player if they died `respawnTimer` seconds ago
        /// </summary>
        private void ProcessDeaths()
        {
            foreach (var p in deadPlayers)
            {
                TimeSpan sinceSync = DateTime.Now.Subtract(p.Value);
                if (sinceSync.TotalSeconds >= respawnTimer)
                {
                    lock (deadPlayers) deadPlayers.Remove(p.Key);
                    SpawnPlayer(p.Key);
                }
            }
        }

        /// <summary>
        /// Rolls through deserters, kills them after `desertionTimer` seconds.
        /// </summary>
        private void ProcessDeserters()
        {
            foreach (var p in exitingPlayers)
            {
                TimeSpan sinceSync = DateTime.Now.Subtract(p.Value);
                if (sinceSync.TotalSeconds >= desertionTimer)
                {
                    lock (exitingPlayers) exitingPlayers.Remove(p.Key);
                    p.Key.health = -1;
                    p.Key.triggerEvent("softBounds:died", true);
                }
            }
        }

        /// <summary>
        /// Places objective boxes down
        /// </summary>
        private void BuildObjects()
        {
            foreach (var o in objectives)
            {
                API.createObject(2107849419, o, new Vector3(0f, 0f, 0f));
                ColShape activator = API.createCylinderColShape(o, 1f, 10f);
            }
        }

        /// <summary>
        /// Builds the boundaries of the map
        /// </summary>
        private void BuildBounds()
        {

            API.consoleOutput("box");
            // Soft bounds
            BoxPosition softBoundBox = bounds["soft"];
            softBounds = API.create3DColShape(softBoundBox.edge1, softBoundBox.edge2);
            softBounds.onEntityExitColShape += (shape, entity) =>
            {
                Client player;
                if ((player = API.getPlayerFromHandle(entity)) != null)
                {
                    player.triggerEvent("softBounds:exit", true);
                    lock (exitingPlayers)
                    {
                        exitingPlayers.Add(player, DateTime.Now);
                    }
                }
            };
            softBounds.onEntityEnterColShape += (shape, entity) =>
            {
                Client player;
                if ((player = API.getPlayerFromHandle(entity)) != null)
                {
                    player.triggerEvent("softBounds:enter", true);
                    lock (exitingPlayers)
                    {
                        exitingPlayers.Remove(player);
                    }
                }
            };

            //Hard bounds
            BoxPosition hardBoundBox = bounds["hard"];
            hardBounds = API.create3DColShape(hardBoundBox.edge1, hardBoundBox.edge2);
            hardBounds.onEntityExitColShape += (shape, entity) =>
            {
                Client player;
                if ((player = API.getPlayerFromHandle(entity)) != null)
                {
                    player.health = -1;
                    player.sendChatMessage("~r~You were warned.~w~");
                }
            };

            API.consoleOutput("end box");

        }

        /// <summary>
        /// Sends initial UI data to client
        /// </summary>
        private void SendUIData(Client player)
        {
            player.downloadData(API.toJson(new Dictionary<string, object>()
            { { "objectives", objectives },
            }));
        }

        [Command("testcol")]
        public void TestColShape(Client player)
        {
            if (hardBounds.containsEntity(player.handle))
            {
                player.sendChatMessage("yep");
            }
            else
            {
                player.sendChatMessage("nope");
            }
        }

        /// <summary>
        /// Auto picks team based on least filled OR random if even.
        /// </summary>
        /// <param name="player"></param>
        private void PickTeam(Client player)
        {
            if (attackers.Count < defenders.Count)
            {

                lock (attackers) attackers.Add(player);
                player.setSyncedData("team", 0);
            }
            else if (defenders.Count < attackers.Count)
            {
                lock (defenders) defenders.Add(player);
                player.setSyncedData("team", 1);
            }
            else
            {
                if (random.Next(2) == 0)
                {
                    lock (attackers) attackers.Add(player);
                    player.setSyncedData("team", 0);
                }
                else
                {
                    lock (defenders) defenders.Add(player);
                    player.setSyncedData("team", 1);
                }
            }
        }

        /// <summary>
        /// Spawns a player based on phase number
        /// </summary>
        /// <param name="player"></param>
        private void SpawnPlayer(Client player)
        {
            int team = (int)player.getSyncedData("team");
            Vector3 regularSpawn = spawns[phase + team];
            Vector3 spawnPoint = GetJiggleSpawn(regularSpawn);
            PedHash skin = skins[team][random.Next(skins[team].Count)];

            player.triggerEvent("spawned", true);
            player.position = spawnPoint;
            player.setSkin(skin);
            player.rotation = new Vector3(0, 0, random.Next(360) - 180);
            player.nametagVisible = false;
            player.freezePosition = false;
            player.invincible = false;
            player.health = 100;
            player.armor = 100;
            player.giveWeapon(WeaponHash.Knife, 500, false, true);
            player.giveWeapon(WeaponHash.Pistol, 500, false, true);
            if (team == 0)
            {
                player.giveWeapon(WeaponHash.CarbineRifle, 1500, true, true);
            }
            else
            {
                player.giveWeapon(WeaponHash.AssaultRifle, 1500, true, true);
            }
        }

        /// <summary>
        /// Slightly moves a spawnpoint from it's center point.
        /// </summary>
        /// <param name="center"></param>
        /// <returns></returns>
        private Vector3 GetJiggleSpawn(Vector3 center)
        {
            double x = random.NextDouble();
            double y = random.NextDouble();

            return center + new Vector3(x, y, 0);
        }

        [Command("suicide")]
        public void Suicide(Client player)
        {
            if (deadPlayers.ContainsKey(player))
            {
                return;
            }

            player.health = -1;
        }

        /// <summary>
        /// Spawns a car
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="model"></param>
        [Command("car")]
        public void MCarSpawn(Client sender, VehicleHash model)
        {
            var rot = API.getEntityRotation(sender.handle);
            var veh = API.createVehicle(model, sender.position, new Vector3(0, 0, rot.Z), 0, 0);
            API.setPlayerIntoVehicle(sender, veh, -1);
        }

        /// <summary>
        /// Prints location in game and to console.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="desc"></param>
        [Command("sl", GreedyArg = true)]
        public void PrintLoc(Client sender, string desc)
        {
            Vector3 pos = sender.position;
            float rot = sender.rotation.Z;
            sender.sendChatMessage($"~g~Location: ~b~X:~w~ {pos.X} ~b~Y:~w~ {pos.Y} ~b~X:~w~ {pos.Z} ~b~A:~w~ {rot}");
            API.consoleOutput($"POSITION HELPER:\n=> {desc}\n===> Position: new Vector3({pos.X}f, {pos.Y}f, {pos.Z}f)\n===> Rotation: new Vector3(0f, 0f, {rot}f)");
        }
    }

    internal class BoxPosition
    {
        public Vector3 edge1;
        public Vector3 edge2;
        public BoxPosition(Vector3 e1, Vector3 e2)
        {

            Vector3 tmp1 = e1.Copy();
            Vector3 tmp2 = e2.Copy();

            tmp1.X = Math.Min(e1.X, e2.X);
            tmp2.X = Math.Max(e1.X, e2.X);

            tmp1.Y = Math.Min(e1.Y, e2.Y);
            tmp2.Y = Math.Max(e1.Y, e2.Y);

            tmp1.Z = Math.Min(e1.Z, e2.Z);
            tmp2.Z = Math.Max(e1.Z, e2.Z);

            this.edge1 = tmp1;
            this.edge2 = tmp2;
        }
    }
}