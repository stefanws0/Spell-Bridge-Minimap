using System;
using System.Linq;
using System.Reflection;

namespace SpellBrigadeMinimap.Tests
{
    /// <summary>
    /// Runs every public static parameterless void method on classes whose name ends in "Tests".
    /// Pass name fragments as arguments to run a subset. Exit code 0 = all passed.
    /// </summary>
    public static class TestRunner
    {
        public static int Main(string[] args)
        {
            var tests = typeof(TestRunner).Assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Tests"))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                .Where(m => args.Length == 0 || args.Any(a => (m.DeclaringType.Name + "." + m.Name).Contains(a)))
                .OrderBy(m => m.DeclaringType.Name).ThenBy(m => m.Name)
                .ToList();

            int failed = 0;
            foreach (var m in tests)
            {
                string name = m.DeclaringType.Name + "." + m.Name;
                try
                {
                    m.Invoke(null, null);
                    Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("FAIL " + name + ": " + e.InnerException.Message);
                }
            }
            Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
            return failed == 0 && tests.Count > 0 ? 0 : 1;
        }
    }

    public static class Check
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Near(float expected, float actual, string what, float tolerance = 1e-3f)
        {
            if (!(Math.Abs(expected - actual) <= tolerance))
                throw new Exception($"{what}: expected {expected}, got {actual}");
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!Equals(expected, actual))
                throw new Exception($"{what}: expected {expected}, got {actual}");
        }
    }
}
