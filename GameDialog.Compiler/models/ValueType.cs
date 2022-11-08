namespace GameDialog.Compiler;

public enum VarType
{
    Undefined = 0,
    Float = 1,
    String = 2,
    Bool = 3,
}

public enum ExpType
{
    Unknown,
    Float,
    String,
    Bool,
    Var,
    Func,
    Mult,
    Div,
    Add,
    Sub,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    Equals,
    NotEquals,
    And,
    Or,
    Not,
    Assign,
    MultAssign,
    DivAssign,
    AddAssign,
    SubAssign,
}
