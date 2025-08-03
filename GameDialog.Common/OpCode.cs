namespace GameDialog.Common;

public static class OpCode
{
    public const ushort Undefined = 0;
    // [Op, float index]
    public const ushort Float = 1;
    // [Op, string index]
    public const ushort String = 2;
    // [Op, 0 or 1]
    public const ushort Bool = 3;
    // [Op, string index (var name)]
    public const ushort Var = 4;
    // [Op, string index (func name), is Awaiting (0 or 1), number of args, instr indices...]
    public const ushort Func = 5;
    // [Op, float index, float index]
    public const ushort Mult = 6;
    public const ushort Div = 7;
    public const ushort Add = 8;
    public const ushort Sub = 9;
    public const ushort LessEquals = 10;
    public const ushort GreaterEquals = 11;
    public const ushort Less = 12;
    public const ushort Greater = 13;
    // [Op, [exp], [exp]]
    public const ushort Equal = 14;
    public const ushort NotEquals = 15;
    // [Op, (0 or 1), (0 or 1)]
    public const ushort And = 16;
    public const ushort Or = 17;
    public const ushort Not = 18;
    // [Op, string index (var name), [exp]]
    public const ushort Assign = 19;
    public const ushort MultAssign = 20;
    public const ushort DivAssign = 21;
    public const ushort AddAssign = 22;
    public const ushort SubAssign = 23;
    // [Op, float index (pause time or -1 == auto or -2 == close)]
    public const ushort Auto = 24;
    // [Op, section index] (Should be compiler only)
    public const ushort Goto = 25;
    // [Op, float index (speed multiplier)]
    public const ushort Speed = 26;
    // [Op, float index (pause time)]
    public const ushort Pause = 27;
}