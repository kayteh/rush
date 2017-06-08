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

        private int respawnTimer = 15;

        private int time = 0;

        private int phase = 0;

        private readonly Dictionary<string, BoxPosition> bounds = new Dictionary<string, BoxPosition>()
        { { "soft", new BoxPosition(new Vector3(1295.73f, 2745.169f, 0f), new Vector3(915.9402f, 2585.65f, 1000f)) }, { "hard", new BoxPosition(new Vector3(1319.016f, 2779.176f, 0f), new Vector3(874.257f, 2548.797f, 1000f)) },
        };

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
        }

        private void OnStart()
        {
            BuildBounds();
            SetTime();
            API.sendChatMessageToAll("~p~Rush started!~w~");
        }

        private void OnDeath(Client player, NetHandle entityKiller, int weapon)
        {
            player.removeAllWeapons();
            player.triggerEvent("spawned", false);
            deadPlayers.Add(player, DateTime.Now);
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
        }

        private void OnTick()
        {
            ProcessDeaths();
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
                    deadPlayers.Remove(p.Key);
                    SpawnPlayer(p.Key);
                }
            }
        }

        /// <summary>
        /// Builds the boundaries of the map
        /// </summary>
        private void BuildBounds()
        {
            // Soft bounds
            BoxPosition softBoundBox = bounds["soft"];
            ColShape softBounds = API.create3DColShape(softBoundBox.edge1, softBoundBox.edge2);
            softBounds.onEntityExitColShape += (shape, entity) =>
            {
                Client player;
                if ((player = API.getPlayerFromHandle(entity)) != null)
                {
                    player.triggerEvent("softBounds:start", true);
                }
            };
        }

        /// <summary>
        /// Auto picks team based on least filled OR random if even.
        /// </summary>
        /// <param name="player"></param>
        private void PickTeam(Client player)
        {
            if (attackers.Count < defenders.Count)
            {
                attackers.Add(player);
                player.setSyncedData("team", 0);
            }
            else if (defenders.Count < attackers.Count)
            {
                defenders.Add(player);
                player.setSyncedData("team", 1);
            }
            else
            {
                if (random.Next(2) == 0)
                {
                    attackers.Add(player);
                    player.setSyncedData("team", 0);
                }
                else
                {
                    defenders.Add(player);
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
            API.consoleOutput($"POSITION HELPER:\n=> {desc}\n===> Position: Vector3({pos.X}f, {pos.Y}f, {pos.Z}f)\n===> Rotation: Vector3(0f, 0f, {rot}f)");
        }
    }

    internal class BoxPosition
    {
        public Vector3 edge1;
        public Vector3 edge2;
        public BoxPosition(Vector3 edge1, Vector3 edge2)
        {
            this.edge1 = edge1;
            this.edge2 = edge2;
        }
    }

}