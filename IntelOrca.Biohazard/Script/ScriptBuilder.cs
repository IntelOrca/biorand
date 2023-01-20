using System;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.Script
{
    internal class ScriptBuilder
    {
        private readonly List<Line> _lines = new List<Line>();
        private readonly StringBuilder _line = new StringBuilder();
        private readonly SortedSet<int> _labelOffsets = new SortedSet<int>();
        private int _indent;
        private int _indentAdjust = 4;

        public bool AssemblyFormat { get; set; }
        public bool ListingFormat { get; set; }
        public int CurrentOffset { get; set; }
        public byte[] CurrentOpcodeBytes { get; set; } = new byte[0];

        public void ResetIndent()
        {
            _indent = 0;
        }

        public void Unindent()
        {
            _indent = Math.Max(0, _indent - _indentAdjust);
        }

        public void Indent()
        {
            _indent += _indentAdjust;
        }

        public void Write(string s)
        {
            WriteIndent();
            _line.Append(s);
        }

        public void MoveToColumn(int index)
        {
            var spacesToAdd = index - _line.Length;
            if (spacesToAdd > 0)
            {
                Write(new string(' ', spacesToAdd));
            }
        }

        private static bool IsTrailingSpace(char c)
        {
            if (c == '\n' || c == '\r')
                return false;
            return char.IsWhiteSpace(c);
        }

        public void WriteLine()
        {
            if (_line.Length != 0)
            {
                while (IsTrailingSpace(_line[_line.Length - 1]))
                {
                    _line.Remove(_line.Length - 1, 1);
                }
            }

            _lines.Add(new Line(CurrentOffset, CurrentOpcodeBytes.Length, _line.ToString()));
            _line.Clear();

            CurrentOffset = 0;
            CurrentOpcodeBytes = Array.Empty<byte>();
        }

        public void WriteLine(string s)
        {
            Write(s);
            WriteLine();
        }

        private void WriteIndent()
        {
            if (_line.Length == 0)
            {
                _line.Append(' ', _indent);
            }
        }

        public void RecordOpcode(int offset, Span<byte> opcodeBytes)
        {
            CurrentOffset = offset;
            CurrentOpcodeBytes = opcodeBytes.ToArray();
        }

        public void InsertLabel(int offset)
        {
            _labelOffsets.Add(offset);
        }

        public void OpenBlock()
        {
            WriteLine("{");
            Indent();
        }

        public void CloseBlock()
        {
            Unindent();
            WriteLine("}");
        }

        public void WriteStandardOpcode(string name, params object[] args)
        {
            var asmColumn = 96;
            if (AssemblyFormat && ListingFormat)
            {
                Write($"{CurrentOffset:X4}");
                Write(":");
                MoveToColumn(8);

                foreach (var b in CurrentOpcodeBytes)
                {
                    Write($"{b:X2}");
                }

                MoveToColumn(asmColumn);
            }
            else if (AssemblyFormat)
            {
                asmColumn = 4;
                MoveToColumn(asmColumn);
            }

            Write(name);
            if (AssemblyFormat)
                MoveToColumn(asmColumn + 24);
            else
                Write("(");
            for (int i = 0; i < args.Length; i++)
            {
                Write(args[i].ToString());
                if (i != args.Length - 1)
                    Write(", ");
            }
            if (!AssemblyFormat)
                Write(");");
            WriteLine();
        }

        public void WriteStandardExpression(string name, params object[] args)
        {
            Write(name);
            Write("(");
            for (int i = 0; i < args.Length; i++)
            {
                Write(args[i].ToString());
                if (i != args.Length - 1)
                    Write(", ");
            }
            Write(")");
        }

        public string GetLabelName(int offset)
        {
            return $"off_{offset:X4}";
        }

        public override string ToString()
        {
            var offsetMap = new Dictionary<int, int>();
            var sb = new StringBuilder();
            foreach (var line in _lines)
            {
                if (line.Offset != 0)
                {
                    var labelRequired = false;
                    var offsets = _labelOffsets.GetViewBetween(line.Offset, line.EndOffset - 1);
                    foreach (var offset in offsets)
                    {
                        labelRequired = true;
                        if (offset != line.Offset)
                        {
                            offsetMap[offset] = line.Offset;
                        }
                    }
                    if (labelRequired)
                    {
                        sb.Append('\n');
                        sb.Append(GetLabelName(line.Offset));
                        sb.Append(':');
                        sb.Append('\n');
                    }
                }

                sb.Append(line.Text);
                sb.Append('\n');
            }
            foreach (var kvp in offsetMap)
            {
                var oldName = GetLabelName(kvp.Key);
                var newName = GetLabelName(kvp.Value);
                var delta = kvp.Key - kvp.Value;
                if (delta > 0)
                    newName += " + " + delta;
                else
                    newName += " - " + -delta;
                sb.Replace(oldName, newName);
            }
            return sb.ToString();
        }

        private struct Line
        {
            public int Offset { get; }
            public int Length { get; }
            public string Text { get; }

            public int EndOffset => Offset + Length;

            public Line(int offset, int length, string text)
            {
                Offset = offset;
                Length = length;
                Text = text;
            }

            public override string ToString() => Text;
        }
    }
}
