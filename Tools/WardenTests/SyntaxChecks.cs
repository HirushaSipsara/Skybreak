using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

static class SyntaxChecks
{
    public static void Run()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        int count=0;
        foreach(var folder in new[]{"Assets/Scripts/AI/CrystalEchoWarden","Assets/Editor/CrystalEchoWarden"})
        {
            string dir=Path.Combine(root,folder);
            if(!Directory.Exists(dir))continue;
            foreach(var file in Directory.GetFiles(dir,"*.cs",SearchOption.AllDirectories))
            {
                var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9),file);
                foreach(var d in tree.GetDiagnostics())if(d.Severity==DiagnosticSeverity.Error)throw new Exception(d.ToString());
                count++;
            }
        }
        Console.WriteLine("SYNTAX PASS: "+count+" C# files. Unity API type checking and Play Mode remain pending.");
    }
}
