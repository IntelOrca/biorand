using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard
{
    internal class RdtFile
    {
        private readonly int[] _offsets;

        public BioVersion Version { get; }
        public byte[] Data { get; }
        public ulong Checksum { get; }

        public RdtFile(string path, BioVersion version)
        {
            Version = version;
            Data = File.ReadAllBytes(path);
            _offsets = ReadHeader();
            Checksum = Data.CalculateFnv1a();
        }

        public MemoryStream GetStream()
        {
            return new MemoryStream(Data);
        }

        private int[] ReadHeader()
        {
            if (Data.Length <= 8)
                return new int[0];

            var br = new BinaryReader(new MemoryStream(Data));
            if (Version == BioVersion.Biohazard1)
            {
                br.ReadBytes(12);
                br.ReadBytes(20 * 3);

                var offsets = new int[19];
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                return offsets;
            }
            else
            {
                br.ReadBytes(8);

                var offsets = new int[23];
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                return offsets;
            }
        }

        public void ReadScript(BioScriptVisitor visitor)
        {
            visitor.VisitVersion(BioVersion.Biohazard1);
            if (Version == BioVersion.Biohazard1)
            {
                ReadScript1(BioScriptKind.Init, visitor, 6);
                ReadScript1(BioScriptKind.Main, visitor, 7);
            }
            else
            {
                ReadScript2(BioScriptKind.Init, visitor, 16);
                ReadScript2(BioScriptKind.Main, visitor, 17);
            }
        }

        private void ReadScript1(BioScriptKind kind, BioScriptVisitor visitor, int offsetIndex)
        {
            var scriptOffset = _offsets[offsetIndex];
            if (scriptOffset == 0)
                return;

            var stream = new MemoryStream(Data);
            stream.Position = scriptOffset;

            var br = new BinaryReader(stream);
            var scriptEnd = scriptOffset + br.ReadUInt16();

            visitor.VisitBeginScript(kind);
            visitor.VisitBeginSubroutine(0);
            try
            {
                while (stream.Position < scriptEnd)
                {
                    var instructionPosition = (int)stream.Position;
                    var opcode = br.ReadByte();
                    var instructionSize = _instructionSizes1[opcode];

                    var bytes = new byte[instructionSize];
                    bytes[0] = opcode;
                    if (br.Read(bytes, 1, instructionSize - 1) != instructionSize - 1)
                        break;

                    visitor.VisitOpcode(instructionPosition, new Span<byte>(bytes));
                }
            }
            catch (Exception ex)
            {
            }
            visitor.VisitEndSubroutine(0);
            visitor.VisitEndScript(kind);
        }

        private void ReadScript2(BioScriptKind kind, BioScriptVisitor visitor, int offsetIndex)
        {
            var scriptOffset = _offsets[offsetIndex];
            if (scriptOffset == 0)
                return;

            var stream = new MemoryStream(Data);
            stream.Position = scriptOffset;
            var len = _offsets[offsetIndex + 1] - scriptOffset;
            ReadScript2(kind, visitor, new BinaryReader(stream), len);
        }

        private static void ReadScript2(BioScriptKind kind, BioScriptVisitor visitor, BinaryReader br, int length)
        {
            var stream = br.BaseStream;

            visitor.VisitVersion(BioVersion.Biohazard2);
            visitor.VisitBeginScript(kind);

            var start = (int)stream.Position;
            var functionOffsets = new List<int>();
            var firstFunctionOffset = br.ReadUInt16();
            functionOffsets.Add(start + firstFunctionOffset);
            var numFunctions = firstFunctionOffset / 2;
            for (int i = 1; i < numFunctions; i++)
            {
                functionOffsets.Add(start + br.ReadUInt16());
            }
            functionOffsets.Add(start + length);
            for (int i = 0; i < numFunctions; i++)
            {
                visitor.VisitBeginSubroutine(i);

                var functionOffset = functionOffsets[i];
                var functionEnd = functionOffsets[i + 1];
                var functionEndMin = functionOffset;
                var ifStack = 0;
                stream.Position = functionOffset;
                while (stream.Position < functionEnd)
                {
                    var instructionPosition = (int)stream.Position;
                    var opcode = br.ReadByte();
                    if (i == numFunctions - 1 && opcode >= _instructionSizes2.Length)
                    {
                        break;
                    }
                    var instructionSize = _instructionSizes2[opcode];

                    var opcodeBytes = new byte[instructionSize];
                    opcodeBytes[0] = opcode;
                    if (br.Read(opcodeBytes, 1, instructionSize - 1) != instructionSize - 1)
                        throw new Exception("Unable to read opcode");

                    visitor.VisitOpcode(instructionPosition, opcodeBytes);

                    if (i == numFunctions - 1)
                    {
                        var isEnd = false;
                        switch ((OpcodeV2)opcode)
                        {
                            case OpcodeV2.EvtEnd:
                                if (instructionPosition >= functionEndMin && ifStack == 0)
                                    isEnd = true;
                                break;
                            case OpcodeV2.IfelCk:
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(opcodeBytes, 2);
                                ifStack++;
                                break;
                            case OpcodeV2.ElseCk:
                                ifStack--;
                                functionEndMin = instructionPosition + BitConverter.ToUInt16(opcodeBytes, 2);
                                break;
                            case OpcodeV2.EndIf:
                                ifStack--;
                                break;
                        }
                        if (isEnd)
                            break;
                    }
                }

                visitor.VisitEndSubroutine(i);
            }

            visitor.VisitEndScript(kind);
        }

        private static int[] _instructionSizes1 = new int[]
        {
            2, 2, 2, 2, 4, 4, 4, 6, 4, 2, 2, 4, 26, 18, 2, 8,
            2, 2, 10, 4, 4, 2, 2, 10, 26, 4, 2, 22, 6, 2, 4, 28,
            14, 14, 4, 2, 4, 4, 0, 2, 4 + 0, 2, 12, 4, 2, 4, 0, 4,
            12, 4, 4, 4 + 0, 8, 4, 4, 4, 4, 2, 4, 6, 6, 12, 2, 6,
            16, 4, 4, 4, 2, 2, 44 + 0, 14, 2, 2, 2, 2, 4, 2, 4, 2,
            2
        };

        private static int[] _instructionSizes2 = new int[]
        {
            1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
            2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
            1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
            1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
            8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
            4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
            14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
            1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
            2, 3, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24
        };
    }
}
