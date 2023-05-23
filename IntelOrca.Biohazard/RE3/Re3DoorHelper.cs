using System;
using System.Linq;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3DoorHelper : IDoorHelper
    {
        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            if (!config.RandomDoors)
                return;

            // For 30B, change boss door to id 30 so we can separate it from the normal door
            var rdt = gameData.GetRdt(new RdtId(2, 0x0B));
            if (rdt != null)
            {
                var bossDoor = rdt.Doors.Skip(1).First();
                bossDoor.Id = 30;
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            UnblockRpdDoor();
            FixRE3SalesOffice();
            FixRE3LockpickDoor();
            FixStagla();
            FixRE3Train();
            UnblockWasteDisposalDoor();
            FixRE3TowerOutdoor();
            FixNemesisFlag();
            FixWarehouseAlley();
            if (config.RandomDoors)
            {
                FixRE3HydrantAlley();
                FixRE3RestuarantFront();
                FixRE3Restuarant();
                FixRE3PressOffice();
                FixRE3TrainCrashExit();
                FixRE3PianoRoom();
                FixRE3Laboratory();
                FixRE3CommsRoom();
                FixRE3Rain();
            }
            else
            {
                FixRE3BarricadeAlley();
            }

            void UnblockRpdDoor()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x11));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                rdt.Nop(0x2CAE, 0x2CF8);
            }

            void FixRE3SalesOffice()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x1B));
                if (rdt == null)
                    return;

                // Allow remote to be used after the door has been unlocked
                rdt.Nop(0x4328);
                rdt.Nop(0x49B0);
                rdt.Nop(0x4B5A);
            }

            void FixRE3LockpickDoor()
            {
                var rdt1 = gameData.GetRdt(new RdtId(0, 0x0A));
                var rdt2 = gameData.GetRdt(new RdtId(0, 0x24));
                if (rdt1 != null && rdt2 != null)
                {
                    var lockpickDoor1 = rdt1.Doors.First(x => x.Id == 2);
                    var lockpickDoor2 = rdt2.Doors.First(x => x.Id == 2);
                    CopyDoorTo(lockpickDoor1, lockpickDoor2);
                }
            }

            void FixStagla()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0E));
                if (rdt != null)
                {
                    rdt.Nop(0x2BB6);
                }
            }

            void FixRE3BarricadeAlley()
            {
                // Force barricade alley door to go to 2nd hydrant alley
                var rdt = gameData.GetRdt(new RdtId(0, 0x08));
                if (rdt != null)
                {
                    var door = rdt.Doors.FirstOrDefault(x => x.Id == 5);
                    if (door != null)
                    {
                        door.Target = new RdtId(0, 0x23);
                    }
                }

                // Force hydrant alley save to go to 2nd hydrant alley
                rdt = gameData.GetRdt(new RdtId(0, 0x0C));
                if (rdt != null)
                {
                    rdt.Nop(0x42A, 0x452);
                }
            }

            void FixRE3HydrantAlley()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x23));
                if (rdt != null)
                {
                    // if (bits[3][28] == 1)
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x5B, 0x00 }));
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, 0x1C, 0x01 }));

                    // cut_replace([0..8], [11..19]);
                    for (int i = 0; i < 9; i++)
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x53, new byte[] { (byte)i, (byte)(i + 11) }));

                    foreach (var cut in new[] { 0, 4, 5, 7 })
                    {
                        // if (cut == 0)
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0A, 0x00 }));
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4E, new byte[] { 0x00, 0x1A, 0x00, (byte)cut, 0x00 }));
                        // cut_chg(11);
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x50, new byte[] { (byte)(cut + 11) }));
                        // endif
                        rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
                    }

                    // cut_auto(1);
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x52, new byte[] { 1 }));
                    // endif
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));

                    // NOP out old cut_replace
                    rdt.Nop(0x2DC8);
                    rdt.Nop(0x2DCB);
                    rdt.Nop(0x2DCE);
                    rdt.Nop(0x2DD1);
                    rdt.Nop(0x2DD4);
                    rdt.Nop(0x2DD7);
                    rdt.Nop(0x2DDA);
                    rdt.Nop(0x2DDD);
                    rdt.Nop(0x2DE0);

                    // NOP out Nemesis encounter (seems to break when we have the cut_replace at the top)
                    rdt.Nop(0x2E18);
                    rdt.Nop(0x2E30);
                    rdt.Nop(0x2E34);
                }
            }

            void FixRE3RestuarantFront()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x05));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 3, 2);
                }
            }

            void FixRE3Restuarant()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x11));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 23, 5);
                }
            }

            void FixRE3PressOffice()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0F));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 1, 0);
                    CopyDoorTo(rdt, 2, 0);
                    CopyDoorTo(rdt, 16, 0);

                    var door = rdt.Doors.First(x => x.Id == 2);
                    door.LockId = 0;
                    door.LockType = 0;
                }
            }

            void FixRE3TrainCrashExit()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x15));
                if (rdt != null)
                {
                    CopyDoorTo(rdt, 5, 4);
                }
            }

            void FixRE3PianoRoom()
            {
                var rdt = gameData.GetRdt(new RdtId(2, 0x01));
                if (rdt != null)
                {
                    AddCutCorrection(rdt, 112, 3, 10);
                }
            }

            void FixRE3Laboratory()
            {
                var rdt = gameData.GetRdt(new RdtId(3, 0x0A));
                if (rdt != null)
                {
                    AddCutCorrection(rdt, 91, 3, 11);
                }
            }

            void FixRE3CommsRoom()
            {
                var rdt = gameData.GetRdt(new RdtId(4, 0x0A));
                if (rdt != null)
                {
                    AddCutCorrection(rdt, 199, 11, 3);
                }
            }

            void FixRE3Rain()
            {
                // Rain often crashes for me
                var rainRooms = new[]
                {
                0x00, 0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x17
            };
                foreach (var rainRoom in rainRooms)
                {
                    var rdt = gameData.GetRdt(new RdtId(3, rainRoom));
                    if (rdt != null)
                    {
                        var rainOpcodes = rdt.Opcodes
                            .Where(x => x.Opcode == (byte)OpcodeV3.RainSet)
                            .ToArray();
                        foreach (var opcode in rainOpcodes)
                        {
                            rdt.Nop(opcode.Offset);
                        }
                    }
                }
            }

            void AddCutCorrection(Rdt rdt, byte flag3, byte originalCut, byte newCut)
            {
                // if (bits[3][flag3] == 1 && cut == originalCut)
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x10, 0x00 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 0x03, flag3, 0x01 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4E, new byte[] { 0x00, 0x1A, 0x00, originalCut, 0x00 }));

                // cut_chg(11);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x50, new byte[] { newCut }));

                // cut_auto(1);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x52, new byte[] { 1 }));

                // endif
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
            }

            void FixRE3Train()
            {
                var rdt = gameData.GetRdt(new RdtId(1, 0x0C));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                var elseIndex = Array.FindIndex(rdt.Opcodes, x => x.Offset == 0x4EE4);
                if (elseIndex != -1)
                {
                    rdt.Opcodes[elseIndex] = new UnknownOpcode(0x4EE4, (byte)OpcodeV3.EndIf, new byte[] { 0x00, 0x00 });
                }
            }

            void UnblockWasteDisposalDoor()
            {
                var rdt = gameData.GetRdt(new RdtId(4, 0x09));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                // 0: door
                rdt.Nop(0x599A);
                rdt.Nop(0x2876);

                // 2: card reader
                // rdt.Nop(0x2782);
                // rdt.Nop(0x286C);

                // 3
                rdt.Nop(0x58EC);
                rdt.Nop(0x2862);

                // 4
                rdt.Nop(0x5900);
                rdt.Nop(0x2858);
            }

            void FixRE3TowerOutdoor()
            {
                var rdt = gameData.GetRdt(new RdtId(2, 0x0B));
                if (rdt == null)
                    return;

                if (!config.RandomItems)
                    return;

                // Prevent Nemesis event from happening if clock puzzle is complete

                // if (bits[3][158] == 1)
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0A, 0x00 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 3, 158, 1 }));
                // bits[3][138] = 1
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4D, new byte[] { 3, 138, 1 }));
                // endif
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));

                if (!config.RandomDoors)
                    return;

                // Fix camera angle if Nemesis event has happened
                // if (bits[3][162] == 1)
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x06, new byte[] { 0x00, 0x0A, 0x00 }));
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4C, new byte[] { 3, 162, 1 }));
                // cut_chg(21);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x50, new byte[] { 21 }));
                // cut_auto(1);
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x52, new byte[] { 1 }));
                // endif
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x08, new byte[] { 0x00 }));
            }

            void FixNemesisFlag()
            {
                foreach (var rdt in gameData.Rdts)
                {
                    // Turn off some kind of Nemesis attack
                    // bits[1][0] = 0
                    rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x4D, new byte[] { 0x01, 0x00, 0x00 }));
                }
            }

            void FixWarehouseAlley()
            {
                var rdt = gameData.GetRdt(new RdtId(0, 0x1D));
                if (rdt == null)
                    return;

                rdt.Nop(0x0AA0);
            }
        }

        static void CopyDoorTo(Rdt rdt, int dstId, int srcId)
        {
            var src = rdt.Doors.First(x => x.Id == srcId);
            foreach (var dst in rdt.Doors.Where(x => x.Id == dstId))
            {
                CopyDoorTo(dst, src);
            }
            foreach (var dst in rdt.Resets.Where(x => x.Id == dstId && x.SCE == 1))
            {
                dst.NextX = src.NextX;
                dst.NextY = src.NextY;
                dst.NextZ = src.NextZ;
            }
        }

        static void CopyDoorTo(IDoorAotSetOpcode dst, IDoorAotSetOpcode src)
        {
            dst.Target = src.Target;
            dst.NextX = src.NextX;
            dst.NextY = src.NextY;
            dst.NextZ = src.NextZ;
            dst.NextD = src.NextD;
            dst.NextFloor = src.NextFloor;
            dst.NextCamera = src.NextCamera;
            dst.LockId = src.LockId;
            dst.LockType = src.LockType;
        }
    }
}
