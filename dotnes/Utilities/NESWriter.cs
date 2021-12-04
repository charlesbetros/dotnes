﻿using System.Text;

namespace dotnes;

/// <summary>
/// Writes .nes files
/// * https://wiki.nesdev.org/w/index.php/INES
/// * https://bheisler.github.io/post/nes-rom-parser-with-nom/
/// </summary>
class NESWriter : IDisposable
{
    const int TEMP = 0x17;

    readonly BinaryWriter _writer;

    public NESWriter(Stream stream, bool leaveOpen = false) => _writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen);

    /// <summary>
    /// Trainer, if present (0 or 512 bytes)
    /// </summary>
    public byte[]? Trainer { get; set; }

    /// <summary>
    /// PRG ROM data (16384 * x bytes)
    /// </summary>
    public byte[]? PRG_ROM { get; set; }

    /// <summary>
    /// CHR ROM data, if present (8192 * y bytes)
    /// </summary>
    public byte[]? CHR_ROM { get; set; }

    /// <summary>
    /// PlayChoice INST-ROM, if present (0 or 8192 bytes)
    /// </summary>
    public byte[]? INST_ROM { get; set; }

    /// <summary>
    /// Mapper, mirroring, battery, trainer
    /// </summary>
    public byte Flags6 { get; set; }

    /// <summary>
    /// Mapper, VS/Playchoice, NES 2.0
    /// </summary>
    public byte Flags7 { get; set; }

    /// <summary>
    /// PRG-RAM size (rarely used extension)
    /// </summary>
    public byte Flags8 { get; set; }

    /// <summary>
    /// TV system (rarely used extension)
    /// </summary>
    public byte Flags9 { get; set; }

    /// <summary>
    /// TV system, PRG-RAM presence (unofficial, rarely used extension)
    /// </summary>
    public byte Flags10 { get; set; }

    public void WriteHeader()
    {
        _writer.Write('N');
        _writer.Write('E');
        _writer.Write('S');
        _writer.Write('\x1A');
        // Size of PRG ROM in 16 KB units
        _writer.Write(checked ((byte)(PRG_ROM.Length / 16384)));
        // Size of CHR ROM in 8 KB units (Value 0 means the board uses CHR RAM)
        if (CHR_ROM != null)
            _writer.Write(checked((byte)(CHR_ROM.Length / 8192)));
        else
            _writer.Write((byte)0);
        _writer.Write(Flags6);
        _writer.Write(Flags7);
        _writer.Write(Flags8);
        _writer.Write(Flags9);
        _writer.Write(Flags10);
        // 5 bytes of padding
        for (int i = 0; i < 5; i++)
        {
            _writer.Write((byte)0);
        }
    }

    /// <summary>
    /// Writes a built-in method from NESLib
    /// </summary>
    public void WriteBuiltIn(string name)
    {
        switch (name)
        {
            case nameof(NESLib.pal_all):
                /*
                 * 8211	8517          	STA TEMP                      ; _pal_all
                 * 8213	8618          	STX TEMP+1                    
                 * 8215	A200          	LDX #$00                      
                 * 8217	A920          	LDA #$20                      
                 */
                STA(TEMP);
                STX(TEMP + 1);
                LDX(0x00);
                LDA(0x20);
                break;
            // NOTE: this one is internal, not in neslib.h
            case "pal_copy":
                /*
                 * 8219	8519          	STA $19                       ; pal_copy
                 * 821B	A000          	LDY #$00
                 */
                STA(0x19);
                LDY(0x00);
                break;
            case nameof(NESLib.pal_bg):
                /*
                 * 822B	8517          	STA TEMP                      ; _pal_bg
                 * 822D	8618          	STX TEMP+1                    
                 * 822F	A200          	LDX #$00                      
                 * 8231	A910          	LDA #$10                      
                 * 8233	D0E4          	BNE pal_copy
                 */
                STA(TEMP);
                STX(TEMP + 1);
                LDX(0x00);
                LDA(0x10);
                BNE(0xE4);
                break;
            case nameof (NESLib.pal_spr):
                /*
                 * 8235	8517          	STA TEMP                      ; _pal_spr
                 * 8237	8618          	STX TEMP+1                    
                 * 8239	A210          	LDX #$10                      
                 * 823B	8A            	TXA                           
                 * 823C	D0DB          	BNE pal_copy
                 */
                STA(TEMP);
                STX(TEMP + 1);
                LDX(0x10);
                TXA();
                BNE(0xDB);
                break;
            case nameof (NESLib.pal_col):
                /*
                 * 823E	8517          	STA TEMP                      ; _pal_col
                 * 8240	205085        	JSR popa                      
                 * 8243	291F          	AND #$1F                      
                 * 8245	AA            	TAX                           
                 * 8246	A517          	LDA TEMP                      
                 * 8248	9DC001        	STA $01C0,x                   
                 * 824B	E607          	INC PAL_UPDATE                
                 * 824D	60            	RTS
                 */
                STA(TEMP);
                JSR(0x8550);
                AND(0x1F);
                TAX();
                LDA_zpg(TEMP);
                STA_abs_X(0x01C0);
                INC(0x07);
                RTS();
                break;
            default:
                throw new NotImplementedException($"{name} is not implemented!");
        }
    }

    /// <summary>
    /// 20: Jump to New Location Saving Return Address
    /// </summary>
    public void JSR(ushort address)
    {
        _writer.Write((byte)Instruction.JSR);
        _writer.Write(address);
    }

    /// <summary>
    /// 29: AND Memory with Accumulator
    /// </summary>
    public void AND(byte n)
    {
        _writer.Write((byte)Instruction.AND);
        _writer.Write(n);
    }

    /// <summary>
    /// 60: Return from Subroutine
    /// </summary>

    public void RTS() => _writer.Write((byte)Instruction.RTS_impl);

    /// <summary>
    /// 85: Store Accumulator in Memory
    /// </summary>
    public void STA(byte n)
    {
        _writer.Write((byte)Instruction.STA_zpg);
        _writer.Write(n);
    }

    /// <summary>
    /// 86: Store Index X in Memory
    /// </summary>
    public void STX(byte n)
    {
        _writer.Write((byte)Instruction.STX_zpg);
        _writer.Write(n);
    }

    /// <summary>
    /// 8A: Transfer Index X to Accumulator
    /// </summary>
    public void TXA() => _writer.Write((byte)Instruction.TXA_impl);

    /// <summary>
    /// 9D: Store Accumulator in Memory
    /// </summary>
    public void STA_abs_X(ushort address)
    {
        _writer.Write((byte)Instruction.STA_abs_X);
        _writer.Write(address);
    }

    /// <summary>
    /// A0: Load Index Y with Memory
    /// </summary>
    public void LDY(byte n)
    {
        _writer.Write((byte)Instruction.LDY);
        _writer.Write(n);
    }

    /// <summary>
    /// A2: Load Index X with Memory
    /// </summary>
    public void LDX(byte n)
    {
        _writer.Write((byte)Instruction.LDX);
        _writer.Write(n);
    }

    /// <summary>
    /// A5: Load Accumulator with Memory
    /// </summary>
    public void LDA_zpg(byte n)
    {
        _writer.Write((byte)Instruction.LDA_zpg);
        _writer.Write(n);
    }

    /// <summary>
    /// A9: Load Accumulator with Memory
    /// </summary>
    public void LDA(byte n)
    {
        _writer.Write((byte)Instruction.LDA);
        _writer.Write(n);
    }

    /// <summary>
    /// AA: Transfer Accumulator to Index X
    /// </summary>
    public void TAX() => _writer.Write((byte)Instruction.TAX_impl);

    /// <summary>
    /// D0: Branch on Result not Zero
    /// </summary>
    public void BNE(byte n)
    {
        _writer.Write((byte)Instruction.BNE_rel);
        _writer.Write(n);
    }

    /// <summary>
    /// E6: Increment Memory by One
    /// </summary>
    public void INC(byte n)
    {
        _writer.Write((byte)Instruction.INC_zpg);
        _writer.Write(n);
    }

    public void Write()
    {
        WriteHeader();
        if (PRG_ROM != null)
            _writer.Write(PRG_ROM);
        if (CHR_ROM != null)
            _writer.Write(CHR_ROM);
        if (Trainer != null)
            _writer.Write(Trainer);
        if (INST_ROM != null)
            _writer.Write(INST_ROM);
    }

    public void Flush() => _writer.Flush();

    public void Dispose() => _writer.Dispose();
}
