using System;
using System.IO;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.RE2;
using Xunit;

namespace IntelOrca.Biohazard.Tests
{
    public class TestRE1Rando : TestBaseRandomizer
    {
        public override byte Game => 1;
    }

    public class TestRE2Rando : TestBaseRandomizer
    {
        public override byte Game => 2;

        [Fact]
        public void RandomizeEnemyPlacements()
        {
            RandomizeEnemyPlacementsInner();
        }
    }

    public abstract class TestBaseRandomizer
    {
        public abstract byte Game { get; }

        [Fact]
        public void RandomizeDoors()
        {
            var config = GetBaseConfig();
            config.RandomDoors = true;
            Randomize(config);
        }

        protected void RandomizeEnemyPlacementsInner()
        {
            var config = GetBaseConfig();
            config.RandomEnemyPlacement = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeItems_Shuffle()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.AlternativeRoutes = false;
            config.ShuffleItems = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeItems_AlternativeRoutes()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.AlternativeRoutes = true;
            config.ShuffleItems = false;
            Randomize(config);
        }

        [Fact]
        public void RandomizeNPCs()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.RandomItems = false;
            config.RandomEnemies = false;
            config.RandomNPCs = false;
            config.ChangePlayer = false;
            config.RandomNPCs = true;
            Randomize(config);
        }

        [Fact]
        public void RandomizeBGM()
        {
            var config = GetBaseConfig();
            config.RandomDoors = false;
            config.RandomItems = false;
            config.RandomEnemies = false;
            config.RandomNPCs = false;
            config.ChangePlayer = false;
            config.RandomBgm = true;
            config.EnabledBGMs = new bool[5];
            for (int i = 0; i < 5; i++)
                config.EnabledBGMs[i] = i % 2 == 0;
            Randomize(config);
        }

        protected void Randomize(RandoConfig config)
        {
            var dataPath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "IntelOrca.Biohazard", "data");
            Environment.SetEnvironmentVariable("BIORAND_DATA", dataPath);

            var reInstall = GetInstallConfig();
            var rando = GetRandomizer();
            rando.Generate(config, reInstall);
        }

        private BaseRandomiser GetRandomizer()
        {
            return Game == 1 ?
                (BaseRandomiser)new Re1Randomiser(null) :
                (BaseRandomiser)new Re2Randomiser(null);
        }

        private RandoConfig GetBaseConfig()
        {
            var config = new RandoConfig();
            config.Player = 0;
            config.GameVariant = 0;
            config.Game = Game;
            config.Seed = 12345;

            config.RandomDoors = false;
            config.AreaCount = 2;
            config.AreaSize = 4;

            config.RandomItems = true;
            config.IncludeDocuments = true;
            config.AlternativeRoutes = false;
            config.ProtectFromSoftLock = true;
            config.ShuffleItems = false;

            config.RandomEnemies = true;
            config.RandomEnemyPlacement = false;
            config.EnemyDifficulty = 2;
            config.EnemyRatios = new byte[5];
            for (int i = 0; i < 5; i++)
                config.EnemyRatios[i] = 7;

            config.RandomNPCs = false;
            config.EnabledNPCs = new bool[10];
            for (int i = 0; i < 10; i++)
                config.EnabledNPCs[i] = i % 2 == 0;

            if (config.Game == 2)
            {
                config.ChangePlayer = true;
                config.Player0 = 0;
                config.Player1 = 0;
            }
            else
            {
                config.ChangePlayer = false;
            }

            config.RandomBgm = false;
            config.EnabledBGMs = new bool[5];
            for (int i = 0; i < 5; i++)
                config.EnabledBGMs[i] = i % 2 == 0;
            return config;
        }

        private static ReInstallConfig GetInstallConfig()
        {
            var reInstall = new ReInstallConfig();
            reInstall.SetInstallPath(0, TestInfo.GetInstallPath(0));
            reInstall.SetInstallPath(1, TestInfo.GetInstallPath(1));
            reInstall.SetEnabled(0, true);
            reInstall.SetEnabled(1, true);
            return reInstall;
        }
    }
}