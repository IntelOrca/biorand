﻿namespace rer
{
    internal enum Opcode : byte
    {

        Nop,
        EvtEnd,
        EvtNext,
        EvtChain,
        EvtExec,
        EvtKill,
        IfelCk,
        ElseCk,
        EndIf,
        Sleep,
        Sleeping,
        Wsleep,
        Wsleeping,
        For,
        Next,
        While,
        Ewhile,
        Do,
        Edwhile,
        Switch,
        Case,
        Default,
        Eswitch,
        Goto,
        Gosub,
        Return,
        Break,
        For2,
        BreakPoint,
        WorkCopy,
        Ck = 0x21,
        Set,
        Cmp,
        Save,
        Copy,
        Calc,
        Calc2,
        SceRnd,
        CutChg,
        CutOld,
        MessageOn,
        AotSet,
        ObjModelSet,
        WorkSet,
        SpeedSet,
        AddSpeed,
        AddASpeed,
        PosSet,
        DirSet,
        MemberSet,
        MemberSet2,
        SeOn,
        DoorAotSe = 0x3B,
        CutAuto,
        MemberCopy,
        MemberCmp,
        PlcDest = 0x40,
        PlcNeck,
        PlcRet,
        PlcFlg,
        SceEmSet,
        ColChgSet,
        AotReset,
        AotOn,
        SuperSet,
        SuperReset,
        ItemAotSet = 0x4E,
        SceBgmControl = 0x51,
        SceEsprControl,
        SceFadeSet,
        SceEspr3dOn,
        SceBgmtblSet = 0x57,
        PlcRot,
        XaOn,
        SceItemLost = 0x62,
        PlcStop = 0x66,
        AotSet4p,
        DoorAotSet4p,
        ItemAotSet4p,
        SceScrMove = 0x6D,
        PartsSet,
        MovieOn,
        SceFadeAdjust = 0x74,
        SceItemGet = 0x76,
        Unk81 = 0x81
    }
}
