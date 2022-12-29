namespace GameDialog.Compiler;

public enum VarType
{
    Undefined = 0,
    Float = 1,
    String = 2,
    Bool = 3,
    Void = 4
}

public enum OpCode
{
    Undefined,
    Float,
    String,
    Bool,
    // name string
    Var,
    // name string, number of arguments (float), expressions...
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
    // toggle (bool)
    Auto,
    // string
    BBCode,
    // Section Index
    Goto,
    NewLine,
    // multiplier (float)
    Speed,
    // SpeakerId (float), Name flag (bool), Name (expression), Portrait flag (bool), Portrait (expression), Mood flag (bool), Mood (expression)
    SpeakerSet,
    Choice
}
