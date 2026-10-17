using Workers;

namespace LanguageProfile;

public static class Queries
{
    public static Response Run()
    {
        var changing = new[] { 1, 2 };
        Func<int, int> ReplaceSource()
        {
            changing = new[] { 9 };
            return value => value * 2;
        }
        var named = changing.Select(selector: ReplaceSource()).ToArray();
        var conditional = changing?.Select(value => value * 2).ToArray();
        object[] numbers = { 1, 2, 3, 4 };
        object[] formats = { QueueContentType.Json, QueueContentType.V8 };
        var nativeEnums = from QueueContentType format in formats select format;
        var typedGroups = from IGrouping<int, int> g in new[] { 1, 2, 3 }.GroupBy(value => value % 2)
                          orderby g.Key
                          select g.Count();
        var ordered = new[] { 3, 1, 2 }.OrderBy(value => value);
        var typedOrdered = from IOrderedEnumerable<int> items in new[] { ordered }
                           from int value in items select value;
        var groups = from int value in numbers
                     group value * 10 by new { Odd = value % 2 } into g
                     orderby g.Key.Odd descending
                     select new { odd = g.Key.Odd, values = g.ToArray() };
        var joined = from int a in numbers
                     join int b in numbers on new { Odd = a % 2 } equals new { Odd = b % 2 }
                     let sum = a + b
                     where sum > 5
                     orderby sum descending, a ascending
                     select sum;
        var groupedJoin = from int a in numbers
                          join int b in numbers on a equals b into matches
                          select new { a, count = matches.Count() } into row
                          where row.a > 2
                          select row;
        var words = from word in new[] { "b", "A", "a", "aa" }
                    orderby word.Length descending, word
                    select word;
        return Response.Json(new { named, conditional, groups = groups.ToArray(), joined = joined.ToArray(),
            groupedJoin = groupedJoin.ToArray(), words = words.ToArray(), nativeEnums = nativeEnums.ToArray(),
            typedGroups = typedGroups.ToArray(), typedOrdered = typedOrdered.ToArray() });
    }
}
