using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Cli
{
    internal class ApClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly ArchipelagoSession _session;
        private readonly ReProcess _reProcess;
        private ReFlags _lastItemFlags;
        private IItemHelper _itemHelper = new Re2ItemHelper();
        private bool _isPickingUpItem;
        private bool _victory;

        public ApClient(ReProcess reProcess, string host, int port)
        {
            _host = host;
            _port = port;
            _reProcess = reProcess;
            _session = ArchipelagoSessionFactory.CreateSession(_host, _port);
        }

        public async Task ConnectAsync()
        {
            var roomInfo = await _session.ConnectAsync();
            Console.WriteLine($"Connected to {roomInfo.SeedName} {roomInfo.Version.ToVersion()}");
        }

        public async Task Login(string name)
        {
            var loginResult = await _session.LoginAsync("Resident Evil", name, ItemsHandlingFlags.AllItems);
            if (!loginResult.Successful)
            {
                throw new Exception("Failed to login.");
            }
            _session.MessageLog.OnMessageReceived += MessageLog_OnMessageReceived;
            _session.Items.ItemReceived += Items_ItemReceived;
        }

        public async Task RunAsync()
        {
            await Task.Delay(1000);
            while (_session.Socket.Connected)
            {
                await CheckVictoryAsync();
                await CheckPickedUpItemsAsync();
                await UpdatePickupItemAsync();
                await Task.Delay(100);
            }
            Console.Error.WriteLine("Disconnected");
        }

        private async Task CheckVictoryAsync()
        {
            try
            {
                if (_victory)
                    return;

                if (_reProcess.CurrentStage == 6 && _reProcess.CurrentRoom == 0)
                {
                    Log($"Victory!");
                    var missingLocations = _session.Locations.AllMissingLocations.ToArray();
                    await _session.Locations.CompleteLocationChecksAsync(missingLocations);
                    await LogLocationsLooted(missingLocations);
                    _victory = true;
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private async Task CheckPickedUpItemsAsync()
        {
            try
            {
                var flags = _reProcess.ItemFlags;
                if (flags != _lastItemFlags)
                {
                    var changedFlags = flags & (flags ^ _lastItemFlags);

                    var allItems = _session.Locations.AllLocations;
                    var allCheckedItems = flags.Keys
                        .Select(x => (long)x)
                        .Intersect(allItems)
                        .ToArray();
                    await _session.Locations.CompleteLocationChecksAsync(allCheckedItems);
                    await LogLocationsLooted(changedFlags.Keys.Select(x => (long)x).ToArray());

                    _lastItemFlags = _reProcess.ItemFlags;
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private async Task UpdatePickupItemAsync()
        {
            try
            {
                var isPickingUpItem = _reProcess.HudMode == 2;
                if (isPickingUpItem == _isPickingUpItem)
                {
                    return;
                }

                if (isPickingUpItem)
                {
                    var flag = _reProcess.PickupFlag;
                    Log($"Picking up item at location {flag}.");

                    var allLocations = _session.Locations.AllLocations;
                    if (allLocations.Contains(flag))
                    {
                        var locationInfo = await _session.Locations.ScoutLocationsAsync(flag);
                        foreach (var l in locationInfo.Locations)
                        {
                            var itemName = _session.Items.GetItemName(l.Item);
                            var playerName = _session.Players.GetPlayerName(l.Player);
                            Log($"  Item: {itemName} for {playerName} at location {l.Location}");

                            if (l.Player != _session.ConnectionInfo.Slot)
                            {
                                _reProcess.PickupType = Re2ItemIds.None;
                                _reProcess.PickupName = Re2ItemIds.None;
                                _reProcess.PickupAmount = 0;
                            }
                            else
                            {
                                _reProcess.PickupType = (byte)(l.Item & 0xFF);
                                _reProcess.PickupName = (byte)(l.Item & 0xFF);
                                _reProcess.PickupAmount = (byte)((l.Item >> 8) & 0xFF);
                            }
                            break;
                        }
                    }
                }
                _isPickingUpItem = isPickingUpItem;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private void MessageLog_OnMessageReceived(LogMessage message)
        {
            Log(string.Concat(message.Parts.Select(x => x.Text)));
        }

        private void Items_ItemReceived(ReceivedItemsHelper helper)
        {
            var items = new List<NetworkItem>();
            while (helper.Any())
            {
                // Prevent own items from appearing in box, we actually picked them up...
                var receivedItem = helper.DequeueItem();
                if (receivedItem.Player != _session.ConnectionInfo.Slot)
                {
                    var itemName = helper.GetItemName(receivedItem.Item);
                    var playerName = _session.Players.GetPlayerName(receivedItem.Player);
                    Log($"Received {itemName} from {playerName}");
                    items.Add(receivedItem);
                }
            }
            AddToItemBox(items.ToArray());
        }

        private async Task LogLocationsLooted(long[] locations)
        {
            try
            {
                Log($"New locations looted: [{string.Join(", ", locations)}]");

                // We need to filter locations to just valid AP ones
                var allItems = _session.Locations.AllLocations;
                var validLocations = locations
                    .Select(x => (long)x)
                    .Intersect(allItems)
                    .ToArray();

                var locationInfo = await _session.Locations.ScoutLocationsAsync(validLocations);
                foreach (var l in locationInfo.Locations)
                {
                    var itemName = _session.Items.GetItemName(l.Item);
                    var playerName = _session.Players.GetPlayerName(l.Player);
                    Log($"Found {itemName} for {playerName} at location {l.Location}");
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private void AddToItemBox(NetworkItem[] networkItems)
        {
            var itemBox = _reProcess.GetItemBox();
            foreach (var networkItem in networkItems)
            {
                var type = (byte)(networkItem.Item & 0xFF);
                var amount = (byte)((networkItem.Item >> 8) & 0xFF);
                var attributes = _itemHelper.GetItemAttributes(type);
                var combine =
                    attributes.HasFlag(ItemAttribute.Ammo) ||
                    attributes.HasFlag(ItemAttribute.InkRibbon);
                itemBox.Add(type, amount, combine);
            }
            _reProcess.SetItemBox(itemBox);
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
        }

        private void Log(Exception ex)
        {
            var backup = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = backup;
        }
    }
}
