// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.Analyser;

using System;
using System.Collections.Generic;
using System.IO;
using Yarn.Shared;

#pragma warning disable RS1035

/// <summary>
/// This class exists to provide a way to write the generated C# files to disk in a place they can be viewed.
/// You almost certainly do not want or need this class.
/// </summary>
public static class FileDebugWriter
{
    /// <summary>
    /// Writes the generated file to disk.
    /// Technically just writes any provided string to disk...
    /// You almost certainly do no want or need this method.
    /// </summary>
    /// <param name="input">contents of the file</param>
    /// <param name="name">name of the file</param>
    /// <param name="configuration">the config file that determins if it will or will not write the file to disk. Turning this on WILL fill your drive.</param>
    public static void WriteGeneratedFile(string input, string name, Yarn.HostAnalysis.AnalysisConfiguration configuration)
    {
        if (configuration.WriteDebugFiles)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yarn.sourcegen.logs", name);
            File.WriteAllText(path, input);
        }
    }
}

/// <summary>
/// Logs out a file with the contents of an exception encountered during source analysis and generation.
/// You almost certainly do not want or need this class.
/// </summary>
public static class EmergencyLogger
{
    /// <summary>
    /// Logs out the exception to disk.
    /// You almost certainly do not want or need this method.
    /// </summary>
    public static void ExceptionLog(Exception ex, string? message = null, bool fullEx = false)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yarn.sourcegen.logs", $"{Path.GetRandomFileName()}.exception.txt");
        
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

/// <summary>
/// Performs the most basic file logging during source analysis and generation.
/// You almost certainly do not want or need this class.
/// </summary>
public class BasicFileLogger: ILogger
{
    private int depth = 0;
    private readonly string path;

    public BasicFileLogger()
    {
        path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yarn.sourcegen.logs", $"log.txt");
    }

    public BasicFileLogger(string name)
    {
        path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yarn.sourcegen.logs", $"log-{name}.txt");
    }
    
    /// <inheritdoc />
    public void WriteLine(object text)
    {
        using (StreamWriter writer = File.AppendText(path))
        {
            var tabs = new String(' ', depth);
            writer.WriteLine(tabs + text);
        }
    }
    /// <inheritdoc />
    public void WriteException(System.Exception ex, string? message = null)
    {
        using (StreamWriter writer = File.AppendText(path))
        {
            var tabs = new String(' ', depth);
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
    /// <inheritdoc />
    public void Inc()
    {
        depth += 1;
    }
    /// <inheritdoc />
    public void Dec()
    {
        depth = Math.Max(depth - 1, 0);
    }
}

/// <summary>
/// Class for writing the YSLS JSON file to disk in the project setting directory.
/// You almost certainly do not want or need this class.
/// </summary>
public class JSONWriter
{
    /// <summary>
    /// Performs the actual writing of the provided JSON to the project settings folder
    /// </summary>
    /// <param name="assembly">the name of the current assembly. Each assembly gets its own YSLS file</param>
    /// <param name="input">the contents of the json</param>
    /// <param name="folder">the root folder of the unity project</param>
    /// <param name="debugWrite">should a copy also be written to the debug location? This isn't really useful outside of developing the source analysers and generators.</param>
    public static void WriteJSON(string assembly, string input, string folder, bool debugWrite = false)
    {
        var path = System.IO.Path.Combine(folder, "ProjectSettings", "Packages", "dev.yarnspinner", $"{assembly}.generated.ysls.json");
        File.WriteAllText(path, input);

        if (debugWrite)
        {
            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yarn.sourcegen.logs", $"{assembly}.ysls.json");
            File.WriteAllText(path, input);
        }
    }
}

#pragma warning restore RS1035