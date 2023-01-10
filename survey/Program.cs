using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace IntelOrca.Biohazard.Survey
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private extern static bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        private static string _jsonPath = @"M:\git\rer\IntelOrca.Biohazard\data\re2\enemy.json";

        private static bool _exit;
        private static byte[] _buffer = new byte[64];
        private static List<EnemyPosition> _enemyPositions = new List<EnemyPosition>();

        private static void LoadJSON()
        {
            var json = File.ReadAllText(_jsonPath);
            _enemyPositions = JsonSerializer.Deserialize<List<EnemyPosition>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
        }

        private static void SaveJSON()
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            var positions = _enemyPositions
                .OrderBy(x => x.Room)
                .ThenByDescending(x => x.Y)
                .ThenBy(x => x.X)
                .ThenBy(x => x.Z)
                .ToArray();
            if (positions.Length > 0)
            {
                foreach (var pos in positions)
                {
                    sb.Append($"    {{ \"room\": \"{pos.Room}\", \"x\": {pos.X}, \"y\": {pos.Y}, \"z\": {pos.Z}, \"d\": {pos.D}, \"f\": {pos.F} }},\n");
                }
                sb.Remove(sb.Length - 2, 2);
                sb.Append('\n');
            }
            sb.Append("]\n");
            File.WriteAllText(_jsonPath, sb.ToString());
        }

        public static void Main(string[] args)
        {
            LoadJSON();
            Console.CancelKeyPress += Console_CancelKeyPress;
            while (!_exit)
            {
                var pAll = Process.GetProcesses();
                var p = pAll.FirstOrDefault(x => x.ProcessName.StartsWith("bio2"));
                if (p != null)
                {
                    Spy(p);
                }

                Console.WriteLine("Waiting for RE 2 to start...");
                Thread.Sleep(4000);
            }
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            _exit = true;
        }

        private static void Spy(Process p)
        {
            var lastGameState = new GameState();
            var gameState = new GameState();
            while (!p.HasExited)
            {
                GetGameState(p, gameState);
                if (!gameState.Equals(lastGameState))
                {
                    Console.CursorTop = 0;
                    Console.WriteLine($"Key: {gameState.Key,6:X4}");
                    Console.WriteLine($"Room:   {gameState.Stage + 1:X}{gameState.Room:X2}");
                    Console.WriteLine($"Cut: {gameState.Cut,6}");
                    Console.WriteLine($"X: {gameState.X,8}");
                    Console.WriteLine($"Y: {gameState.Y,8}");
                    Console.WriteLine($"Z: {gameState.Z,8}");
                    Console.WriteLine($"D: {gameState.D,8}");
                    Console.WriteLine($"F: {gameState.Floor,8}");

                    if (gameState.Key == 0x2300)
                    {
                        AddEnemyPosition(gameState);
                    }

                    Console.WriteLine("--------------------");
                    for (int i = 0; i < 10; i++)
                    {
                        var index = _enemyPositions.Count - i - 1;
                        if (index >= _enemyPositions.Count)
                            break;

                        var pos = _enemyPositions[index];
                        Console.WriteLine("{0}, {1}, {2}, {3}, {4}                         ", pos.Room, pos.X, pos.Y, pos.Z, pos.D);
                    }
                }
                Thread.Sleep(10);

                (gameState, lastGameState) = (lastGameState, gameState);
            }
        }

        private static void AddEnemyPosition(GameState state)
        {
            var pos = new EnemyPosition()
            {
                Room = $"{state.Stage + 1:X}{state.Room:X2}",
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                D = state.D,
                F = state.Floor,
            };
            if (!_enemyPositions.Contains(pos))
            {
                _enemyPositions.Add(pos);
                SaveJSON();
            }
        }

        private static void GetGameState(Process p, GameState gameState)
        {
            var buffer = _buffer;

            ReadMemory(p, 0x00988604, buffer, 0, 2);
            gameState.Key = BitConverter.ToUInt16(buffer, 0);

            ReadMemory(p, 0x0098890C, buffer, 0, 10);
            gameState.X = BitConverter.ToInt16(buffer, 0);
            gameState.Y = BitConverter.ToInt16(buffer, 2);
            gameState.Z = BitConverter.ToInt16(buffer, 4);
            gameState.D = BitConverter.ToInt16(buffer, 8);

            ReadMemory(p, 0x00989FF6, buffer, 0, 1);
            gameState.Floor = buffer[0];

            ReadMemory(p, 0x0098EB14, buffer, 0, 10);
            gameState.Stage = buffer[0];
            gameState.Room = buffer[2];
            gameState.Cut = buffer[4];
            gameState.LastCut = buffer[6];
        }

        private unsafe static bool ReadMemory(Process process, int address, byte[] buffer, int offset, int length)
        {
            IntPtr outLength;
            fixed (byte* bufferP = buffer)
            {
                var dst = bufferP + offset;
                return ReadProcessMemory(process.Handle, (IntPtr)address, (IntPtr)dst, (IntPtr)length, out outLength);
            }
        }

        private class GameState : IEquatable<GameState>
        {
            public ushort Key { get; set; }
            public byte Stage { get; set; }
            public byte Room { get; set; }
            public byte Cut { get; set; }
            public byte LastCut { get; set; }
            public short X { get; set; }
            public short Y { get; set; }
            public short Z { get; set; }
            public short D { get; set; }
            public byte Floor { get; set; }

            public override bool Equals(object? obj)
            {
                return Equals(obj as GameState);
            }

            public bool Equals(GameState? other)
            {
                return other is GameState state &&
                       Key == state.Key &&
                       Stage == state.Stage &&
                       Room == state.Room &&
                       Cut == state.Cut &&
                       LastCut == state.LastCut &&
                       X == state.X &&
                       Y == state.Y &&
                       Z == state.Z &&
                       D == state.D &&
                       Floor == state.Floor;
            }

            public override int GetHashCode()
            {
                HashCode hash = new HashCode();
                hash.Add(Key);
                hash.Add(Stage);
                hash.Add(Room);
                hash.Add(Cut);
                hash.Add(LastCut);
                hash.Add(X);
                hash.Add(Y);
                hash.Add(Z);
                hash.Add(D);
                hash.Add(Floor);
                return hash.ToHashCode();
            }
        }

        public struct EnemyPosition : IEquatable<EnemyPosition>
        {
            public string? Room { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int D { get; set; }
            public int F { get; set; }

            public override bool Equals(object? obj)
            {
                return obj is EnemyPosition pos ? Equals(pos) : false;
            }

            public bool Equals(EnemyPosition other)
            {
                return other is EnemyPosition position &&
                       Room == position.Room &&
                       X == position.X &&
                       Y == position.Y &&
                       Z == position.Z &&
                       D == position.D &&
                       F == position.F;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Room, X, Y, Z, D, F);
            }

            public bool IsVeryClose(EnemyPosition other)
            {
                if (Room != other.Room)
                    return false;
                if (Y != other.Y)
                    return false;

                var deltaX = X - other.X;
                var deltaY = Y - other.Y;
                var dist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (dist <= 1000)
                    return true;

                return false;
            }
        }
    }
}
