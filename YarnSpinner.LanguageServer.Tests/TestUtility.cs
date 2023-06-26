using System;
using System.IO;
using System.Linq;
using Xunit;

public static class TestUtility
{
    public static string PathToTestWorkspace => Path.Combine(PathToTestData, "TestWorkspace");

    public static string PathToTestData
    {
        get
        {
            var context = AppContext.BaseDirectory;

            var directoryContainingProject = GetParentDirectoryContainingFile(new DirectoryInfo(context), "*.csproj");

            if (directoryContainingProject != null)
            {
                return Path.Combine(directoryContainingProject.FullName, "TestData");
            }
            else
            {
                throw new InvalidOperationException("Failed to find path containing .csproj!");
            }

            static DirectoryInfo? GetParentDirectoryContainingFile(DirectoryInfo directory, string filePattern)
            {
                var current = directory;
                do
                {
                    if (current.EnumerateFiles(filePattern).Any())
                    {
                        return current;
                    }
                    current = current.Parent;
                } while (current != null);

                return null;
            }
        }
    }
}
