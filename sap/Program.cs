using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard;

namespace IntelOrca.Scd
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var extractPath = GetOption(args, "-x");
                var outputPath = GetOption(args, "-o");
                if (extractPath != null)
                {
                    return Extract(extractPath, outputPath);
                }
                else if (args.Length != 0)
                {
                    if (outputPath != null)
                    {
                        return Create(outputPath, args);
                    }
                    else
                    {
                        var path = args[0];
                        if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                        {
                            return Extract(path, outputPath);
                        }
                        else if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            return Create(outputPath, args);
                        }
                    }
                }
                return PrintUsage();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int Extract(string extractPath, string outputPath)
        {
            if (!File.Exists(extractPath))
            {
                Console.Error.WriteLine($"'{extractPath}' does not exist.");
                return 1;
            }

            var sapFile = new SapFile(extractPath);
            if (outputPath == null)
            {
                outputPath = Environment.CurrentDirectory;
            }
            var fileNameFormat = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(extractPath) + ".{0:00}.wav");

            Directory.CreateDirectory(outputPath);
            for (int i = 0; i < sapFile.WavFiles.Count; i++)
            {
                var wavPath = string.Format(fileNameFormat, i);
                File.WriteAllBytes(wavPath, sapFile.WavFiles[i]);
            }
            return 0;
        }

        private static int Create(string outputPath, string[] args)
        {
            var wavFiles = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-o")
                {
                    i++;
                }
                else
                {
                    if (!File.Exists(args[i]))
                    {
                        Console.Error.WriteLine("Unable to open '{0}'", args[i]);
                        return 1;
                    }
                    wavFiles.Add(args[i]);
                }
            }

            if (outputPath == null)
            {
                outputPath = Path.ChangeExtension(Path.GetFileName(wavFiles[0]), ".sap");
            }

            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                var bw = new BinaryWriter(fs);
                bw.Write((long)1);
                foreach (var wav in wavFiles)
                {
                    using (var fs2 = new FileStream(wav, FileMode.Open, FileAccess.Read))
                    {
                        fs2.CopyTo(fs);
                    }
                }
            }
            return 0;
        }

        private static string GetOption(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name)
                {
                    if (i + 1 >= args.Length)
                        return null;
                    return args[i + 1];
                }
            }
            return null;
        }

        private static int PrintUsage()
        {
            Console.WriteLine("Resident Evil SAP packer / extractor");
            Console.WriteLine("usage: sap -x <sap> [-o directory]");
            Console.WriteLine("       scd -o <sap> <wav> [<wav> ...]");
            return 1;
        }
    }
}
