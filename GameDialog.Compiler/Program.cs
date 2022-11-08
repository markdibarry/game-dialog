using GameDialog.Compiler;

string docPath = "test.txt";
DocumentManager documentManager = new();
documentManager.Documents[docPath] = new(docPath)
{
    Text = File.ReadAllText(docPath)
};
documentManager.Compile();
