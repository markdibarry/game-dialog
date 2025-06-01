using System.Text;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleChoices(Choice_stmtContext[] context)
    {
        List<int> choiceSet = [(int)InstructionType.Choice];
        _scriptData.Instructions.Add(choiceSet);
        ResolveStatements(_scriptData.Instructions.Count - 1);
        AddChoiceSet(context, choiceSet);
    }

    private void AddChoiceSet(Choice_stmtContext[] context, List<int> choiceSet)
    {
        _nestLevel++;

        foreach (Choice_stmtContext choiceStmt in context)
        {
            if (choiceStmt.CHOICE() != null)
                AddChoice(choiceStmt, choiceSet);
            else
                HandleChoiceCondition(choiceStmt.choice_cond_stmt(), choiceSet);

            LowerUnresolvedStatements();
        }

        _unresolvedStmts.Add((_nestLevel, choiceSet));
        _nestLevel--;
    }

    private void AddChoice(Choice_stmtContext choiceStmt, List<int> choiceSet)
    {
        StringBuilder sb = new();
        HandleLineText(sb, choiceStmt.choice_text().children);
        int stringIndex = _scriptData.Strings.GetOrAdd(sb.ToString());
        List<int> choice = [(int)ChoiceOp.Choice, -1, stringIndex];
        _unresolvedStmts.Add((_nestLevel, choice));

        foreach (StmtContext stmt in choiceStmt.stmt())
            Visit(stmt);

        choiceSet.AddRange(choice);
    }

    private void HandleChoiceCondition(Choice_cond_stmtContext choiceCond, List<int> choiceSet)
    {
        // if
        Choice_if_stmtContext ifStmt = choiceCond.choice_if_stmt();
        choiceSet.AddRange([(int)ChoiceOp.If, _scriptData.Instructions.Count]);
        _scriptData.Instructions.Add(GetInstrStmt(ifStmt.expression(), VarType.Bool));
        AddChoiceSet(ifStmt.choice_stmt(), choiceSet);

        // else if
        foreach (Choice_elseif_stmtContext elseifStmt in choiceCond.choice_elseif_stmt())
        {
            choiceSet.AddRange([(int)ChoiceOp.ElseIf, _scriptData.Instructions.Count]);
            _scriptData.Instructions.Add(GetInstrStmt(ifStmt.expression(), VarType.Bool));
            AddChoiceSet(elseifStmt.choice_stmt(), choiceSet);
        }

        // else
        if (choiceCond.choice_else_stmt() != null)
        {
            choiceSet.AddRange([(int)ChoiceOp.Else]);
            Choice_else_stmtContext elseStmt = choiceCond.choice_else_stmt();
            AddChoiceSet(elseStmt.choice_stmt(), choiceSet);
        }

        choiceSet.AddRange([(int)ChoiceOp.EndIf]);
    }
}
