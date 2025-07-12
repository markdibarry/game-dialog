using System.Text;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleChoices(ChoiceStmtContext[] context)
    {
        List<int> choiceSet = [InstructionType.Choice];
        _scriptData.Instructions.Add(choiceSet);
        ResolveStatements(_scriptData.Instructions.Count - 1);
        AddChoiceSet(context, choiceSet);
    }

    private void AddChoiceSet(ChoiceStmtContext[] context, List<int> choiceSet)
    {
        _nestLevel++;

        foreach (ChoiceStmtContext choiceStmt in context)
        {
            if (choiceStmt.CHOICE() != null)
                AddChoice(choiceStmt, choiceSet);
            else
                HandleChoiceCondition(choiceStmt.choiceCondStmt(), choiceSet);

            LowerUnresolvedStatements();
        }

        _unresolvedStmts.Add((_nestLevel, choiceSet));
        _nestLevel--;
    }

    private void AddChoice(ChoiceStmtContext choiceStmt, List<int> choiceSet)
    {
        StringBuilder sb = new();
        HandleTextContent(sb, choiceStmt.textContent());
        int stringIndex = _scriptData.Strings.GetOrAdd(sb.ToString());
        _scriptData.DialogStringIndices.Add(stringIndex);
        List<int> choice = [ChoiceOp.Choice, -1, stringIndex];
        _unresolvedStmts.Add((_nestLevel, choice));

        foreach (StmtContext stmt in choiceStmt.stmt())
            Visit(stmt);

        choiceSet.AddRange(choice);
    }

    private void HandleChoiceCondition(ChoiceCondStmtContext choiceCond, List<int> choiceSet)
    {
        // if
        ChoiceIfStmtContext ifStmt = choiceCond.choiceIfStmt();
        choiceSet.AddRange([ChoiceOp.If, _scriptData.Instructions.Count]);
        _scriptData.Instructions.Add(GetInstrStmt(ifStmt.expression(), VarType.Bool));
        AddChoiceSet(ifStmt.choiceStmt(), choiceSet);

        // else if
        foreach (ChoiceElseifStmtContext elseifStmt in choiceCond.choiceElseifStmt())
        {
            choiceSet.AddRange([ChoiceOp.ElseIf, _scriptData.Instructions.Count]);
            _scriptData.Instructions.Add(GetInstrStmt(ifStmt.expression(), VarType.Bool));
            AddChoiceSet(elseifStmt.choiceStmt(), choiceSet);
        }

        // else
        if (choiceCond.choiceElseStmt() != null)
        {
            choiceSet.AddRange([ChoiceOp.Else]);
            ChoiceElseStmtContext elseStmt = choiceCond.choiceElseStmt();
            AddChoiceSet(elseStmt.choiceStmt(), choiceSet);
        }

        choiceSet.AddRange([ChoiceOp.EndIf]);
    }
}
