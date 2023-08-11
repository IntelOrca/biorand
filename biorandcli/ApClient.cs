using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using IntelOrca.Biohazard.RE2;

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
                await CheckPickedUpItemsAsync();
                await Task.Delay(100);
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
                    var newItems = changedFlags.Keys
                        .Select(x => (long)x)
                        .Intersect(allItems)
                        .ToArray();

                    var locationInfo = await _session.Locations.ScoutLocationsAsync(newItems);
                    foreach (var l in locationInfo.Locations)
                    {
                        var itemName = _session.Items.GetItemName(l.Item);
                        var playerName = _session.Players.GetPlayerName(l.Player);
                        Log($"Found {itemName} for {playerName}");
                    }

                    await _session.Locations.CompleteLocationChecksAsync(allCheckedItems);

                    _lastItemFlags = _reProcess.ItemFlags;
                }
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
                var receivedItem = helper.DequeueItem();
                var itemName = helper.GetItemName(receivedItem.Item);
                var playerName = _session.Players.GetPlayerName(receivedItem.Player);
                Log($"Received {itemName} from {playerName}");
                items.Add(receivedItem);
            }
            AddToItemBox(items.ToArray());
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
