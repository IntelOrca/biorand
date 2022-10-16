using System;
using System.IO;
using rer;

class Program
{
    private static Random _random = new Random();

    public static void Main(string[] args)
    {
        var factory = new PlayGraphFactory();
        var gameData = GameDataReader.Read(@"F:\games\re2", @"F:\games\re2r");
        factory.Create(gameData, @"M:\git\rer\rer\data\clairea.json");
        factory.Save();

        var random = new Random();
        var bgmRandomiser = new BgmRandomiser(@"F:\games\re2", @"F:\games\re2r");
        bgmRandomiser.Randomise(random);
    }
}
