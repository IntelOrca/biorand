using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard;

namespace IntelOrca.Scd
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var extractPath = GetOption(args, "-x");
            var outputPath = GetOption(args, "-o");
            var paths = args.Where(x => !x.StartsWith("-")).ToArray();

            if (extractPath != null)
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
                return 1;
            }

            return PrintUsage();
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
