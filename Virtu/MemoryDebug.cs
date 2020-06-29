using System;
using System.IO;
using System.Text;

namespace Jellyfish.Virtu {
    public partial class Memory {
        public void Exsourcise( int startAddress, int endAddress, string filename ) {
            int[] info = new int[0x10000];

            using (StreamWriter sourceFile = new System.IO.StreamWriter( filename + ".S", false )) {
                sourceFile.WriteLine( String.Format( " ORG ${0:X4}", startAddress ) );

                // Find externs
                int address = startAddress;
                while (address <= endAddress) {
                    if (DebugInfo[address].Flags.HasFlag( DebugFlags.Opcode )) {
                        int opcode = ReadDebug( address );
                        A addressingMode = AddressingMode65N02[opcode];
                        int opcodeLength = AddressingModeLength[(int)addressingMode];
                        int operand = ReadDebug( (address + 1) & 0xFFFF );
                        int byte2 = ReadDebug( (address + 2) & 0xFFFF );
                        if (opcodeLength == 3) {
                            operand = (byte2 << 8) | operand;
                        }

                        switch (addressingMode) {
                            case A.Rel: // Relative                   $0000
                                {
                                    int branchDest = (address + 2 + (sbyte)operand) & 0xFFFF;
                                    info[branchDest] |= 1; // label
                                }
                                break;

                            case A.Zpg: // Zero Page                  $00
                            case A.ZpX: // Zero Page Indexed          $00,X
                            case A.ZpY: // Zero Page Indexed          $00,Y
                            case A.ZpI: // Zero Page Indirect         ($00)
                            case A.ZIX: // Zero Page Indexed Indirect ($00,X)
                            case A.ZIY: // Zero Page Indirect Indexed ($00),Y
                            case A.Abs: // Absolute                   $0000
                            case A.AbX: // Absolute Indexed           $0000,X
                            case A.AbY: // Absolute Indexed           $0000,Y
                            case A.AbI: // Absolute Indirect          ($0000)
                            case A.AIX: // Absolute Indexed Indirect  ($0000,X)
                                {
                                    info[operand] |= 1; // label
                                    if (operand < startAddress || operand > endAddress) {
                                        info[operand] |= 2; // extern
                                    }
                                }
                                break;
                        }
                        address += opcodeLength;
                    } else {
                        address += 1;
                    }
                }

                // Output EQUs for externs
                for (address = 0; address <= 0xFFFF; address++) {
                    if ((info[address] & 2) != 0 && (address < startAddress || address > endAddress)) // extern
                    {
                        sourceFile.WriteLine( "H{0:X4} EQU ${0:X4}", address );
                    }
                }

                // Output source code
                address = startAddress;
                while (address <= endAddress) {
                    if ((info[address] & 1) != 0) // label
                    {
                        sourceFile.Write( "H{0:X4}", address );
                    }

                    int opcode = ReadDebug( address );
                    //if (Legal65N02[opcode]) // TODO: Standalone needs logic like this
                    if (DebugInfo[address].Flags.HasFlag( DebugFlags.Opcode ) || (opcode != 0x00 && Legal65N02[opcode])) {
                        int operand0 = ReadDebug( (address + 1) & 0xFFFF );
                        int operand1 = ReadDebug( (address + 2) & 0xFFFF );
                        int operand2 = (address + 2 + (sbyte)operand0) & 0xFFFF;
                        sourceFile.Write( " " );
                        sourceFile.Write( Mnemonic[(int)Mnemonic65N02[opcode]] );
                        sourceFile.Write( " " );
                        sourceFile.Write( AddressingModeFormat[(int)AddressingMode65N02[opcode]],
                            operand0,
                            operand1,
                            operand2,
                            "H" );
                        address += AddressingModeLength[(int)AddressingMode65N02[opcode]];
                    } else {
                        int ascLen = 0;
                        int test = opcode;
                        while (test >= 0x20 && test <= 0x5D) {
                            ascLen++;
                            if ((address + ascLen > endAddress) ||
                                (ascLen == 16) ||
                                (DebugInfo[address + ascLen].Flags.HasFlag( DebugFlags.Opcode ))) {
                                break;
                            }
                            test = ReadDebug( address + ascLen );
                        }

                        if (ascLen >= 5) {
                            sourceFile.Write( " ASC '", opcode );
                            for (int i = 0; i < ascLen; i++) {
                                sourceFile.Write( Convert.ToChar( ReadDebug( address + i ) ) );
                            }
                            sourceFile.Write( '\'' );
                            address += ascLen;
                        } else {
                            sourceFile.Write( " HEX {0:X2}", opcode );
                            address += 1;
                        }
                    }

                    sourceFile.WriteLine();
                }
            }
        }

