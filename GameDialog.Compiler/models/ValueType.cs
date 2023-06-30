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
    Var, // name string
    Func, // name string, number of arguments (float), expressions...
    Mult, // float, float
    Div,
    Add,
    Sub,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    Equals, // expression, expression
    NotEquals,
    And, // bool, bool
    Or,
    Not,
    Assign, // Variable index, expression
    MultAssign,
    DivAssign,
    AddAssign,
    SubAssign,
    Auto, // toggle (bool)
    Goto, // Section Index
    NewLine,
    Speed, // multiplier (float)
    Pause, // time (float)
    SpeakerSet,
    SpeakerSetName, // SpeakerId (float), Name (expression)
    SpeakerSetMood, // SpeakerId (float), Mood (expression)
    SpeakerSetPortrait, // SpeakerId (float), Portrait (expression)
    Choice
}
