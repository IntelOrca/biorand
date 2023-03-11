using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class PSXADPCMDecoder
    {
        private readonly static double[,] f = {
            {           0.0,   0.0 },
            {   60.0 / 64.0,   0.0 },
            {  115.0 / 64.0, -52.0 / 64.0 },
            {   98.0 / 64.0, -55.0 / 64.0 },
            {  122.0 / 64.0, -60.0 / 64.0 }
        };

        public byte[] Decode(byte[] adpcm, int sampleRate)
        {
            var waveHeader = new WaveHeader();
            waveHeader.nRiffMagic = WaveHeader.RiffMagic;
            waveHeader.nWaveMagic = WaveHeader.WaveMagic;
            waveHeader.nFormatMagic = WaveHeader.FmtMagic;
            waveHeader.nFormatLength = 16;
            waveHeader.wFormatTag = 1;
            waveHeader.nChannels = 1;
            waveHeader.nSamplesPerSec = (uint)sampleRate;
            waveHeader.wBitsPerSample = 16;
            waveHeader.nBlockAlign = (ushort)((waveHeader.wBitsPerSample * waveHeader.nChannels) / 8);
            waveHeader.nAvgBytesPerSec = waveHeader.nSamplesPerSec * waveHeader.nBlockAlign;
            waveHeader.wDataMagic = WaveHeader.DataMagic;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(waveHeader);
            Decode(adpcm, bw);

            bw.BaseStream.Position = 4;
            bw.Write((uint)(ms.Length - 36));
            bw.BaseStream.Position = 40;
            bw.Write((uint)(ms.Length - 44));

            return ms.ToArray();
        }

        private void Decode(ReadOnlySpan<byte> data, BinaryWriter bw)
        {
            int index = 0;
            var s_1 = 0.0;
            var s_2 = 0.0;
            var samples = new double[28];
            while (index < data.Length)
            {
                int d, s;
                var predict_nr = data[index++];
                var shift_factor = predict_nr & 0xF;
                predict_nr >>= 4;
                var flags = data[index++];
                if (flags == 7)
                    break;
                for (var i = 0; i < 28; i += 2)
                {
                    d = data[index++];
                    s = (d & 0x0F) << 12;
                    if ((s & 0x8000) != 0)
                        s = (int)(s | 0xFFFF0000);
                    samples[i] = s >> shift_factor;
                    s = (d & 0xF0) << 8;
                    if ((s & 0x8000) != 0)
                        s = (int)(s | 0xFFFF0000);
                    samples[i + 1] = s >> shift_factor;
                }

                for (var i = 0; i < 28; i++)
                {
                    samples[i] = samples[i] + s_1 * f[predict_nr, 0] + s_2 * f[predict_nr, 1];
                    s_2 = s_1;
                    s_1 = samples[i];
                    d = (int)(samples[i] + 0.5);
                    bw.Write((short)d);
                }
            }
        }
    }
}