        public int Disassemble( int address, StringBuilder stringBuilder ) {
            int opcode = ReadDebug( address );
            int operand0 = ReadDebug( (address + 1) & 0xFFFF );
            int operand1 = ReadDebug( (address + 2) & 0xFFFF );
            int operand2 = (address + 2 + (sbyte)operand0) & 0xFFFF;

            int mnemonic = (Machine.Cpu.Is65C02) ? (int)Mnemonic65C02[opcode] : (int)Mnemonic65N02[opcode];
            int addressingMode = (Machine.Cpu.Is65C02) ? (int)AddressingMode65C02[opcode] : (int)AddressingMode65N02[opcode];

            stringBuilder.Append( Mnemonic[mnemonic] );
            stringBuilder.Append( " " );
            stringBuilder.AppendFormat( AddressingModeFormat[addressingMode],
                operand0,
                operand1,
                operand2,
                "$" );
            return AddressingModeLength[addressingMode];
        }

        private readonly bool[] Legal65N02 = new bool[]
        {
            /*       x0     x1     x2     x3     x4     x5     x6     x7     x8     x9     xA     xB     xC     xD     xE     xF  */
            /* 0x */ true,  true,  false, false, false, true,  true,  false, true,  true,  true,  false, false, true,  true,  false,
            /* 1x */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
            /* 2x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 3x */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
            /* 4x */ true,  true,  false, false, false, true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 5x */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
            /* 6x */ true,  true,  false, false, false, true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 7x */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
            /* 8x */ false, true,  false, false, true,  true,  true,  false, true,  false, true,  false, true,  true,  true,  false,
            /* 9x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, false, true,  false, false,
            /* Ax */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Bx */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Cx */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Dx */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
            /* Ex */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Fx */ true,  true,  false, false, false, true,  true,  false, true,  true,  false, false, false, true,  true,  false,
        };

        private readonly bool[] Legal65C02 = new bool[]
        {
            /*       x0     x1     x2     x3     x4     x5     x6     x7     x8     x9     xA     xB     xC     xD     xE     xF  */
            /* 0x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 1x */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 2x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 3x */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 4x */ true,  true,  false, false, false, true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 5x */ true,  true,  true,  false, false, true,  true,  false, true,  true,  true,  false, false, true,  true,  false,
            /* 6x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 7x */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 8x */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* 9x */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Ax */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Bx */ true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Cx */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Dx */ true,  true,  true,  false, false, true,  true,  false, true,  true,  true,  false, false, true,  true,  false,
            /* Ex */ true,  true,  false, false, true,  true,  true,  false, true,  true,  true,  false, true,  true,  true,  false,
            /* Fx */ true,  true,  true,  false, false, true,  true,  false, true,  true,  true,  false, false, true,  true,  false,
        };

