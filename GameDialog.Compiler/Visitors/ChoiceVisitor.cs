namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleChoices(DialogParser.Choice_stmtContext[] context)
    {
        List<int> choiceSet = new();
        _dialogScript.ChoiceSets.Add(choiceSet);
        ResolveStatements(new GoTo(StatementType.Choice, _dialogScript.ChoiceSets.Count - 1));
        AddChoiceSet(context, choiceSet);
    }

    private void AddChoiceSet(DialogParser.Choice_stmtContext[] context, List<int> choiceSet)
    {
        _nestLevel++;
        // follows pattern
        // -1 = choice index
        // -2 = go to index
        // -3 = unresolved index
        // > -1 = instruction index, next is go to if condition fails
        foreach (var choiceStmt in context)
        {
            if (choiceStmt.TEXT() != null)
                AddChoice(choiceStmt, choiceSet);
            else
                HandleChoiceCondition(choiceStmt.choice_cond_stmt(), choiceSet);
        }
        _nestLevel--;
    }

    private void AddChoice(DialogParser.Choice_stmtContext choiceStmt, List<int> choiceSet)
    {
        Choice choice = new() { Text = choiceStmt.TEXT().GetText()};
        _dialogScript.Choices.Add(choice);
        choiceSet.AddRange(new[] { -1, _dialogScript.Choices.Count - 1 });
        _unresolvedStmts.Add((_nestLevel, choice));
        foreach (var stmt in choiceStmt.stmt())
            Visit(stmt);
    }

    private void HandleChoiceCondition(DialogParser.Choice_cond_stmtContext choiceCond, List<int> choiceSet)
    {
        // if
        var ifStmt = choiceCond.choice_if_stmt();
        var ifExp = _expressionVisitor.GetExpression(ifStmt.expression(), VarType.Bool);
        List<int> unresolvedClauses = new();
        _dialogScript.Instructions.Add(ifExp);
        choiceSet.AddRange(new[] { _dialogScript.Instructions.Count - 1, -3 });
        int unresolvedFallbackIndex = choiceSet.Count - 1;
        AddChoiceSet(ifStmt.choice_stmt(), choiceSet);
        LowerUnresolvedStatements();
        // else if
        foreach (var elseifStmt in choiceCond.choice_elseif_stmt())
        {
            // close if clause with go-to
            choiceSet.AddRange(new[] { -2, -3 });
            unresolvedClauses.Add(choiceSet.Count - 1);

            var elseifExp = _expressionVisitor.GetExpression(elseifStmt.expression(), VarType.Bool);
            _dialogScript.Instructions.Add(elseifExp);
            choiceSet[unresolvedFallbackIndex] = choiceSet.Count;
            choiceSet.AddRange(new[] { _dialogScript.Instructions.Count - 1, -3 });
            unresolvedFallbackIndex = choiceSet.Count - 1;
            AddChoiceSet(elseifStmt.choice_stmt(), choiceSet);
            LowerUnresolvedStatements();
        }

        // else
        if (choiceCond.choice_else_stmt() != null)
        {
            // close elseif clause with go-to
            choiceSet.AddRange(new[] { -2, -3 });
            unresolvedClauses.Add(choiceSet.Count - 1);

            var elseStmt = choiceCond.choice_else_stmt();
            choiceSet[unresolvedFallbackIndex] = choiceSet.Count;
            unresolvedFallbackIndex = -3;
            AddChoiceSet(elseStmt.choice_stmt(), choiceSet);
            LowerUnresolvedStatements();
        }
        if (unresolvedFallbackIndex != -3)
            choiceSet[unresolvedFallbackIndex] = choiceSet.Count;
        foreach (var clauseIndex in unresolvedClauses)
            choiceSet[clauseIndex] = choiceSet.Count;
    }
}
