using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Scd
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var paths = args.Where(x => !x.StartsWith("-")).ToArray();
            var rdtPath = paths.FirstOrDefault();
            if (rdtPath == null)
            {
                return PrintUsage();
            }

            var bioVersion = BioVersion.Biohazard2;
            var version = GetOption(args, "-v");
            if (version != null)
            {
                if (!int.TryParse(version, out var parsedVersion) || parsedVersion < 1 || parsedVersion > 3)
                {
                    Console.Error.WriteLine("Invalid version");
                    return 1;
                }
                if (parsedVersion == 1)
                    bioVersion = BioVersion.Biohazard1;
                else if (parsedVersion == 3)
                    bioVersion = BioVersion.Biohazard3;
            }

            if (args.Contains("-x"))
            {
                var rdtFile = new RdtFile(rdtPath, bioVersion);
                File.WriteAllBytes("init.scd", rdtFile.GetScd(BioScriptKind.Init));
                if (bioVersion != BioVersion.Biohazard3)
                    File.WriteAllBytes("main.scd", rdtFile.GetScd(BioScriptKind.Main));
                for (int i = 0; i < rdtFile.EventScriptCount; i++)
                {
                    File.WriteAllBytes($"event_{i:X2}.scd", rdtFile.GetScd(BioScriptKind.Event, i));
                }
                return 0;
            }
            else if (args.Contains("-d"))
            {
                if (rdtPath.EndsWith(".rdt", StringComparison.OrdinalIgnoreCase))
                {
                    var rdtFile = new RdtFile(rdtPath, bioVersion);
                    foreach (var listing in new[] { false, true })
                    {
                        if (listing && !args.Contains("--list"))
                            continue;

                        var script = rdtFile.DisassembleScd(listing);
                        var extension = listing ? ".lst" : ".s";
                        File.WriteAllText(Path.ChangeExtension(Path.GetFileName(rdtPath), extension), script);
                    }
                }
                else if (rdtPath.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                {
                    var kind = args.Contains("--main") ? BioScriptKind.Main : BioScriptKind.Init;
                    var scd = File.ReadAllBytes(rdtPath);
                    var s = Diassemble(bioVersion, kind, scd);
                    var sPath = Path.ChangeExtension(rdtPath, ".s");
                    File.WriteAllText(sPath, s);
                    var lst = Diassemble(bioVersion, kind, scd, listing: true);
                    var lstPath = Path.ChangeExtension(rdtPath, ".lst");
                    File.WriteAllText(lstPath, lst);
                }
                return 0;
            }
            else if (args.Contains("--decompile"))
            {
                if (rdtPath.EndsWith(".rdt", StringComparison.OrdinalIgnoreCase))
                {
                    var rdtFile = new RdtFile(rdtPath, bioVersion);
                    var script = rdtFile.DecompileScd();
                    File.WriteAllText(Path.ChangeExtension(Path.GetFileName(rdtPath), ".bio"), script);
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Only RDT files can be decompiled.");
                    return 1;
                }
            }
            else
            {
                if (rdtPath.EndsWith(".s", StringComparison.OrdinalIgnoreCase))
                {
                    var s = File.ReadAllText(rdtPath);
                    var scdAssembler = new ScdAssembler();
                    var result = scdAssembler.Assemble(rdtPath, s);
                    if (result == 0)
                    {
                        if (scdAssembler.OutputInit != null && scdAssembler.OutputInit.Length != 0)
                        {
                            var scdPath = Path.ChangeExtension(rdtPath, "init.scd");
                            File.WriteAllBytes(scdPath, scdAssembler.OutputInit);
                        }
                        if (scdAssembler.OutputMain != null && scdAssembler.OutputMain.Length != 0)
                        {
                            var scdPath = Path.ChangeExtension(rdtPath, "main.scd");
                            File.WriteAllBytes(scdPath, scdAssembler.OutputMain);
                        }
                    }
                    else
                    {
                        foreach (var error in scdAssembler.Errors.Errors)
                        {
                            Console.WriteLine($"{error.Path}({error.Line + 1},{error.Column + 1}): error {error.ErrorCodeString}: {error.Message}");
                        }
                    }
                }
                else
                {
                    var rdtFile = new RdtFile(rdtPath);
                    if (paths.Length >= 2)
                    {
                        var inPath = paths[1];
                        if (inPath.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                        {

                        }
                        else if (inPath.EndsWith(".s", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = File.ReadAllText(inPath);
                            var scdAssembler = new ScdAssembler();
                            var result = scdAssembler.Assemble(rdtPath, s);
                            if (result == 0)
                            {
                                if (scdAssembler.OutputInit.Length != 0)
                                {
                                    rdtFile.SetScd(BioScriptKind.Init, scdAssembler.OutputInit);
                                }
                                if (scdAssembler.OutputMain.Length != 0)
                                {
                                    rdtFile.SetScd(BioScriptKind.Main, scdAssembler.OutputMain);
                                }
                            }
                            else
                            {
                                foreach (var error in scdAssembler.Errors.Errors)
                                {
                                    Console.WriteLine(error);
                                }
                            }
                        }
                    }
                    else
                    {

                        var initScdPath = GetOption(args, "--init");
                        var mainScdPath = GetOption(args, "--main");
                        if (initScdPath != null)
                        {
                            var data = File.ReadAllBytes(initScdPath);
                            rdtFile.SetScd(BioScriptKind.Init, data);
                        }
                        if (mainScdPath != null)
                        {
                            var data = File.ReadAllBytes(mainScdPath);
                            rdtFile.SetScd(BioScriptKind.Main, data);
                        }
                    }

                    var outPath = GetOption(args, "-o");
                    if (outPath != null)
                    {
                        rdtFile.Save(outPath);
                    }
                    else
                    {
                        rdtFile.Save(rdtPath + ".patched");
                    }
                }
                return 0;
            }
        }

        private static string Diassemble(BioVersion version, BioScriptKind kind, byte[] scd, bool listing = false)
        {
            var scdReader = new ScdReader();
            return scdReader.Diassemble(scd, version, kind, listing);
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
            Console.WriteLine("Resident Evil SCD assembler / diassembler");
            Console.WriteLine("usage: scd -x <rdt>");
            Console.WriteLine("       scd -d <rdt | scd>");
            Console.WriteLine("       scd [-o <rdt>] <rdt> [s] | [--init <.scd | .s>] [--main <scd | s>]");
            return 1;
        }
    }
}