        public enum M {
            ADC,
            ANC, // 65N02 Illegal
            AND,
            ALR, // 65N02 Illegal
            ARR, // 65N02 Illegal
            ASL,
            ASO, // 65N02 Illegal
            AXA, // 65N02 Illegal
            AXS, // 65N02 Illegal
            BCC,
            BCS,
            BEQ,
            BIT,
            BMI,
            BNE,
            BPL,
            BRA, // 65C02
            BRK,
            BVC,
            BVS,
            CLC,
            CLD,
            CLI,
            CLV,
            CMP,
            CPX,
            CPY,
            DCM, // 65N02 Illegal
            DEC,
            DEX,
            DEY,
            EOR,
            HLT, // 65N02 Illegal
            INC,
            INS, // 65N02 Illegal
            INX,
            INY,
            JMP,
            JSR,
            LAS, // 65N02 Illegal
            LAX, // 65N02 Illegal
            LDA,
            LDX,
            LDY,
            LSE, // 65N02 Illegal
            LSR,
            NOP,
            OAL, // 65N02 Illegal
            ORA,
            PHA,
            PHP,
            PHX, // 65C02
            PHY, // 65C02
            PLA,
            PLP,
            PLX, // 65C02
            PLY, // 65C02
            RLA, // 65N02 Illegal
            ROL,
            ROR,
            RRA, // 65N02 Illegal
            RTI,
            RTS,
            SAX, // 65N02 Illegal
            SAY, // 65N02 Illegal
            SBC,
            SEC,
            SED,
            SEI,
            STA,
            STX,
            STY,
            STZ, // 65C02
            TAS, // 65N02 Illegal
            TAX,
            TAY,
            TRB, // 65C02
            TSB, // 65C02
            TSX,
            TXA,
            TXS,
            TYA,
            XAA, // 65N02 Illegal
            XAS, // 65N02 Illegal
        };

        public static string[] Mnemonic = new string[]
        {
            "ADC",
            "ANC", // 65N02 Illegal
            "AND",
            "ALR", // 65N02 Illegal
            "ARR", // 65N02 Illegal
            "ASL",
            "ASO", // 65N02 Illegal
            "AXA", // 65N02 Illegal
            "AXS", // 65N02 Illegal
            "BCC",
            "BCS",
            "BEQ",
            "BIT",
            "BMI",
            "BNE",
            "BPL",
            "BRA", // 65C02
            "BRK",
            "BVC",
            "BVS",
            "CLC",
            "CLD",
            "CLI",
            "CLV",
            "CMP",
            "CPX",
            "CPY",
            "DCM", // 65N02 Illegal
            "DEC",
            "DEX",
            "DEY",
            "EOR",
            "HLT", // 65N02 Illegal
            "INC",
            "INS", // 65N02 Illegal
            "INX",
            "INY",
            "JMP",
            "JSR",
            "LAS", // 65N02 Illegal
            "LAX", // 65N02 Illegal
            "LDA",
            "LDX",
            "LDY",
            "LSE", // 65N02 Illegal
            "LSR",
            "NOP",
            "OAL", // 65N02 Illegal
            "ORA",
            "PHA",
            "PHP",
            "PHX", // 65C02
            "PHY", // 65C02
            "PLA",
            "PLP",
            "PLX", // 65C02
            "PLY", // 65C02
            "RLA", // 65N02 Illegal
            "ROL",
            "ROR",
            "RRA", // 65N02 Illegal
            "RTI",
            "RTS",
            "SAX", // 65N02 Illegal
            "SAY", // 65N02 Illegal
            "SBC",
            "SEC",
            "SED",
            "SEI",
            "STA",
            "STX",
            "STY",
            "STZ", // 65C02
            "TAS", // 65N02 Illegal
            "TAX",
            "TAY",
            "TRB", // 65C02
            "TSB", // 65C02
            "TSX",
            "TXA",
            "TXS",
            "TYA",
            "XAA", // 65N02 Illegal
            "XAS", // 65N02 Illegal
        };

