namespace GameDialog.Compiler;

public enum VarType
{
    Undefined = 0,
    Float = 1,
    String = 2,
    Bool = 3,
    Void = 4
}

public enum InstructionType
{
    Unknown,
    Float,
    String,
    Bool,
    // Variable index
    Var,
    // Variable index, number of arguments (float), expressions...
    Func,
    // float, float
    Mult,
    Div,
    Add,
    Sub,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    // expression, expression
    Equals,
    NotEquals,
    // bool, bool
    And,
    Or,
    Not,
    // Variable index, expression
    Assign,
    MultAssign,
    DivAssign,
    AddAssign,
    SubAssign,

    // bool
    Auto,
    // string
    BBCode,
    // Section Index
    Goto,
    NewLine,
    // float
    Speed,
    // SpeakerId (float), Name (string), Mood (string), Portrait (string)
    SpeakerSet,
    // SpeakerId (string)
    SpeakerGet,
}
