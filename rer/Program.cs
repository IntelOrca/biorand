using System;
using System.IO;
using rer;

class Program
{
    private static Random _random = new Random();

    public static void Main(string[] args)
    {
        var randomItems = new Random(2);
        var randomMusic = new Random(2);

        var factory = new PlayGraphFactory(randomItems);
        var gameData = GameDataReader.Read(@"F:\games\re2", @"F:\games\re2r");
        CheckRoomItems(gameData);
        factory.Create(gameData, @"M:\git\rer\rer\data\clairea.json");
        // factory.Save();

        var bgmRandomiser = new BgmRandomiser(@"F:\games\re2", @"F:\games\re2r");
        bgmRandomiser.Randomise(randomMusic);
    }

    private static void CheckRoomItems(GameData gameData)
    {
        // Test what items are in the room
        var rtd = gameData.GetRdt(RdtId.Parse("615"))!;
        // rtd.SetItem(1, 0x43, 1); // crank
        // rtd.SetItem(3, 0x33, 1); // red jewel
        // rtd.SetItem(7, 0x2F, 1); // lighter
        // rtd.SetItem(6, 0x0D, 1); // colt
        // rtd.SetItem(7, 0x60, 1); // mo disk
        // rtd.SetItem(8, 0x61, 1); // umbrella card
        foreach (var rdt in gameData.Rdts)
        {
            rdt.Save();
        }
    }
}