        public static M[] Mnemonic65N02 = new M[]
        {
            // x0     x1     x2     x3     x4     x5     x6     x7     x8     x9     xA     xB     xC     xD     xE     xF
            M.BRK, M.ORA, M.HLT, M.ASO, M.NOP, M.ORA, M.ASL, M.ASO, M.PHP, M.ORA, M.ASL, M.ANC, M.NOP, M.ORA, M.ASL, M.ASO, // 0x
            M.BPL, M.ORA, M.HLT, M.ASO, M.NOP, M.ORA, M.ASL, M.ASO, M.CLC, M.ORA, M.NOP, M.ASO, M.NOP, M.ORA, M.ASL, M.ASO, // 1x
            M.JSR, M.AND, M.HLT, M.RLA, M.BIT, M.AND, M.ROL, M.RLA, M.PLP, M.AND, M.ROL, M.ANC, M.BIT, M.AND, M.ROL, M.RLA, // 2x
            M.BMI, M.AND, M.HLT, M.RLA, M.NOP, M.AND, M.ROL, M.RLA, M.SEC, M.AND, M.NOP, M.RLA, M.NOP, M.AND, M.ROL, M.RLA, // 3x
            M.RTI, M.EOR, M.HLT, M.LSE, M.NOP, M.EOR, M.LSR, M.LSE, M.PHA, M.EOR, M.LSR, M.ALR, M.JMP, M.EOR, M.LSR, M.LSE, // 4x
            M.BVC, M.EOR, M.HLT, M.LSE, M.NOP, M.EOR, M.LSR, M.LSE, M.CLI, M.EOR, M.NOP, M.LSE, M.NOP, M.EOR, M.LSR, M.LSE, // 5x
            M.RTS, M.ADC, M.HLT, M.RRA, M.NOP, M.ADC, M.ROR, M.RRA, M.PLA, M.ADC, M.ROR, M.ARR, M.JMP, M.ADC, M.ROR, M.RRA, // 6x
            M.BVS, M.ADC, M.HLT, M.RRA, M.NOP, M.ADC, M.ROR, M.RRA, M.SEI, M.ADC, M.NOP, M.RRA, M.NOP, M.ADC, M.ROR, M.RRA, // 7x
            M.NOP, M.STA, M.NOP, M.AXS, M.STY, M.STA, M.STX, M.AXS, M.DEY, M.NOP, M.TXA, M.XAA, M.STY, M.STA, M.STX, M.AXS, // 8x
            M.BCC, M.STA, M.HLT, M.AXA, M.STY, M.STA, M.STX, M.AXS, M.TYA, M.STA, M.TXS, M.TAS, M.SAY, M.STA, M.XAS, M.AXA, // 9x
            M.LDY, M.LDA, M.LDX, M.LAX, M.LDY, M.LDA, M.LDX, M.LAX, M.TAY, M.LDA, M.TAX, M.OAL, M.LDY, M.LDA, M.LDX, M.LAX, // Ax
            M.BCS, M.LDA, M.HLT, M.LAX, M.LDY, M.LDA, M.LDX, M.LAX, M.CLV, M.LDA, M.TSX, M.LAS, M.LDY, M.LDA, M.LDX, M.LAX, // Bx
            M.CPY, M.CMP, M.NOP, M.DCM, M.CPY, M.CMP, M.DEC, M.DCM, M.INY, M.CMP, M.DEX, M.SAX, M.CPY, M.CMP, M.DEC, M.DCM, // Cx
            M.BNE, M.CMP, M.HLT, M.DCM, M.NOP, M.CMP, M.DEC, M.DCM, M.CLD, M.CMP, M.NOP, M.DCM, M.NOP, M.CMP, M.DEC, M.DCM, // Dx
            M.CPX, M.SBC, M.NOP, M.INS, M.CPX, M.SBC, M.INC, M.INS, M.INX, M.SBC, M.NOP, M.SBC, M.CPX, M.SBC, M.INC, M.INS, // Ex
            M.BEQ, M.SBC, M.HLT, M.INS, M.NOP, M.SBC, M.INC, M.INS, M.SED, M.SBC, M.NOP, M.INS, M.NOP, M.SBC, M.INC, M.INS  // Fx
        };

