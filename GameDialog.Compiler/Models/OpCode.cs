namespace GameDialog.Compiler;

public enum OpCode
{
    Undefined,
    Float, // [Op, float index]
    String, // [Op, string index]
    Bool, // [Op, 0 or 1]
    Var, // [Op, string index (var name)]
    Func, // [Op, string index (func name), number of args, instr indices...]
    Mult, // [Op, float index, float index]
    Div,
    Add,
    Sub,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    Equals, // [Op, [exp], [exp]]
    NotEquals,
    And, // [Op, (0 or 1), (0 or 1)]
    Or,
    Not,
    Assign, // [Op, string index (var name), [exp]]
    MultAssign,
    DivAssign,
    AddAssign,
    SubAssign,
    Auto, // [Op, float index (pause time or -1 == auto or -2 == close)]
    Goto, // [Op, section index] (Should be compiler only)
    NewLine,
    Speed, // [Op, float index (speed multiplier)]
    Pause, // [Op, float index (pause time)]
    GetName // [Op, speaker index]
}
