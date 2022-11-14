using GameDialog.Compiler;

string docPath = "test.txt";
DocumentManager documentManager = new();
documentManager.Documents[docPath] = new(docPath, File.ReadAllText(docPath));
documentManager.Compile();