        public static readonly M[] Mnemonic65C02 = new M[]
        {
            // x0     x1     x2     x3     x4     x5     x6     x7     x8     x9     xA     xB     xC     xD     xE     xF
            M.BRK, M.ORA, M.NOP, M.NOP, M.TSB, M.ORA, M.ASL, M.NOP, M.PHP, M.ORA, M.ASL, M.NOP, M.TSB, M.ORA, M.ASL, M.NOP, // 0x
            M.BPL, M.ORA, M.ORA, M.NOP, M.TRB, M.ORA, M.ASL, M.NOP, M.CLC, M.ORA, M.INC, M.NOP, M.TRB, M.ORA, M.ASL, M.NOP, // 1x
            M.JSR, M.AND, M.NOP, M.NOP, M.BIT, M.AND, M.ROL, M.NOP, M.PLP, M.AND, M.ROL, M.NOP, M.BIT, M.AND, M.ROL, M.NOP, // 2x
            M.BMI, M.AND, M.AND, M.NOP, M.BIT, M.AND, M.ROL, M.NOP, M.SEC, M.AND, M.DEC, M.NOP, M.BIT, M.AND, M.ROL, M.NOP, // 3x
            M.RTI, M.EOR, M.NOP, M.NOP, M.NOP, M.EOR, M.LSR, M.NOP, M.PHA, M.EOR, M.LSR, M.NOP, M.JMP, M.EOR, M.LSR, M.NOP, // 4x
            M.BVC, M.EOR, M.EOR, M.NOP, M.NOP, M.EOR, M.LSR, M.NOP, M.CLI, M.EOR, M.PHY, M.NOP, M.NOP, M.EOR, M.LSR, M.NOP, // 5x
            M.RTS, M.ADC, M.NOP, M.NOP, M.STZ, M.ADC, M.ROR, M.NOP, M.PLA, M.ADC, M.ROR, M.NOP, M.JMP, M.ADC, M.ROR, M.NOP, // 6x
            M.BVS, M.ADC, M.ADC, M.NOP, M.STZ, M.ADC, M.ROR, M.NOP, M.SEI, M.ADC, M.PLY, M.NOP, M.JMP, M.ADC, M.ROR, M.NOP, // 7x
            M.BRA, M.STA, M.NOP, M.NOP, M.STY, M.STA, M.STX, M.NOP, M.DEY, M.BIT, M.TXA, M.NOP, M.STY, M.STA, M.STX, M.NOP, // 8x
            M.BCC, M.STA, M.STA, M.NOP, M.STY, M.STA, M.STX, M.NOP, M.TYA, M.STA, M.TXS, M.NOP, M.STZ, M.STA, M.STZ, M.NOP, // 9x
            M.LDY, M.LDA, M.LDX, M.NOP, M.LDY, M.LDA, M.LDX, M.NOP, M.TAY, M.LDA, M.TAX, M.NOP, M.LDY, M.LDA, M.LDX, M.NOP, // Ax
            M.BCS, M.LDA, M.LDA, M.NOP, M.LDY, M.LDA, M.LDX, M.NOP, M.CLV, M.LDA, M.TSX, M.NOP, M.LDY, M.LDA, M.LDX, M.NOP, // Bx
            M.CPY, M.CMP, M.NOP, M.NOP, M.CPY, M.CMP, M.DEC, M.NOP, M.INY, M.CMP, M.DEX, M.NOP, M.CPY, M.CMP, M.DEC, M.NOP, // Cx
            M.BNE, M.CMP, M.CMP, M.NOP, M.NOP, M.CMP, M.DEC, M.NOP, M.CLD, M.CMP, M.PHX, M.NOP, M.NOP, M.CMP, M.DEC, M.NOP, // Dx
            M.CPX, M.SBC, M.NOP, M.NOP, M.CPX, M.SBC, M.INC, M.NOP, M.INX, M.SBC, M.NOP, M.NOP, M.CPX, M.SBC, M.INC, M.NOP, // Ex
            M.BEQ, M.SBC, M.SBC, M.NOP, M.NOP, M.SBC, M.INC, M.NOP, M.SED, M.SBC, M.PLX, M.NOP, M.NOP, M.SBC, M.INC, M.NOP  // Fx
        };

