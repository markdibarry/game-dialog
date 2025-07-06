parser grammar DialogParser;

options { tokenVocab=DialogLexer; }

script: section+ EOF;
section: section_title section_body;
section_title: TITLE NEWLINE+;
section_body: stmt+;

stmt
    : cond_stmt
    | tag NEWLINE+
    | line_stmt
    | INDENT stmt+ DEDENT;

line_stmt:
    (speaker_ids | UNDERSCORE) line_text NEWLINE+
        choice_stmt*
    | (speaker_ids | UNDERSCORE) ml_text NEWLINE+
        DEDENT choice_stmt*;

text_content: (LINE_TEXT | tag)*;

line_text
    : LINE_ENTER text_content
    | ML_ENTER text_content ML_CLOSE;
ml_text:
    ML_ENTER
        text_content NEWLINE
        INDENT text_content
        (NEWLINE text_content)*
    ML_CLOSE;

hash_name: HASH NAME;
hash_assignment: hash_name OP_ASSIGN expression;
hash_collection: (hash_name | hash_assignment)+;

speaker_collection: NAME hash_collection;
speaker_id: NAME;
speaker_ids: speaker_id (NAME_SEPARATOR speaker_id)*;

cond_stmt: if_stmt elseif_stmt* else_stmt?;
if_stmt:
    IF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT stmt+ DEDENT;
elseif_stmt:
    ELSEIF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT stmt+ DEDENT;
else_stmt:
    ELSE NEWLINE+
    INDENT stmt+ DEDENT;

tag:
    OPEN_BRACKET
    (
        assignment
        | expression
        | attr_expression
        | hash_collection
        | speaker_collection
        | BBCODE_NAME BBCODE_EXTRA_TEXT?
    )
    CLOSE_BRACKET;
attr_expression: NAME (expression | assignment)+;

choice_stmt:
    choice_cond_stmt
    | CHOICE text_content NEWLINE+ (INDENT stmt* DEDENT)?;
choice_cond_stmt: choice_if_stmt choice_elseif_stmt* choice_else_stmt?;
choice_if_stmt:
    IF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT choice_stmt+ DEDENT;
choice_elseif_stmt:
    ELSEIF OPEN_BRACKET expression CLOSE_BRACKET NEWLINE+
    INDENT choice_stmt+ DEDENT;
choice_else_stmt:
    ELSE NEWLINE+
    INDENT choice_stmt+ DEDENT;

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
