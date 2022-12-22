using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script
{
    public class ScdAssembler
    {
        private IConstantTable _constantTable = new Bio1ConstantTable();
        private ParserState _state;
        private List<string> _procNames = new List<string>();
        private List<int> _labelOffsets = new List<int>();
        private List<string> _labelNames = new List<string>();
        private List<byte> _procData = new List<byte>();
        private byte _currentOpcode;
        private string _currentOpcodeSignature = "";
        private int _signatureIndex;

        public ErrorList Errors { get; } = new ErrorList();
        public byte[] Output { get; private set; } = new byte[0];

        public int Assemble(string path, string script)
        {
            _procData.Clear();
            _procData.Add(0);
            _procData.Add(0);

            var lexer = new Lexer(Errors);
            var tokens = lexer.ParseAllTokens(path, script);
            if (Errors.Count != 0)
                return 1;

            foreach (var token in tokens)
            {
                if (_state == ParserState.SkipToNextLine)
                {
                    if (token.Kind != TokenKind.NewLine)
                    {
                        continue;
                    }
                    _state = ParserState.None;
                }
                ProcessToken(in token);
            }

            if (Errors.Count != 0)
            {
                return 1;
            }

            var procLength = _procData.Count - 2;
            _procData[0] = (byte)(procLength & 0xFF);
            _procData[1] = (byte)(procLength >> 8);
            Output = _procData.ToArray();
            return 0;
        }

        private void ProcessToken(in Token token)
        {
            if (token.Kind == TokenKind.Whitespace)
                return;

            switch (_state)
            {
                case ParserState.None:
                    if (token.Kind == TokenKind.Directive)
                    {
                        if (token.Text == ".proc")
                        {
                            _state = ParserState.ExpectProcName;
                        }
                    }
                    else if (token.Kind == TokenKind.Opcode)
                    {
                        EmitError(in token, 3, "Opcode can only appear within a procedure.");
                        _state = ParserState.SkipToNextLine;
                    }
                    break;
                case ParserState.ExpectOpcode:
                    if (token.Kind == TokenKind.Directive)
                    {
                        if (token.Text == ".proc")
                        {
                            _state = ParserState.ExpectProcName;
                        }
                    }
                    else if (token.Kind == TokenKind.Label)
                    {
                        if (!AddLabel(token.Text))
                        {
                            EmitError(in token, 7, $"'{token.Text}' has already been defined.");
                        }
                    }
                    else if (token.Kind == TokenKind.Opcode)
                    {
                        if (BeginOpcode(token.Text))
                        {
                            _state = ParserState.ExpectOperand;
                        }
                        else
                        {
                            EmitError(in token, 1, "Unknown opcode");
                            _state = ParserState.SkipToNextLine;
                        }
                        break;
                    }
                    else if (!TokenIsEndOfLine(token))
                    {
                        EmitError(in token, 4, "Expected opcode");
                        _state = ParserState.SkipToNextLine;
                    }
                    break;
                case ParserState.ExpectOperand:
                    if (token.Kind == TokenKind.Number)
                    {
                        AddOperandNumber(token);
                        _state = ParserState.ExpectComma;
                    }
                    else if (token.Kind == TokenKind.Symbol)
                    {
                        AddOperandSymbol(token);
                        _state = ParserState.ExpectComma;
                    }
                    else if (TokenIsEndOfLine(token))
                    {
                        EndCurrentOpcode(in token);
                        _state = ParserState.ExpectOpcode;
                    }
                    break;
                case ParserState.ExpectComma:
                    if (token.Kind == TokenKind.Comma)
                    {
                        _state = ParserState.ExpectOperand;
                    }
                    else if (TokenIsEndOfLine(token))
                    {
                        EndCurrentOpcode(in token);
                        _state = ParserState.ExpectOpcode;
                    }
                    else
                    {
                        EmitError(in token, 5, "Expected comma after operand.");
                        _state = ParserState.SkipToNextLine;
                    }
                    break;
                case ParserState.ExpectProcName:
                    if (token.Kind != TokenKind.Symbol)
                    {
                        EmitError(in token, 2, "Expected procedure name.");
                        _state = ParserState.SkipToNextLine;
                    }
                    else
                    {
                        _procNames.Add(token.Text);
                        _state = ParserState.ExpectOpcode;
                    }
                    break;
            }
        }

        private bool AddLabel(string name)
        {
            if (_labelNames.Contains(name))
                return false;

            _labelNames.Add(name);
            _labelOffsets.Add(_procData.Count);
            return true;
        }

        private bool BeginOpcode(string name)
        {
            var opcode = _constantTable.FindOpcode(name);
            if (opcode == null)
            {
                return false;
            }
            _currentOpcode = opcode.Value;
            _currentOpcodeSignature = _constantTable.GetOpcodeSignature(opcode.Value);
            var colonIndex = _currentOpcodeSignature.IndexOf(':');
            if (colonIndex == -1)
            {
                var length = _constantTable.GetInstructionSize(opcode.Value);
                _currentOpcodeSignature = new string('u', length);
            }
            else
            {
                _currentOpcodeSignature = _currentOpcodeSignature.Substring(colonIndex + 1);
            }
            _signatureIndex = 0;
            WriteUInt8(opcode.Value);
            return true;
        }

        private void AddOperandNumber(in Token token)
        {
            var num = int.Parse(token.Text);
            AddOperandNumber(in token, num);
        }

        private void AddOperandNumber(in Token token, int num)
        {
            if (_signatureIndex > _currentOpcodeSignature.Length)
            {
                EmitError(in token, 12, "Too many operands for this opcode.");
                return;
            }

            var arg = _currentOpcodeSignature[_signatureIndex];
            if (arg == 'I')
            {
                WriteInt16((short)num);
            }
            else
            {
                WriteUInt8((byte)num);
            }
            _signatureIndex++;
        }

        private void AddOperandSymbol(in Token token)
        {
            AddOperandNumber(in token, 0);
        }

        private bool EndCurrentOpcode(in Token token)
        {
            if (_signatureIndex != _currentOpcodeSignature.Length)
            {
                EmitError(in token, 10, "Incorrect number of operands for opcode.");
                return false;
            }
            return true;
        }

        private static bool TokenIsEndOfLine(in Token token)
        {
            return token.Kind == TokenKind.EOF || token.Kind == TokenKind.Comma || token.Kind == TokenKind.NewLine;
        }

        private void EmitError(in Token token, int code, string message)
        {
            Errors.AddError(token.Path, token.Line, token.Column, code, message);
        }

        private void EmitWarning(in Token token, int code, string message)
        {
            Errors.AddWarning(token.Path, token.Line, token.Column, code, message);
        }

        private void WriteUInt8(byte b)
        {
            _procData.Add(b);
        }

        private void WriteInt16(short s)
        {
            _procData.Add((byte)(s & 0xFF));
            _procData.Add((byte)(s >> 8));
        }

        private enum ParserState
        {
            None,
            SkipToNextLine,
            ExpectOpcode,
            ExpectProcName,
            ExpectOperand,
            ExpectComma,
        }

        private class Lexer
        {
            private readonly List<Error> _errors = new List<Error>();

            private string _path = "";
            private string _s = "";
            private int _sIndex;

            private int _offset;
            private int _line;
            private int _column;

            private bool _expectingOpcode;

            public ErrorList Errors { get; }

            public Lexer(ErrorList errors)
            {
                Errors = errors;
            }

            public Token[] ParseAllTokens(string path, string script)
            {
                _path = path;
                _s = script;
                _expectingOpcode = true;

                var tokens = new List<Token>();
                Token token;
                do
                {
                    token = ParseToken();
                    ValidateToken(in token);
                    tokens.Add(token);
                } while (token.Kind != TokenKind.EOF);
                return tokens.ToArray();
            }

            private void EmitError(in Token token, int code, string message)
            {
                Errors.AddError(_path, token.Line, token.Column, code, message);
            }

            private bool ValidateToken(in Token token)
            {
                if (token.Kind == TokenKind.Number)
                {
                }
                else if (token.Kind == TokenKind.Comma)
                {
                    if (token.Text != ",")
                    {
                        EmitError(in token, 0, "Invalid symbol");
                        return false;
                    }
                }
                return true;
            }

            private Token ParseToken()
            {
                var c = PeekChar();
                if (c == char.MinValue)
                    return new Token(TokenKind.EOF, _path, _line, _column);

                if (ParseNewLine())
                    return CreateToken(TokenKind.NewLine);
                if (ParseWhitespace())
                    return CreateToken(TokenKind.Whitespace);
                if (ParseComment())
                    return CreateToken(TokenKind.Comment);
                if (ParseDirective())
                {
                    _expectingOpcode = false;
                    return CreateToken(TokenKind.Directive);
                }
                if (ParseNumber())
                    return CreateToken(TokenKind.Number);
                if (ParseSymbol())
                {
                    if (GetLastChar() == ':')
                    {
                        return CreateToken(TokenKind.Label);
                    }
                    else
                    {
                        if (_expectingOpcode)
                        {
                            _expectingOpcode = false;
                            return CreateToken(TokenKind.Opcode);
                        }
                        else
                        {
                            return CreateToken(TokenKind.Symbol);
                        }
                    }
                }
                if (ParseOperator())
                    return CreateToken(TokenKind.Comma);
                throw new Exception();
            }

            private Token CreateToken(TokenKind kind)
            {
                var length = _sIndex - _offset;
                var token = new Token(_s, _offset, kind, _path, _line, _column, length);
                if (kind == TokenKind.NewLine)
                {
                    _line++;
                    _column = 0;
                    _expectingOpcode = true;
                }
                else
                {
                    _column += length;
                }
                _offset = _sIndex;
                return token;
            }

            private bool ParseNewLine()
            {
                var c = PeekChar();
                if (c != '\r')
                    return false;

                ReadChar();
                c = PeekChar();
                if (c == '\n')
                    ReadChar();
                return true;
            }

            private bool ParseWhitespace()
            {
                var c = PeekChar();
                if (!char.IsWhiteSpace(c))
                    return false;
                do
                {
                    ReadChar();
                    c = PeekChar();
                } while (c != char.MinValue && char.IsWhiteSpace(c));
                return true;
            }

            private bool ParseComment()
            {
                var c = PeekChar();
                if (c != ';')
                    return false;
                do
                {
                    ReadChar();
                    c = PeekChar();
                } while (c != char.MinValue && c != '\r' && c != '\n');
                return true;
            }

            private bool ParseDirective()
            {
                var c = PeekChar();
                if (c != '.')
                    return false;
                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool ParseNumber()
            {
                var c = PeekChar();
                if ((c < '0' || c > '9') && c != '-')
                    return false;

                if (c == '-')
                {
                    c = PeekChar(skip: 1);
                    if (c < '0' || c > '9')
                        return false;
                    ReadChar();
                }

                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool ParseSymbol()
            {
                if (PeekSeparator())
                    return false;

                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool ParseOperator()
            {
                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private char PeekChar(int skip = 0)
            {
                var offset = _sIndex + skip;
                if (offset >= _s.Length)
                    return char.MinValue;
                return _s[offset];
            }

            private char ReadChar()
            {
                var result = PeekChar();
                _sIndex++;
                return result;
            }

            private bool PeekSeparator()
            {
                var c = PeekChar();
                if (c == '_' || c == ':' || char.IsLetterOrDigit(c))
                    return false;
                return true;
            }

            private char GetLastChar()
            {
                if (_sIndex == 0)
                    return char.MinValue;
                return _s[_sIndex - 1];
            }
        }

        [DebuggerDisplay("{Kind} | {Text}")]
        private struct Token
        {
            private readonly string _content;
            private readonly int _offset;

            public TokenKind Kind { get; }
            public string Path { get; }
            public short Line { get; }
            public short Column { get; }
            public short Length { get; }

            public string Text => _content.Substring(_offset, Length);

            public Token(TokenKind kind, string path, int line, int column)
            {
                _content = "";
                _offset = 0;
                Kind = kind;
                Path = path;
                Line = (short)line;
                Column = (short)column;
                Length = 0;
            }

            public Token(string content, int offset, TokenKind kind, string path, int line, int column, int length)
            {
                _content = content;
                _offset = offset;
                Path = path;
                Kind = kind;
                Line = (short)line;
                Column = (short)column;
                Length = (short)length;
            }
        }

        private enum TokenKind : byte
        {
            Whitespace,
            NewLine,
            Comment,
            Number,
            Symbol,
            Label,
            Comma,
            Opcode,
            Directive,
            EOF
        }

        public class ErrorList
        {
            public List<Error> Errors { get; } = new List<Error>();

            public int Count => Errors.Count;

            public void AddError(string path, int line, int column, int code, string message)
            {
                Errors.Add(new Error(path, line, column, ErrorKind.Error, code, message));
            }

            public void AddWarning(string path, int line, int column, int code, string message)
            {
                Errors.Add(new Error(path, line, column, ErrorKind.Warning, code, message));
            }
        }

        [DebuggerDisplay("{Path}({Line},{Column}): error {ErrorCodeString}: {Message}")]
        public struct Error
        {
            public string Path { get; }
            public int Line { get; }
            public int Column { get; }
            public ErrorKind Kind { get; }
            public int Code { get; }
            public string Message { get; }

            public string ErrorCodeString => $"SCD{Code:0000}";

            public Error(string path, int line, int column, ErrorKind kind, int code, string message)
            {
                Path = path;
                Line = line;
                Column = column;
                Kind = kind;
                Code = code;
                Message = message;
            }
        }

        public enum ErrorKind
        {
            Error,
            Warning
        }
    }
}
