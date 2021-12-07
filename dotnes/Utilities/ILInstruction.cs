﻿using System.Reflection.Metadata;

namespace dotnes;

/// <summary>
/// Holds info about IL
/// </summary>
record ILInstruction(ILOpCode OpCode, int? Integer = null, string? String = null);
