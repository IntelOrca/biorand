using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
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

            if (args.Contains("-x"))
            {
                var rdtFile = new RdtFile(rdtPath, BioVersion.Biohazard1);
                File.WriteAllBytes("init.scd", rdtFile.GetScd(BioScriptKind.Init));
                File.WriteAllBytes("main.scd", rdtFile.GetScd(BioScriptKind.Main));
                for (int i = 0; i < rdtFile.EventScriptCount; i++)
                {
                    File.WriteAllBytes($"event_{i:X2}.scd", rdtFile.GetScd(BioScriptKind.Main));
                }
                return 0;
            }
            else if (args.Contains("-d"))
            {
                if (rdtPath.EndsWith(".rdt", StringComparison.OrdinalIgnoreCase))
                {
                    var rdtFile = new RdtFile(rdtPath, BioVersion.Biohazard1);

                    var sb = new StringBuilder();
                    sb.AppendLine(".version 1");
                    sb.AppendLine(Diassemble(BioScriptKind.Init, rdtFile.GetScd(BioScriptKind.Init)));
                    sb.AppendLine(Diassemble(BioScriptKind.Main, rdtFile.GetScd(BioScriptKind.Main)));
                    for (int i = 0; i < rdtFile.EventScriptCount; i++)
                    {
                        sb.AppendLine(Diassemble(BioScriptKind.Event, rdtFile.GetScd(BioScriptKind.Event, i)));
                    }
                    File.WriteAllText(Path.ChangeExtension(Path.GetFileName(rdtPath), ".s"), sb.ToString());
                }
                else if (rdtPath.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                {
                    var scd = File.ReadAllBytes(rdtPath);
                    var s = Diassemble(BioScriptKind.Init, scd);
                    var sPath = Path.ChangeExtension(rdtPath, ".s");
                    File.WriteAllText(sPath, s);
                    var lst = Diassemble(BioScriptKind.Init, scd, listing: true);
                    var lstPath = Path.ChangeExtension(rdtPath, ".lst");
                    File.WriteAllText(lstPath, lst);
                }
                return 0;
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
                        if (scdAssembler.OutputInit != null)
                        {
                            var scdPath = Path.ChangeExtension(rdtPath, "init.scd");
                            File.WriteAllBytes(scdPath, scdAssembler.OutputInit);
                        }
                        if (scdAssembler.OutputMain != null)
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
                    var rdtFile = new RdtFile(rdtPath, BioVersion.Biohazard1);
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
                                if (scdAssembler.OutputInit != null)
                                {
                                    rdtFile.SetScd(BioScriptKind.Init, scdAssembler.OutputInit);
                                }
                                if (scdAssembler.OutputMain != null)
                                {
                                    rdtFile.SetScd(BioScriptKind.Main, scdAssembler.OutputMain);
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

        private static string Diassemble(BioScriptKind kind, byte[] scd, bool listing = false)
        {
            var scdReader = new ScdReader();
            return scdReader.Diassemble(scd, BioVersion.Biohazard1, kind, listing);
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
