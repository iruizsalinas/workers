namespace Workers.Compiler.Tests;

public sealed class TaskValidationRegressionTests
{
    [Fact]
    public void WhenAllRejectsInvalidInputsBeforeReturningATask() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var missingCaught = false;
                var elementCaught = false;
                var enumerationCaught = false;
                Task<int[]> pending = null;
                try { Task<int>[] missing = null; pending = Task.WhenAll(missing); }
                catch (System.Exception) { missingCaught = true; }
                if (pending is not null) { try { await pending; } catch (System.Exception) { } }
                pending = null;
                try { Task<int>[] tasks = [Task.FromResult(1), null]; pending = Task.WhenAll(tasks); }
                catch (System.Exception) { elementCaught = true; }
                if (pending is not null) { try { await pending; } catch (System.Exception) { } }
                pending = null;
                try { pending = Task.WhenAll(Broken()); }
                catch (System.Exception) { enumerationCaught = true; }
                if (pending is not null) { try { await pending; } catch (System.Exception) { } }
                return new { missingCaught, elementCaught, enumerationCaught };
            }
            private static IEnumerable<Task<int>> Broken()
            {
                yield return Task.FromResult(1);
                throw new System.Exception("enumeration failed");
            }
        }
        """);
}
