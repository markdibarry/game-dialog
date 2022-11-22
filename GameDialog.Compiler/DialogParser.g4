parser grammar DialogParser;

@parser::header {#pragma warning disable 3021}
options { tokenVocab=DialogLexer; }

script: section+ EOF;
section: section_title section_body;
section_title: TITLE_EDGE NAME TITLE_EDGE NEWLINE+;
section_body: stmt+;

stmt
    : cond_stmt
    | tag NEWLINE+
    | line_stmt
    | INDENT stmt+ DEDENT
    ;

line_stmt
    :
        (speaker_ids | UNDERSCORE) (line_text | ml_text) NEWLINE+
        (INDENT choice_stmt* DEDENT)?
    ;
speaker_ids: NAME (NAME_SEPARATOR NAME)*;
ml_text: ML_EDGE (TEXT | tag)* ML_EDGE;
line_text: LINE_ENTER (TEXT | tag)*;

cond_stmt: if_stmt elseif_stmt* else_stmt?;
if_stmt: IF TAG_ENTER expression TAG_EXIT NEWLINE+ INDENT stmt+ DEDENT;
elseif_stmt : ELSEIF TAG_ENTER expression TAG_EXIT NEWLINE+ INDENT stmt+ DEDENT;
else_stmt: ELSE NEWLINE+ INDENT stmt+ DEDENT;

tag: TAG_ENTER (assignment | expression | attr_expression | BBCODE_NAME BBCODE_EXTRA_TEXT?) TAG_EXIT;
attr_expression: NAME (expression | assignment)+;

choice_stmt
    : choice_cond_stmt
    | CHOICE TEXT (tag? NEWLINE+ | NEWLINE+ INDENT stmt* DEDENT)
    ;
choice_cond_stmt: choice_if_stmt choice_elseif_stmt* choice_else_stmt?;
choice_if_stmt: IF TAG_ENTER expression TAG_EXIT NEWLINE+ INDENT choice_stmt+ DEDENT;
choice_elseif_stmt: ELSEIF TAG_ENTER expression TAG_EXIT NEWLINE+ INDENT choice_stmt+ DEDENT;
choice_else_stmt: ELSE NEWLINE+ INDENT choice_stmt+ DEDENT;

expression
    : OPEN_PAREN right=expression CLOSE_PAREN #ExpPara
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
assignment
    :
        NAME
        op=(OP_ASSIGN | OP_MULT_ASSIGN | OP_DIVIDE_ASSIGN | OP_ADD_ASSIGN | OP_SUB_ASSIGN) 
        right=expression
    ;
function : NAME '(' (expression (COMMA expression)*)? ')';