        public enum A {
            Imp, // Implied
            Imm, // Immediate                  #$00
            Rel, // Relative                   $0000
            Zpg, // Zero Page                  $00
            ZpX, // Zero Page Indexed          $00,X
            ZpY, // Zero Page Indexed          $00,Y
            ZpI, // Zero Page Indirect         ($00)
            ZIX, // Zero Page Indexed Indirect ($00,X)
            ZIY, // Zero Page Indirect Indexed ($00),Y
            Abs, // Absolute                   $0000
            AbX, // Absolute Indexed           $0000,X
            AbY, // Absolute Indexed           $0000,Y
            AbI, // Absolute Indirect          ($0000)
            AIX, // Absolute Indexed Indirect  ($0000,X)
            H_1, // HEX 1 byte operand         00
            H_2  // HEX 2 byte operand         00 00
        };

        private readonly int[] AddressingModeLength = new int[]
        {
            1, // Imp: Implied
            2, // Imm: Immediate                  #$00
            2, // Rel: Relative                   $0000
            2, // Zpg: Zero Page                  $00
            2, // ZpX: Zero Page Indexed          $00,X
            2, // ZpY: Zero Page Indexed          $00,Y
            2, // ZIX: Zero Page Indexed Indirect ($00,X)
            2, // ZIY: Zero Page Indirect Indexed ($00),Y
            2, // ZpI: Zero Page Indirect         ($00)
            3, // Abs: Absolute                   $0000
            3, // AbX: Absolute Indexed           $0000,X
            3, // AbY: Absolute Indexed           $0000,Y
            3, // AbI: Absolute Indirect          ($0000)
            3, // AIX: Absolute Indexed Indirect  ($0000,X)
            2, // H_1: HEX 1 byte operand         00
            3  // H_2: HEX 2 byte operand         00 00
        };

        public static string[] AddressingModeFormat = new string[]
        {
            "",                     // Imp: Implied
            "#${0:X2}",             // Imm: Immediate                  #$00
            "{3}{2:X4}",            // Rel: Relative                   $0000
            "{3}{0:X2}",            // Zpg: Zero Page                  $00
            "{3}{0:X2},X",          // ZpX: Zero Page Indexed          $00,X
            "{3}{0:X2},Y",          // ZpY: Zero Page Indexed          $00,Y
            "({3}{0:X2})",          // ZpI: Zero Page Indirect         ($00)
            "({3}{0:X2},X)",        // ZIX: Zero Page Indexed Indirect ($00,X)
            "({3}{0:X2}),Y",        // ZIY: Zero Page Indirect Indexed ($00),Y
            "{3}{1:X2}{0:X2}",      // Abs: Absolute                   $0000
            "{3}{1:X2}{0:X2},X",    // AbX: Absolute Indexed           $0000,X
            "{3}{1:X2}{0:X2},Y",    // AbY: Absolute Indexed           $0000,Y
            "({3}{1:X2}{0:X2})",    // AbI: Absolute Indirect          ($0000)
            "({3}{1:X2}{0:X2},X)",  // AIX: Absolute Indexed Indirect  ($0000,X)
            "{0:X2}",               // H_1: HEX 1 byte operand         00
            "{0:X2} {1:X2}"         // H_2: HEX 2 byte operand         00 00
        };

