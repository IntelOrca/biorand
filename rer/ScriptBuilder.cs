using System;
using System.Collections.Generic;
using System.Text;

namespace rer
{
    internal class ScriptBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private Dictionary<int, int> _offsetToPosition = new Dictionary<int, int>();
        private int _linePosition;
        private int _indent;
        private int _indentAdjust = 4;
        private int _lineLength;

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
            _sb.Append(s);
            _lineLength += s.Length;
        }

        public void WriteLine()
        {
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

        public void RecordOffset(int offset)
        {
            _offsetToPosition.Add(offset, _linePosition);
        }

        public void InsertLabel(int offset, string line)
        {
            if (_offsetToPosition.TryGetValue(offset, out var sbPosition))
            {
                _sb.Insert(sbPosition, line + "\n");
                _offsetToPosition.Remove(offset);
            }
        }

        public override string ToString() => _sb.ToString();
    }
}
