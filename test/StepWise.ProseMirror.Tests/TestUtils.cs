using System.Text.RegularExpressions;
using Xunit;

using StepWise.Prose.Model;


namespace StepWise.Prose.Test;

public static class TestUtils {
    public static void ist(bool @bool) =>
        Assert.True(@bool);
    public static void ist<A, B>(A a, B b) =>
        Assert.Equal<object?>(b, a);
    public static void ist<A, B>(A a, B b, Func<A, B, bool> f) =>
        Assert.True(f(a, b));

    public static bool eq(Node a, Node b) =>
        a.Eq(b);

    public static void ist<A>(A? a) =>
        Assert.NotNull(a);

    public static void istThrows(Action f) =>
        Assert.ThrowsAny<Exception>(f);

    /// <summary>Asserts that <paramref name="f"/> throws an exception whose message matches
    /// <paramref name="pattern"/>, where <c>*</c> matches any run of characters.</summary>
    public static Exception ThrowsWithMessage(Action f, string pattern) {
        var ex = Assert.ThrowsAny<Exception>(f);
        var regex = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
        Assert.Matches(new Regex(regex, RegexOptions.Singleline), ex.Message);
        return ex;
    }
}
