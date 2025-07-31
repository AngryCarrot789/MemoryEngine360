// 
// Copyright (c) 2025-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

namespace MemEngine360.Engine.Debugging;

public readonly struct VECTOR128(ulong lowPart, ulong highPart) {
    public readonly ulong LowPart = lowPart;
    public readonly ulong HighPart = highPart;
}

public enum EnumContextFlags : uint {
    CONTROL            = 0x01, // SS:SP, CS:IP, FLAGS, BP
    INTEGER            = 0x02, // AX, BX, CX, DX, SI, DI
    SEGMENTS           = 0x04, // DS, ES, FS, GS
    FLOATING_POINT     = 0x08, // 387 state
    DEBUG_REGISTERS    = 0x10, // DB 0-3,6,7
    EXTENDED_REGISTERS = 0x20, // cpu specific extensions
    FULL = CONTROL | INTEGER | SEGMENTS
}

public struct ThreadContext {
    public EnumContextFlags ContextFlags;
    public uint MSR;
    public uint IAR;
    public uint LR;
    public ulong CTR;
    public ulong GPR0;
    public ulong GPR1;
    public ulong GPR2;
    public ulong GPR3;
    public ulong GPR4;
    public ulong GPR5;
    public ulong GPR6;
    public ulong GPR7;
    public ulong GPR8;
    public ulong GPR9;
    public ulong GPR10;
    public ulong GPR11;
    public ulong GPR12;
    public ulong GPR13;
    public ulong GPR14;
    public ulong GPR15;
    public ulong GPR16;
    public ulong GPR17;
    public ulong GPR18;
    public ulong GPR19;
    public ulong GPR20;
    public ulong GPR21;
    public ulong GPR22;
    public ulong GPR23;
    public ulong GPR24;
    public ulong GPR25;
    public ulong GPR26;
    public ulong GPR27;
    public ulong GPR28;
    public ulong GPR29;
    public ulong GPR30;
    public ulong GPR31;
    public uint CR;
    public uint XER;
    public ulong FPSCR;
    public ulong FPR0;
    public ulong FPR1;
    public ulong FPR2;
    public ulong FPR3;
    public ulong FPR4;
    public ulong FPR5;
    public ulong FPR6;
    public ulong FPR7;
    public ulong FPR8;
    public ulong FPR9;
    public ulong FPR10;
    public ulong FPR11;
    public ulong FPR12;
    public ulong FPR13;
    public ulong FPR14;
    public ulong FPR15;
    public ulong FPR16;
    public ulong FPR17;
    public ulong FPR18;
    public ulong FPR19;
    public ulong FPR20;
    public ulong FPR21;
    public ulong FPR22;
    public ulong FPR23;
    public ulong FPR24;
    public ulong FPR25;
    public ulong FPR26;
    public ulong FPR27;
    public ulong FPR28;
    public ulong FPR29;
    public ulong FPR30;
    public ulong FPR31;
    public ulong Reserved1;
    public VECTOR128 VSCR;
    public VECTOR128 VR0;
    public VECTOR128 VR1;
    public VECTOR128 VR2;
    public VECTOR128 VR3;
    public VECTOR128 VR4;
    public VECTOR128 VR5;
    public VECTOR128 VR6;
    public VECTOR128 VR7;
    public VECTOR128 VR8;
    public VECTOR128 VR9;
    public VECTOR128 VR10;
    public VECTOR128 VR11;
    public VECTOR128 VR12;
    public VECTOR128 VR13;
    public VECTOR128 VR14;
    public VECTOR128 VR15;
    public VECTOR128 VR16;
    public VECTOR128 VR17;
    public VECTOR128 VR18;
    public VECTOR128 VR19;
    public VECTOR128 VR20;
    public VECTOR128 VR21;
    public VECTOR128 VR22;
    public VECTOR128 VR23;
    public VECTOR128 VR24;
    public VECTOR128 VR25;
    public VECTOR128 VR26;
    public VECTOR128 VR27;
    public VECTOR128 VR28;
    public VECTOR128 VR29;
    public VECTOR128 VR30;
    public VECTOR128 VR31;
    public VECTOR128 VR32;
    public VECTOR128 VR33;
    public VECTOR128 VR34;
    public VECTOR128 VR35;
    public VECTOR128 VR36;
    public VECTOR128 VR37;
    public VECTOR128 VR38;
    public VECTOR128 VR39;
    public VECTOR128 VR40;
    public VECTOR128 VR41;
    public VECTOR128 VR42;
    public VECTOR128 VR43;
    public VECTOR128 VR44;
    public VECTOR128 VR45;
    public VECTOR128 VR46;
    public VECTOR128 VR47;
    public VECTOR128 VR48;
    public VECTOR128 VR49;
    public VECTOR128 VR50;
    public VECTOR128 VR51;
    public VECTOR128 VR52;
    public VECTOR128 VR53;
    public VECTOR128 VR54;
    public VECTOR128 VR55;
    public VECTOR128 VR56;
    public VECTOR128 VR57;
    public VECTOR128 VR58;
    public VECTOR128 VR59;
    public VECTOR128 VR60;
    public VECTOR128 VR61;
    public VECTOR128 VR62;
    public VECTOR128 VR63;
    public VECTOR128 VR64;
    public VECTOR128 VR65;
    public VECTOR128 VR66;
    public VECTOR128 VR67;
    public VECTOR128 VR68;
    public VECTOR128 VR69;
    public VECTOR128 VR70;
    public VECTOR128 VR71;
    public VECTOR128 VR72;
    public VECTOR128 VR73;
    public VECTOR128 VR74;
    public VECTOR128 VR75;
    public VECTOR128 VR76;
    public VECTOR128 VR77;
    public VECTOR128 VR78;
    public VECTOR128 VR79;
    public VECTOR128 VR80;
    public VECTOR128 VR81;
    public VECTOR128 VR82;
    public VECTOR128 VR83;
    public VECTOR128 VR84;
    public VECTOR128 VR85;
    public VECTOR128 VR86;
    public VECTOR128 VR87;
    public VECTOR128 VR88;
    public VECTOR128 VR89;
    public VECTOR128 VR90;
    public VECTOR128 VR91;
    public VECTOR128 VR92;
    public VECTOR128 VR93;
    public VECTOR128 VR94;
    public VECTOR128 VR95;
    public VECTOR128 VR96;
    public VECTOR128 VR97;
    public VECTOR128 VR98;
    public VECTOR128 VR99;
    public VECTOR128 VR100;
    public VECTOR128 VR101;
    public VECTOR128 VR102;
    public VECTOR128 VR103;
    public VECTOR128 VR104;
    public VECTOR128 VR105;
    public VECTOR128 VR106;
    public VECTOR128 VR107;
    public VECTOR128 VR108;
    public VECTOR128 VR109;
    public VECTOR128 VR110;
    public VECTOR128 VR111;
    public VECTOR128 VR112;
    public VECTOR128 VR113;
    public VECTOR128 VR114;
    public VECTOR128 VR115;
    public VECTOR128 VR116;
    public VECTOR128 VR117;
    public VECTOR128 VR118;
    public VECTOR128 VR119;
    public VECTOR128 VR120;
    public VECTOR128 VR121;
    public VECTOR128 VR122;
    public VECTOR128 VR123;
    public VECTOR128 VR124;
    public VECTOR128 VR125;
    public VECTOR128 VR126;
    public VECTOR128 VR127;
}