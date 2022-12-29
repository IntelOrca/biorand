using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IntelOrca.Biohazard.Script
{
    public class ScdAssembler
    {
        private const byte UnkOpcode = 255;

        private const int OperandStateNone = 0;
        private const int OperandStateValue = 1;
        private const int OperandStateOr = 2;
        private const int OperandStateAdd = 3;
        private const int OperandStateSubtract = 4;

        private IConstantTable _constantTable = new Bio1ConstantTable();

        private ParserState _state;
        private ParserState _restoreState;
        private List<string> _procNames = new List<string>();
        private List<(string, int)> _labels = new List<(string, int)>();
        private List<(int, int, Token)> _labelReferences = new List<(int, int, Token)>();
        private List<byte> _procData = new List<byte>();
        private List<byte[]> _procedures = new List<byte[]>();
        private byte _currentOpcode;
        private string _currentOpcodeSignature = "";
        private int _signatureIndex;
        private BioVersion? _version;
        private BioScriptKind? _currScriptKind;
        private BioScriptKind? _lastScriptKind;
        private int _operandState;
        private int _operandValue;

        public ErrorList Errors { get; } = new ErrorList();
        public byte[] OutputInit { get; private set; } = new byte[0];
        public byte[] OutputMain { get; private set; } = new byte[0];

        public int Assemble(string path, string script)
        {
            var lexer = new Lexer(Errors);
            var tokens = lexer.ParseAllTokens(path, script);
            if (Errors.Count != 0)
                return 1;

            _state = ParserState.Default;
            foreach (var token in tokens)
            {
                if (_state == ParserState.Terminate)
                {
                    break;
                }
                else if (_state == ParserState.SkipToNextLine)
                {
                    if (token.Kind != TokenKind.NewLine)
                    {
                        continue;
                    }
                    _state = _restoreState;
                }
                ProcessToken(in token);
            }
            EndScript();

            if (Errors.Count == 0 && _version == null)
            {
                Errors.AddError(path, 0, 0, ErrorCodes.ExpectedScdVersionNumber, ErrorCodes.GetMessage(ErrorCodes.ExpectedScdVersionNumber));
            }
            return Errors.Count == 0 ? 0 : 1;
        }

        private void ProcessToken(in Token token)
        {
            if (token.Kind == TokenKind.Whitespace)
                return;

            switch (_state)
            {
                case ParserState.Default:
                    if (token.Kind == TokenKind.Directive)
                    {
                        ProcessDirective(in token);
                    }
                    break;
                case ParserState.ExpectVersion:
                    if (token.Kind != TokenKind.Number)
                    {
                        EmitError(in token, ErrorCodes.ExpectedScdVersionNumber);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.Default;
                    }
                    else
                    {
                        if (!int.TryParse(token.Text, out var num) || (num != 1 && num != 2))
                        {
                            EmitError(in token, ErrorCodes.InvalidScdVersionNumber);
                            _state = ParserState.SkipToNextLine;
                            _restoreState = ParserState.Default;
                        }
                        else
                        {
                            _version = num == 1 ? BioVersion.Biohazard1 : BioVersion.Biohazard2;
                            _state = num == 1 ? ParserState.ExpectOpcode : ParserState.Default;
                            if (_version == BioVersion.Biohazard1)
                                _constantTable = new Bio1ConstantTable();
                            else
                                _constantTable = new Bio2ConstantTable();
                        }
                    }
                    break;
                case ParserState.ExpectProcName:
                    if (token.Kind != TokenKind.Symbol)
                    {
                        EmitError(in token, ErrorCodes.ExpectedProcedureName);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.Default;
                    }
                    else
                    {
                        _procNames.Add(token.Text);
                        _state = ParserState.ExpectOpcode;
                    }
                    break;
                case ParserState.ExpectOpcode:
                    if (token.Kind == TokenKind.Directive)
                    {
                        ProcessDirective(in token);
                    }
                    else if (token.Kind == TokenKind.Label)
                    {
                        if (!AddLabel(token.Text.Substring(0, token.Text.Length - 1)))
                        {
                            EmitError(in token, ErrorCodes.LabelAlreadyDefined, token.Text);
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
                            EmitError(in token, ErrorCodes.UnknownOpcode, token.Text);
                            _state = ParserState.SkipToNextLine;
                            _restoreState = ParserState.ExpectOpcode;
                        }
                        break;
                    }
                    else if (!TokenIsEndOfLine(token))
                    {
                        EmitError(in token, ErrorCodes.ExpectedOpcode);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.ExpectOpcode;
                    }
                    break;
                case ParserState.ExpectOperand:
                    if (token.Kind == TokenKind.Number)
                    {
                        AddOperandNumber(token);
                        _state = ParserState.ExpectCommaOrOperator;
                    }
                    else if (token.Kind == TokenKind.Symbol)
                    {
                        AddOperandSymbol(token);
                        _state = ParserState.ExpectCommaOrOperator;
                    }
                    else if (TokenIsEndOfLine(token))
                    {
                        EndCurrentOpcode(in token);
                        _state = ParserState.ExpectOpcode;
                    }
                    break;
                case ParserState.ExpectCommaOrOperator:
                    if (token.Kind == TokenKind.Add)
                    {
                        _operandState = OperandStateAdd;
                        _state = ParserState.ExpectOperand;
                    }
                    else if (token.Kind == TokenKind.Subtract)
                    {
                        _operandState = OperandStateSubtract;
                        _state = ParserState.ExpectOperand;
                    }
                    else if (token.Kind == TokenKind.BitwiseOr)
                    {
                        _operandState = OperandStateOr;
                        _state = ParserState.ExpectOperand;
                    }
                    else if (token.Kind == TokenKind.Comma)
                    {
                        EndOperand();
                        _state = ParserState.ExpectOperand;
                    }
                    else if (TokenIsEndOfLine(token))
                    {
                        EndCurrentOpcode(in token);
                        _state = ParserState.ExpectOpcode;
                    }
                    else
                    {
                        EmitError(in token, ErrorCodes.ExpectedComma);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.ExpectOpcode;
                    }
                    break;
            }
        }

        private void ProcessDirective(in Token token)
        {
            switch (token.Text)
            {
                case ".version":
                    if (_version != null)
                    {
                        EmitError(in token, ErrorCodes.ScdVersionAlreadySpecified);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.Default;
                    }
                    _state = ParserState.ExpectVersion;
                    break;
                case ".init":
                    if (_currScriptKind == BioScriptKind.Init ||
                        _lastScriptKind == BioScriptKind.Init)
                    {
                        EmitError(in token, ErrorCodes.ScdTypeAlreadySpecified);
                    }
                    else
                    {
                        ChangeScriptKind(BioScriptKind.Init);
                    }
                    break;
                case ".main":
                    if (_currScriptKind == BioScriptKind.Main ||
                        _lastScriptKind == BioScriptKind.Main)
                    {
                        EmitError(in token, ErrorCodes.ScdTypeAlreadySpecified);
                    }
                    else
                    {
                        ChangeScriptKind(BioScriptKind.Main);
                    }
                    break;
                case ".proc":
                    if (_version == null)
                    {
                        EmitError(in token, ErrorCodes.ScdVersionNotSpecified);
                        _state = ParserState.Terminate;
                    }
                    else if (_version == BioVersion.Biohazard1)
                    {
                        EmitError(in token, ErrorCodes.ProcedureNotValid);
                        _state = ParserState.SkipToNextLine;
                        _restoreState = ParserState.Default;
                    }
                    else
                    {
                        if (_procedures.Count != 0 || _procData.Count != 0)
                            EndProcedure();
                        _state = ParserState.ExpectProcName;
                    }
                    break;
                default:
                    EmitError(in token, ErrorCodes.UnknownDirective, token.Text);
                    _restoreState = _state;
                    _state = ParserState.SkipToNextLine;
                    break;
            }
        }

        private void BeginScript()
        {
            _procedures.Clear();
            _procNames.Clear();
            _labels.Clear();
            _labelReferences.Clear();
            _procData.Clear();
            if (_version == BioVersion.Biohazard1)
            {
                _procData.Add(0);
                _procData.Add(0);
            }
        }

        private void EndScript()
        {
            if (_currScriptKind == null)
                return;

            if (_version == BioVersion.Biohazard1)
            {
                FixLabelReferences();
            }
            if (Errors.Count == 0)
            {
                if (_version == BioVersion.Biohazard1)
                {
                    var procLength = _procData.Count;
                    if (procLength <= 2)
                    {
                        // Empty script
                        procLength = 0;
                    }

                    _procData[0] = (byte)(procLength & 0xFF);
                    _procData[1] = (byte)(procLength >> 8);
                    _procData.Add(0);
                    while ((_procData.Count & 3) != 0)
                    {
                        _procData.Add(0);
                    }
                }
                else
                {
                    EndProcedure();

                    var offset = _procedures.Count * 2;
                    foreach (var p in _procedures)
                    {
                        WriteUint16((ushort)offset);
                        offset += p.Length;
                    }
                    foreach (var p in _procedures)
                    {
                        _procData.AddRange(p);
                    }
                }

                var output = _procData.ToArray();
                if (_currScriptKind == BioScriptKind.Main)
                    OutputMain = output;
                else
                    OutputInit = output;
            }
        }

        private void ChangeScriptKind(BioScriptKind kind)
        {
            EndScript();
            _lastScriptKind = _currScriptKind;
            _currScriptKind = kind;
            BeginScript();
        }

        private void EndProcedure()
        {
            FixLabelReferences();
            _procedures.Add(_procData.ToArray());
            _labels.Clear();
            _labelReferences.Clear();
            _procData.Clear();
        }

        private bool AddLabel(string name)
        {
            if (_labels.Any(x => x.Item1 == name))
                return false;

            _labels.Add((name, _procData.Count));
            return true;
        }

        private void RecordLabelReference(int size, in Token token)
        {
            _labelReferences.Add((_procData.Count, size, token));
        }

        private bool BeginOpcode(string name)
        {
            if (_currScriptKind == null)
                _currScriptKind = BioScriptKind.Init;

            if (name == "unk")
            {
                _currentOpcode = UnkOpcode;
                return true;
            }

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
                var length = _constantTable.GetInstructionSize(opcode.Value, null);
                if (_currentOpcodeSignature != "")
                    length--;
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
            if (!CheckOperandLength(in token))
                return;

            switch (_operandState)
            {
                case OperandStateNone:
                    _operandValue = num;
                    break;
                case OperandStateValue:
                    EmitError(in token, ErrorCodes.ExpectedOperator);
                    break;
                case OperandStateOr:
                    _operandValue |= num;
                    break;
                case OperandStateAdd:
                    _operandValue += num;
                    break;
                case OperandStateSubtract:
                    _operandValue -= num;
                    break;
            }
            _operandState = OperandStateValue;
        }

        private void EndOperand()
        {
            if (_operandState == 0)
                return;

            if (_currentOpcode == UnkOpcode)
            {
                WriteUInt8((byte)_operandValue);
            }
            else
            {
                var arg = _currentOpcodeSignature[_signatureIndex];
                if (arg == 'I')
                {
                    WriteInt16((short)_operandValue);
                }
                else if (arg == 'L' || arg == 'U')
                {
                    WriteUint16((ushort)_operandValue);
                }
                else if (arg == 'r')
                {
                    var room = _operandValue & 0xFF;
                    var stage = (_operandValue >> 8) & 0xFF;
                    WriteUInt8((byte)((stage << 5) | (room & 0b11111)));
                }
                else
                {
                    WriteUInt8((byte)_operandValue);
                }
                _signatureIndex++;
            }

            _operandState = 0;
            _operandValue = 0;
        }

        private void AddOperandSymbol(in Token token)
        {
            if (!CheckOperandLength(in token))
                return;

            var arg = _currentOpcodeSignature[_signatureIndex];
            if (arg == 'l' || arg == 'L')
            {
                RecordLabelReference(arg == 'l' ? 1 : 2, in token);
                AddOperandNumber(in token, 0);
            }
            else
            {
                var value = _constantTable.GetConstantValue(token.Text);
                if (value == null)
                {
                    EmitError(in token, ErrorCodes.UnknownSymbol, token.Text);
                    AddOperandNumber(in token, 0);
                }
                else
                {
                    AddOperandNumber(in token, value.Value);
                }
            }
        }

        private bool CheckOperandLength(in Token token)
        {
            if (_currentOpcode != UnkOpcode && _signatureIndex > _currentOpcodeSignature.Length)
            {
                EmitError(in token, ErrorCodes.TooManyOperands);
                return false;
            }
            return true;
        }

        private bool EndCurrentOpcode(in Token token)
        {
            EndOperand();
            if (_currentOpcode != UnkOpcode && _signatureIndex != _currentOpcodeSignature.Length)
            {
                EmitError(in token, ErrorCodes.IncorrectNumberOfOperands);
                return false;
            }
            return true;
        }

        private void FixLabelReferences()
        {
            foreach (var (offset, size, t) in _labelReferences)
            {
                var labelIndex = _labels.FindIndex(x => x.Item1 == t.Text);
                if (labelIndex == -1)
                {
                    EmitError(in t, ErrorCodes.UnknownLabel, t.Text);
                }
                else
                {
                    var labelAddress = _labels[labelIndex].Item2;
                    var value = labelAddress - offset + size;
                    // TODO Check range
                    if (size == 1)
                    {
                        _procData[offset] = (byte)(_procData[offset] + value);
                    }
                    else
                    {
                        var customOffset = (short)(_procData[offset + 0] | (_procData[offset + 1] << 8));
                        var updatedValue = value + customOffset;
                        _procData[offset + 0] = (byte)(updatedValue & 0xFF);
                        _procData[offset + 1] = (byte)((updatedValue >> 8) & 0xFF);
                    }
                }
            }
        }

        private static bool TokenIsEndOfLine(in Token token)
        {
            return token.Kind == TokenKind.EOF || token.Kind == TokenKind.Comment || token.Kind == TokenKind.NewLine;
        }

        private void EmitError(in Token token, int code, params object[] args)
        {
            Errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
        }

        private void EmitWarning(in Token token, int code, params object[] args)
        {
            Errors.AddWarning(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
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

        private void WriteUint16(ushort s)
        {
            _procData.Add((byte)(s & 0xFF));
            _procData.Add((byte)(s >> 8));
        }

        private enum ParserState
        {
            Default,
            Terminate,
            SkipToNextLine,
            ExpectVersion,
            ExpectProcName,
            ExpectOpcode,
            ExpectOperand,
            ExpectCommaOrOperator,
        }

        private class Lexer
        {
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

            private void EmitError(in Token token, int code, params object[] args)
            {
                Errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
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
                        EmitError(in token, ErrorCodes.InvalidOperator, token.Text);
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
                {
                    var length = _sIndex - _offset;
                    if (length == 1)
                    {
                        var ch = GetLastReadChar();
                        if (ch == ',')
                            return CreateToken(TokenKind.Comma);
                        else if (ch == '+')
                            return CreateToken(TokenKind.Add);
                        else if (ch == '-')
                            return CreateToken(TokenKind.Subtract);
                        else if (ch == '|')
                            return CreateToken(TokenKind.BitwiseOr);
                    }
                    return CreateToken(TokenKind.Unknown);
                }
                throw new Exception();
            }

            private char GetLastReadChar()
            {
                return _s[_sIndex - 1];
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
                if (c != '\n' && c != '\r')
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
            Unknown,
            Whitespace,
            NewLine,
            Comment,
            Number,
            Symbol,
            Label,
            Comma,
            Add,
            Subtract,
            BitwiseOr,
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

        public class ErrorCodes
        {
            public const int ScdVersionNotSpecified = 1;
            public const int OpcodeNotInProcedure = 2;
            public const int LabelAlreadyDefined = 3;
            public const int UnknownOpcode = 4;
            public const int ExpectedOpcode = 5;
            public const int ExpectedComma = 6;
            public const int UnknownSymbol = 7;
            public const int TooManyOperands = 8;
            public const int IncorrectNumberOfOperands = 9;
            public const int UnknownLabel = 10;
            public const int InvalidOperator = 11;
            public const int ExpectedProcedureName = 12;
            public const int ExpectedScdVersionNumber = 13;
            public const int InvalidScdVersionNumber = 14;
            public const int ScdVersionAlreadySpecified = 15;
            public const int ProcedureNotValid = 16;
            public const int ScdTypeAlreadySpecified = 17;
            public const int UnknownDirective = 18;
            public const int ExpectedOperator = 19;

            public static string GetMessage(int code) => _messages[code];

            private static readonly string[] _messages = new string[]
            {
                "",
                "SCD version must be specified before any procedure or opcode.",
                "Opcode must be inside a procedure.",
                "'{0}' has already been defined as a label.",
                "'{0}' it not a valid opcode.",
                "Expected opcode.",
                "Expected , after opcode.",
                "'{0}' has not been defined as a constant.",
                "Too many operands for this opcode.",
                "Incorrect number of operands for opcode.",
                "'{0}' has not been defined as a label within the same procedure.",
                "'{0}' is not a known or valid operator.",
                "Expected procedure name.",
                "Expected SCD version number.",
                "Invalid SCD version number. Only version 1 and 2 are supported.",
                "SCD version already specified.",
                "Procedures are not valid in SCD version 1.",
                "SCD type already specified.",
                "'{0}' is not a valid directive.",
                "Expected operator.",
            };
        }
    }
}
