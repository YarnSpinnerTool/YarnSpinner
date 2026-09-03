#nullable enable

namespace Yarn.Analyser;

using System;
using System.Collections.Generic;
using System.IO;
using Yarn.Shared;

#pragma warning disable RS1035

public class FileDebugWriter
{
    public static void WriteGeneratedFile(string input, string name)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TimsLogs", name);
        File.WriteAllText(path, input);
    }
}

public class EmergencyLogger
{
    public static void ExceptionLog(Exception ex, string? message = null, bool fullEx = false)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TimsLogs", $"{Path.GetRandomFileName()}.exception.txt");
        
        List<string> lines = [];
        if (message == null)
        {
            lines.Add($"Exception: {ex.Message}\n");
        }
        else
        {
            lines.Add($"{message}: {ex.Message}\n");
        }

        if (fullEx)
        {
            var s = new System.Diagnostics.StackTrace();
            lines.Add(s.ToString());
        }

        File.WriteAllLines(path, lines);
    }
}

public class BetterLogger: ILogger
{
    private int depth = 0;
    private string path;

    public BetterLogger()
    {
        path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TimsLogs", $"log.txt");
    }

    public BetterLogger(string name)
    {
        path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TimsLogs", $"log-{name}.txt");
    }

    public void Write(object text)
    {
        using (StreamWriter writer = File.AppendText(path))
        {
            var tabs = new String('\t', depth);
            writer.Write(tabs + text);
        }
    }
    public void WriteLine(object text)
    {
        using (StreamWriter writer = File.AppendText(path))
        {
            var tabs = new String('\t', depth);
            writer.WriteLine(tabs + text);
        }
    }
    public void WriteException(System.Exception ex, string? message = null)
    {
        using (StreamWriter writer = File.AppendText(path))
        {
            var tabs = new String('\t', depth);
            if (message == null)
            {
                writer.WriteLine($"{tabs}Exception: {ex.Message}");
            }
            else
            {
                writer.WriteLine($"{tabs}{message}: {ex.Message}");
            }
        }
    }
    public void Inc()
    {
        depth +=1 ;
    }
    public void Dec()
    {
        depth = Math.Max(depth - 1, 0);
    }
    public void SetDepth(int depth)
    {
        this.depth = Math.Max(depth, 0);
    }

    public void Dispose() {}
}

public class JSONWriter
{
    public static void WriteJSON(string assembly, string input, string folder, bool debugWrite = false)
    {
        var path = System.IO.Path.Combine(folder, "ProjectSettings", "Packages", "dev.yarnspinner", $"{assembly}.generated.ysls.json");
        File.WriteAllText(path, input);

        if (debugWrite)
        {
            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TimsLogs", $"{assembly}.ysls.json");
            File.WriteAllText(path, input);
        }
    }
}

#pragma warning restore RS1035