        public static A[] AddressingMode65N02 = new A[]
        {
            // TODO: Fix this up (H_1 etc to actual undocumented addressing modes)
            /* M  / L 0    1      2      3      4      5      6      7      8      9      A      B      C      D      E      F           */
            /* 0 */ A.Imp, A.ZIX, A.Imp, A.H_1, A.H_1, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.H_2, A.Abs, A.Abs, A.H_2, /* 0 */
            /* 1 */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2, /* 1 */
            /* 2 */ A.Abs, A.ZIX, A.Imp, A.H_1, A.Zpg, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* 2 */
            /* 3 */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2, /* 3 */
            /* 4 */ A.Imp, A.ZIX, A.Imp, A.H_1, A.H_1, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* 4 */
            /* 5 */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2, /* 5 */
            /* 6 */ A.Imp, A.ZIX, A.Imp, A.H_1, A.H_1, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.AbI, A.Abs, A.Abs, A.H_2, /* 6 */
            /* 7 */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2, /* 7 */
            /* 8 */ A.H_1, A.ZIX, A.H_1, A.H_1, A.Zpg, A.Zpg, A.Zpg, A.H_1, A.Imp, A.H_1, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* 8 */
            /* 9 */ A.Rel, A.ZIY, A.Imp, A.H_1, A.ZpX, A.ZpX, A.ZpY, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.H_2, A.H_2, /* 9 */
            /* A */ A.Imm, A.ZIX, A.Imm, A.H_1, A.Zpg, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* A */
            /* B */ A.Rel, A.ZIY, A.Imp, A.H_1, A.ZpX, A.ZpX, A.ZpY, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.AbX, A.AbX, A.AbY, A.H_2, /* B */
            /* C */ A.Imm, A.ZIX, A.H_1, A.H_1, A.Zpg, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* C */
            /* D */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2, /* D */
            /* E */ A.Imm, A.ZIX, A.H_1, A.H_1, A.Zpg, A.Zpg, A.Zpg, A.H_1, A.Imp, A.Imm, A.Imp, A.H_1, A.Abs, A.Abs, A.Abs, A.H_2, /* E */
            /* F */ A.Rel, A.ZIY, A.Imp, A.H_1, A.H_1, A.ZpX, A.ZpX, A.H_1, A.Imp, A.AbY, A.Imp, A.H_2, A.H_2, A.AbX, A.AbX, A.H_2  /* F */
            /*          0      1      2      3      4      5      6      7      8      9      A      B      C      D      E      F       */
        };

        public static A[] AddressingMode65C02 = new A[]
        {
            /* M  / L   0      1      2      3      4      5      6      7      8      9      A      B      C      D      E      F       */
            /* 0 */ A.Imp, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* 0 */
            /* 1 */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.Zpg, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.Abs, A.AbX, A.AbX, A.Imp, /* 1 */
            /* 2 */ A.Abs, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* 2 */
            /* 3 */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.ZpX, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.AbX, A.AbX, A.AbX, A.Imp, /* 3 */
            /* 4 */ A.Imp, A.ZIX, A.H_1, A.Imp, A.H_1, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* 4 */
            /* 5 */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.H_1, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.H_2, A.AbX, A.AbX, A.Imp, /* 5 */
            /* 6 */ A.Imp, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.AbI, A.Abs, A.Abs, A.Imp, /* 6 */
            /* 7 */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.ZpX, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.AIX, A.AbX, A.AbX, A.Imp, /* 7 */
            /* 8 */ A.Rel, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* 8 */
            /* 9 */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.ZpX, A.ZpX, A.ZpY, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.Abs, A.AbX, A.AbX, A.Imp, /* 9 */
            /* A */ A.Imm, A.ZIX, A.Imm, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* A */
            /* B */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.ZpX, A.ZpX, A.ZpY, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.AbX, A.AbX, A.AbY, A.Imp, /* B */
            /* C */ A.Imm, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* C */
            /* D */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.H_1, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.H_2, A.AbX, A.AbX, A.Imp, /* D */
            /* E */ A.Imm, A.ZIX, A.H_1, A.Imp, A.Zpg, A.Zpg, A.Zpg, A.Imp, A.Imp, A.Imm, A.Imp, A.Imp, A.Abs, A.Abs, A.Abs, A.Imp, /* E */
            /* F */ A.Rel, A.ZIY, A.ZpI, A.Imp, A.H_1, A.ZpX, A.ZpX, A.Imp, A.Imp, A.AbY, A.Imp, A.Imp, A.H_2, A.AbX, A.AbX, A.Imp  /* F */
            /*          0      1      2      3      4      5      6      7      8      9      A      B      C      D      E      F       */
        };
    }
}
