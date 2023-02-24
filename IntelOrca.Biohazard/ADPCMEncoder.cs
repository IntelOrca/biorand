using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    /// <summary>
    /// Encodes 16-bit PCM to .VB ADPCM encoded sound data.
    /// </summary>
    /// <remarks>
    /// Based on https://github.com/Aikku93/wav2vag/.
    /// </remarks>
    public unsafe class ADPCMEncoder
    {
        private const int SPUADPCM_FRAME_LEN = 28;

        public byte[] Encode(ReadOnlySpan<short> src, int loopBeg = -1, int loopEnd = -1)
        {
            var appendSilentLoop = (loopBeg == -1 /* || LoopEnd == -1 */);
            var nSamples = src.Length;
            var nFrames = nSamples / SPUADPCM_FRAME_LEN;
            var nTotalFrames = nFrames;
            if (appendSilentLoop)
                nTotalFrames++;

            var data = new byte[nTotalFrames * 16];
            var outFrames = MemoryMarshal.Cast<byte, SPUADPCMFrame>(data);
            var lpcTap = new int[2];
            for (var frame = 0; frame < nFrames; frame++)
            {
                //! NOTE: Flags are ORed in for the case of a one-frame loop.
                var frameData = SPUADPCM_Compress(src, lpcTap);
                if (frame * SPUADPCM_FRAME_LEN == loopBeg)
                    frameData.u8[1] |= 0x04; //! LOOP_START
                if ((frame + 1) * SPUADPCM_FRAME_LEN == loopEnd)
                    frameData.u8[1] |= 0x03; //! LOOP_END_REPT
                outFrames[frame] = frameData;
                src = src.Slice(SPUADPCM_FRAME_LEN);
            }

            if (appendSilentLoop)
            {
                //! Append silent loop for one-shot samples
                outFrames[outFrames.Length - 1].u8[1] = 0x05; //! LOOP_START|LOOP_END_MUTE
            }

            return data;
        }

        private static SPUADPCMFrame SPUADPCM_Compress(ReadOnlySpan<short> src, int[] lpcTap)
        {
            var lpc = new int[5, 2];
            var bestTap0 = 0;
            var bestTap1 = 0;
            var bestError = ulong.MaxValue;
            SPUADPCMFrame bestFrame;
            for (var filter = 0; filter < 5; filter++)
            {
                for (var shift = 0; shift <= 12; shift++)
                {
                    var tap0 = lpcTap[0];
                    var tap1 = lpcTap[1];
                    var thisError = 0UL;
                    SPUADPCMFrame thisFrame;
                    thisFrame.u32[0] = (uint)((12 - shift) | filter << 4);
                    thisFrame.u32[1] = thisFrame.u32[2] = thisFrame.u32[3] = 0;
                    for (int n = 0; n < SPUADPCM_FRAME_LEN; n++)
                    {
                        //! LPC filters provided by SPU (.6fxp)
                        lpc[0, 0] = 0; lpc[0, 1] = 0;
                        lpc[1, 0] = 60; lpc[1, 1] = 0;
                        lpc[2, 0] = 115; lpc[2, 1] = -52;
                        lpc[3, 0] = 98; lpc[3, 1] = -55;
                        lpc[4, 0] = 122; lpc[4, 1] = -60;

                        //! Get prediction, form residue, quantize, clip, sum error, swap LPC taps
                        //! NOTE: Prediction formula is based off a common-sense approach to the
                        //! formula provided in the no$psx specification. The formula there uses
                        //! division by 64, but this is likely to be a bit-shift right instead, as
                        //! then the rounding term actually makes sense, as its the most common
                        //! approach towards rounding (even if it's off by 1 for negative values).
                        int x = src[n];
                        int p = (tap0 * lpc[filter, 0] + tap1 * lpc[filter, 1] + 32) >> 6;
                        int r = x - p;
                        int q = (r + (((1 << shift) - ((r < 0) ? 1 : 0)) >> 1)) >> shift; //! Round[r / 2^Shift]. Looks janky but it's correct
                        q = (q < -8) ? (-8) : (q > +7) ? (+7) : q;
                        int y = p + (q << shift); y = (y < -0x8000) ? (-0x8000) : (y > +0x7FFF) ? (+0x7FFF) : y;
                        int e = y - x;
                        thisFrame.u32[(n + 4) / 8] |= (uint)((q & 0xF) << (((n + 4) % 8) * 4));
                        thisError += (ulong)e * (ulong)e;
                        tap1 = tap0;
                        tap0 = y;
                    }
                    if (thisError < bestError)
                    {
                        bestTap0 = tap0;
                        bestTap1 = tap1;
                        bestError = thisError;
                        bestFrame = thisFrame;
                    }
                }
            }
            lpcTap[0] = bestTap0;
            lpcTap[1] = bestTap1;
            return bestFrame;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct SPUADPCMFrame
        {
            [FieldOffset(0)]
            public fixed byte u8[16];

            [FieldOffset(0)]
            public fixed ushort u16[8];

            [FieldOffset(0)]
            public fixed uint u32[4];
        }
    }
}
