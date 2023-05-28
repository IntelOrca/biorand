using System;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class BioString
    {
        private const string EnTable = " .___()_____0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________";
        private const byte Green = 0xF9;
        private const byte StartText = 0xFA;
        private const byte YesNoQuestion = 0xFB;
        private const byte NewLine = 0xFC;
        private const byte UnknownFD = 0xFD;
        private const byte EndText = 0xFE;

        private readonly byte[] _data;

        public BioString(Span<byte> data)
        {
            _data = data.ToArray();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var isGreen = false;
            for (var i = 0; i < _data.Length; i++)
            {
                var b = _data[i];
                switch (b)
                {
                    case Green:
                        if (!isGreen)
                            sb.Append('{');
                        else
                            sb.Append('}');
                        isGreen = !isGreen;
                        i++;
                        break;
                    case StartText:
                        i++;
                        break;
                    case YesNoQuestion:
                        i++;
                        sb.Append('@');
                        break;
                    case NewLine:
                        sb.Append('\n');
                        break;
                    case UnknownFD:
                        i++;
                        break;
                    case EndText:
                        i = _data.Length - 1;
                        break;
                    default:
                        if (b < EnTable.Length)
                        {
                            sb.Append(EnTable[b]);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
