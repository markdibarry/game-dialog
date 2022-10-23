parser grammar DialogParser;

@parser::header {#pragma warning disable 3021}
options { tokenVocab=DialogLexer; }

script: section+ EOF;
section: section_title section_body;
section_title: TITLE_EDGE NAME TITLE_EDGE NEWLINE+;
section_body: stmt+;
stmt
    : if_stmt
    | exp_stmt* line_stmt
    | line_stmt
    | exp_stmt+
    | INDENT stmt+ DEDENT;
line_stmt
    :
        NAME (line_text | ml_text) NEWLINE+
        (INDENT choice_stmt* DEDENT)?
    ;
exp_stmt: (expression | assignment) NEWLINE+;
ml_text: ML_EDGE (TEXT | tag)* ML_EDGE;
line_text: LINE_ENTER (TEXT | tag)*;
if_stmt
    :
        IF expression NEWLINE+ INDENT stmt+ DEDENT
        (ELSEIF expression NEWLINE+ INDENT stmt+ DEDENT)*
        (ELSE INDENT stmt+ DEDENT)?
    ;
choice_stmt
    : choice_if_stmt 
    | (CHOICE TEXT (tag? NEWLINE+ | NEWLINE+ INDENT stmt* DEDENT))
    ;
choice_if_stmt
    :
        IF expression NEWLINE+ INDENT choice_stmt DEDENT
        (ELSEIF expression NEWLINE+ INDENT choice_stmt DEDENT)*
        (ELSE INDENT choice_stmt DEDENT)?
    ;
expression
    : OPEN_PAREN expression CLOSE_PAREN
    | OP_NOT expression
    | expression (OP_MULT | OP_DIVIDE) expression
    | expression (OP_ADD | OP_SUB) expression
    | expression (OP_LESS_EQUALS | OP_GREATER_EQUALS | OP_LESS | OP_GREATER) expression
    | expression (OP_EQUALS | OP_NOT_EQUALS) expression
    | expression (OP_AND | OP_OR) expression
    | constant;
assignment
    :
        NAME
        (OP_ASSIGNMENT | OP_MULT_ASSIGN | OP_DIVIDE_ASSIGN | OP_ADD_ASSIGN | OP_SUB_ASSIGN) 
        expression
    ;
constant
    : NUMBER
    | BOOL
    | NAME
    | STRING;
tag: TAG_EDGE (assignment | expression)+ TAG_EDGE;