namespace Workers.Compiler.Tests;

public sealed class ExceptionConstructionRegressionTests
{
    [Fact]
    public void PrimaryConstructorsPassMessageAndInnerExceptionToTheBase() => DifferentialRunner.Equal("""
        public sealed class NotFound(string message) : System.Exception(message);
        public sealed class Wrapped(string message, System.Exception inner) : System.Exception(message, inner);
        public sealed class ApiError(int status, string message) : System.Exception(message)
        {
            public int Status { get; } = status;
        }
        public sealed class Plain(int code) : System.Exception
        {
            public int Code => code;
        }
        public static class Test
        {
            public static object Run()
            {
                var results = new List<string>();
                try { throw new NotFound("missing"); }
                catch (NotFound e) { results.Add(e.Message); }
                try { throw new Wrapped("outer", new System.InvalidOperationException("inner")); }
                catch (Wrapped e) { results.Add(e.Message + "|" + e.InnerException?.Message); }
                try { throw new ApiError(404, "nf"); }
                catch (ApiError e) when (e.Status == 404) { results.Add(e.Status + e.Message); }
                try { throw new Plain(7); }
                catch (Plain e) { results.Add(e.Code + "|" + e.Message); }
                return results;
            }
        }
        """);

    [Fact]
    public void PrimaryConstructorBaseArgumentsAreComputedOnce() => DifferentialRunner.Equal("""
        public sealed class Counted(string message) : System.Exception(Test.Next(message));
        public static class Test
        {
            public static int Calls;
            public static string Next(string value) { Calls++; return value + Calls; }
            public static object Run()
            {
                var error = new Counted("m");
                return new { Message = error.Message, Calls };
            }
        }
        """);

    [Fact]
    public void DerivedUserExceptionsKeepTheMessageTheirBaseComputes() => DifferentialRunner.Equal("""
        public class AppError(string detail) : System.Exception("app: " + detail);
        public sealed class CodeError(int code) : AppError($"code {code}")
        {
            public int Code => code;
        }
        public class ClassicError : System.Exception
        {
            public ClassicError(string reason) : base("classic: " + reason) { }
        }
        public sealed class ClassicChild : ClassicError
        {
            public ClassicChild(int code) : base("child " + code) { }
        }
        public sealed class InheritedMessage : ClassicError
        {
            public InheritedMessage(string message) : base(message) { }
        }
        public static class Test
        {
            public static object Run()
            {
                var results = new List<string>();
                try { throw new CodeError(5); }
                catch (AppError e) { results.Add(e.Message + "|" + (e is CodeError c ? c.Code : -1)); }
                try { throw new ClassicChild(3); }
                catch (ClassicError e) { results.Add(e.Message); }
                try { throw new InheritedMessage("x"); }
                catch (ClassicError e) { results.Add(e.Message); }
                return results;
            }
        }
        """);
}
