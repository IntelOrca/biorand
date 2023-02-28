using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.BioRand;

namespace IntelOrca.Emd
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var inputPaths = GetArguments(args);
                if (inputPaths.Length == 0)
                    return PrintUsage();

                var outputPath = GetOption(args, "-o");
                if (outputPath == null)
                {
                    outputPath = Path.ChangeExtension(Path.GetFileName(inputPaths[0]), ".obj");
                }

                var outputEmdPath = Path.ChangeExtension(outputPath, ".emd");
                var outputPldPath = Path.ChangeExtension(outputPath, ".pld");
                var outputObjPath = Path.ChangeExtension(outputPath, ".obj");
                var outputPngPath = Path.ChangeExtension(outputPath, ".png");
                var outputMd2Path = Path.ChangeExtension(outputPath, ".md2");
                var outputTimPath = Path.ChangeExtension(outputPath, ".tim");

                var inputEmdPath = inputPaths.FirstOrDefault(x => x.EndsWith(".emd", StringComparison.OrdinalIgnoreCase));
                if (inputEmdPath == null)
                    inputEmdPath = inputPaths.FirstOrDefault(x => x.EndsWith(".pld", StringComparison.OrdinalIgnoreCase));
                if (inputEmdPath == null)
                    return PrintUsage();

                var inputMd2Path = inputPaths.FirstOrDefault(x => x.EndsWith(".md2", StringComparison.OrdinalIgnoreCase));
                var inputObjPath = inputPaths.FirstOrDefault(x => x.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
                var inputPngPath = inputPaths.FirstOrDefault(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

                var importing = inputMd2Path != null || inputObjPath != null || inputPngPath != null;

                // Import EMD/PLD
                ModelFile modelFile;
                TimFile timFile = null;
                if (inputEmdPath.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                {
                    modelFile = new EmdFile(inputEmdPath);
                    var inputTimPath = Path.ChangeExtension(inputEmdPath, ".tim");
                    if (File.Exists(inputTimPath))
                    {
                        timFile = new TimFile(inputTimPath);
                    }
                }
                else if (inputEmdPath.EndsWith(".pld", StringComparison.OrdinalIgnoreCase))
                {
                    var pldFile = new PldFile(inputEmdPath);
                    modelFile = pldFile;
                    timFile = pldFile.GetTim();
                }
                else
                {
                    Console.Error.WriteLine("An .emd or .pld file must be imported.");
                    return 1;
                }

                if (importing)
                {
                    if (inputMd2Path != null)
                    {
                        // Import MD2
                        modelFile.SetMd2(File.ReadAllBytes(inputMd2Path));
                    }
                    else if (inputObjPath != null)
                    {
                        // Import OBJ
                        modelFile.ImportObj(inputObjPath);
                    }
                    if (inputPngPath != null)
                    {
                        var importedTimFile = ImportTimFile(inputPngPath);
                        if (modelFile is PldFile pldFile)
                        {
                            pldFile.SetTim(importedTimFile);
                        }
                        else
                        {
                            importedTimFile.Save(outputTimPath);
                        }
                    }
                    {
                        if (modelFile is PldFile pldFile)
                            pldFile.Save(outputPldPath);
                        else if (modelFile is EmdFile emdFile)
                            emdFile.Save(outputEmdPath);
                    }
                }
                else
                {
                    modelFile.ExportObj(outputObjPath);
                    timFile?.ToBitmap((x, y) => x / 128).Save(outputPngPath);
                    File.WriteAllBytes(outputMd2Path, modelFile.GetMd2());
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static TimFile ImportTimFile(string path)
        {
            using (var bitmap = (Bitmap)Bitmap.FromFile(path))
            {
                var timFile = new TimFile(bitmap.Width, bitmap.Height);
                timFile.ImportBitmap(bitmap);
                return timFile;
            }
        }

        private static string[] GetArguments(string[] args)
        {
            var result = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    i++;
                }
                else
                {
                    result.Add(args[i]);
                }
            }
            return result.ToArray();
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
            Console.WriteLine("Resident Evil EMD import / export");
            Console.WriteLine("usage: emd PL00.PLD");
            Console.WriteLine("       emd EM52.EMD [-o EM52.obj]");
            Console.WriteLine("       emd EM52.EMD my.obj my.png [-o custom.emd]");
            return 1;
        }
    }
}
