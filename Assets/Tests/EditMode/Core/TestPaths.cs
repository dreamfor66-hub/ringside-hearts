using System.IO;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>Finds project files from both the Unity Test Runner and the dotnet harness.</summary>
    public static class TestPaths
    {
        public static string DataFile(string name) => Path.Combine(ProjectRoot(), "Assets", "Resources", "Data", name);

        static string ProjectRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), Path.GetDirectoryName(typeof(TestPaths).Assembly.Location) })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) &&
                        Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Unity project root (Assets + ProjectSettings) not found");
        }
    }
}
