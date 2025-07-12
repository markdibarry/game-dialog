parser grammar DialogParser;

options { tokenVocab=DialogLexer; }

script: section+ EOF;
section: sectionTitle sectionBody;
sectionTitle: TITLE NEWLINE+;
sectionBody: stmt+;

stmt
    : condStmt
    | tag NEWLINE+
    | lineStmt
    | INDENT stmt+ DEDENT;

lineStmt:
    (speakerIds | UNDERSCORE) lineText NEWLINE+
        choiceStmt*
    | (speakerIds | UNDERSCORE) mlText NEWLINE+
        DEDENT choiceStmt*;

textContent: (LINE_TEXT | tag)*;

lineText
    : LINE_ENTER textContent
    | ML_ENTER textContent ML_CLOSE;
mlText:
    ML_ENTER
        textContent NEWLINE
        INDENT textContent
        (NEWLINE textContent)*
    ML_CLOSE;

hashName: HASH NAME;
hashAssignment: hashName OP_ASSIGN expression;
hashCollection: (hashName | hashAssignment)+;

speakerCollection: NAME hashCollection;
speakerId: NAME;
speakerIds: speakerId (NAME_SEPARATOR speakerId)*;

condStmt: ifStmt elseifStmt* elseStmt?;
ifStmt:
    IF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT stmt+ DEDENT;
elseifStmt:
    ELSEIF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT stmt+ DEDENT;
elseStmt:
    ELSE NEWLINE+
    INDENT stmt+ DEDENT;

tag:
    OPEN_BRACKET
    (
        assignment
        | expression
        | attrExpression
        | hashCollection
        | speakerCollection
        | BBCODE_NAME BBCODE_EXTRA_TEXT?
    )
    CLOSE_BRACKET;
attrExpression: NAME (expression | assignment)+;

choiceStmt:
    choiceCondStmt
    | CHOICE textContent NEWLINE+ (INDENT stmt* DEDENT)?;
choiceCondStmt: choiceIfStmt choiceElseifStmt* choiceElseStmt?;
choiceIfStmt:
    IF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT choiceStmt+ DEDENT;
choiceElseifStmt:
    ELSEIF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT choiceStmt+ DEDENT;
choiceElseStmt:
    ELSE NEWLINE+
    INDENT choiceStmt+ DEDENT;

expression:
    OPEN_PAREN right=expression CLOSE_PAREN #ExpPara
    | op=OP_NOT right=expression #ExpNot
    | left=expression op=(OP_MULT | OP_DIVIDE) right=expression #ExpMultDiv
    | left=expression op=(OP_ADD | OP_SUB) right=expression #ExpAddSub
    | left=expression op=(OP_LESS_EQUALS | OP_GREATER_EQUALS | OP_LESS | OP_GREATER) right=expression #ExpComp
    | left=expression op=(OP_EQUALS | OP_NOT_EQUALS) right=expression #ExpEqual
    | left=expression op=(OP_AND | OP_OR) right=expression #ExpAndOr
    | FLOAT #ConstFloat
    | BOOL #ConstBool
    | NAME #ConstVar
    | STRING #ConstString
    | function #ConstFunc;
assignment:
    NAME
    op=(OP_ASSIGN | OP_MULT_ASSIGN | OP_DIVIDE_ASSIGN | OP_ADD_ASSIGN | OP_SUB_ASSIGN)
    right=expression;
function : (AWAIT)? NAME '(' (expression (COMMA expression)*)? ')';
