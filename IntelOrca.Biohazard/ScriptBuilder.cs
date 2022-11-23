using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard
{
    internal class ScriptBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private Dictionary<int, int> _offsetToPosition = new Dictionary<int, int>();
        private List<int> _labelQueue = new List<int>();
        private int _linePosition;
        private int _indent;
        private int _indentAdjust = 4;
        private int _lineLength;

        public bool AssemblyFormat { get; set; }
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

        public void WriteLabel(int offset)
        {
            if (_lineLength != 0)
                WriteLine();
            var oldIndent = _indent;
            _indent = 0;
            WriteLine("// " + GetLabelName(offset) + ":");
            _indent = oldIndent;
        }

        public void Write(string s)
        {
            WriteIndent();
            _sb.Append(s);
            _lineLength += s.Length;
        }

        public void MoveToColumn(int index)
        {
            var spacesToAdd = index - _lineLength;
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
            while (IsTrailingSpace(_sb[_sb.Length - 1]))
                _sb.Remove(_sb.Length - 1, 1);

            _sb.AppendLine();
            _lineLength = 0;
            _linePosition = _sb.Length;
        }

        public void WriteLine(string s)
        {
            Write(s);
            WriteLine();
        }

        private void WriteIndent()
        {
            if (_lineLength == 0)
            {
                _sb.Append(' ', _indent);
                _lineLength += _indent;
            }
        }

        public void RecordOpcode(int offset, Span<byte> opcodeBytes)
        {
            CurrentOffset = offset;
            CurrentOpcodeBytes = opcodeBytes.ToArray();

            _offsetToPosition.Add(offset, _linePosition);

            var idx = _labelQueue.IndexOf(offset);
            if (idx != -1)
            {
                _labelQueue.RemoveAt(idx);
                InsertLabel(offset);
            }
        }

        public void InsertLabel(int offset)
        {
            if (_offsetToPosition.TryGetValue(offset, out var sbPosition))
            {
                _sb.Insert(sbPosition, "\n" + GetLabelName(offset) + ":\n");
                _offsetToPosition.Remove(offset);
            }
            else
            {
                _labelQueue.Add(offset);
            }
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
            if (AssemblyFormat)
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

            Write(name);
            if (AssemblyFormat)
                MoveToColumn(asmColumn + 16);
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

        public string GetLabelName(int offset)
        {
            return $"off_{offset:X4}";
        }

        public override string ToString() => _sb.ToString();
    }
}
