using System.Globalization;

public static class VerificationCollectionScenarios
{
    public static Dictionary<string, string> Run() => new()
    {
        ["reservationAliases"] = ReservationAliases(),
        ["streamDisposal"] = StreamDisposal(),
        ["sensorExtrema"] = SensorExtrema(),
        ["tenantJoinLifecycle"] = TenantJoinLifecycle(),
        ["seededLedger"] = SeededLedger(),
        ["comparisonLifecycle"] = ComparisonLifecycle(),
        ["nullQueryCallbacks"] = NullQueryCallbacks(),
        ["tenantSetAudit"] = TenantSetAudit(),
        ["reservationIdentity"] = ReservationIdentity(),
        ["reservationEqualityCallbacks"] = ReservationEqualityCallbacks()
    };

    private static string ReservationAliases()
    {
        var reservations = new List<int> { 1, 2, 3 };
        IEnumerable<int> alias = reservations;
        reservations.AddRange(alias);
        var result = string.Join(",", reservations.Select(value => value.ToString()));
        var stock = new List<int> { 4, 5 };
        try { stock.AddRange(ValidatedBatch(new List<string>(), "import", 3, true)); }
        catch (Exception) { }
        return result + "|" + string.Join(",", stock.Select(value => value.ToString()));
    }

    private static IEnumerable<int> ValidatedBatch(List<string> events, string label, int count, bool reject)
    {
        events.Add(label + "-start");
        try
        {
            for (int index = 1; index <= count; index++)
            {
                events.Add(label + "-" + index);
                if (reject && index == 3) throw new InvalidOperationException("Rejected row.");
                yield return index;
            }
        }
        finally { events.Add(label + "-close"); }
    }

    private static string StreamDisposal()
    {
        var events = new List<string>();
        try { ValidatedBatch(events, "aggregate", 3, false).Aggregate(0, (total, value) => RejectSecond(total, value)); }
        catch (Exception) { events.Add("caught"); }
        var result = string.Join(",", events);
        events.Clear();
        var first = ValidatedBatch(events, "left", 3, false).Zip(ValidatedBatch(events, "right", 3, false), (left, right) => left + right).First();
        result += $"|{first}:" + string.Join(",", events);
        events.Clear();
        var empty = ValidatedBatch(events, "left", 0, false).Zip(ValidatedBatch(events, "right", 3, false), (left, right) => left + right).ToArray();
        result += $"|{empty.Length}:" + string.Join(",", events);
        events.Clear();
        try { var value = CloseFailure(events, "left").Zip(CloseFailure(events, "right"), (left, right) => left + right).First(); }
        catch (Exception) { events.Add("caught"); }
        result += "|" + string.Join(",", events);
        events.Clear();
        var selected = ValidatedBatch(events, "selected", 2, false).Aggregate(0, (total, value) => total + value, total => AuditAggregateResult(total, events));
        return result + $"|{selected}:" + string.Join(",", events);
    }

    private static IEnumerable<int> CloseFailure(List<string> events, string label)
    {
        try { yield return 1; }
        finally
        {
            events.Add(label + "-close");
            if (label == "right") throw new InvalidOperationException("Close failed.");
        }
    }

    private static int AuditAggregateResult(int value, List<string> events)
    {
        events.Add("result");
        return value;
    }

    private static int RejectSecond(int total, int value)
    {
        if (value == 2) throw new InvalidOperationException("Rejected second reservation.");
        return total + value;
    }

    private static string SensorExtrema()
    {
        var events = new List<string>();
        var sensor = new double[] { 5, double.NaN, 9 }.Select(value => AuditReading(value, events));
        var minimum = sensor.Min();
        var result = minimum.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", events);
        events.Clear();
        var maximum = sensor.Max();
        result += "|" + maximum.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", events);
        events.Clear();
        var nullable = new double?[] { null, 5, double.NaN, 9 }.Select(value => AuditNullableReading(value, events));
        var nullableMinimum = nullable.Min();
        result += "|" + (nullableMinimum ?? 0).ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", events);
        var shipments = new[] { new VerificationShipment("a", null), new VerificationShipment("b", double.NaN), new VerificationShipment("c", 5), new VerificationShipment("d", null) };
        result += $"|{shipments.MinBy(shipment => shipment.Priority)!.Id}:{shipments.MaxBy(shipment => shipment.Priority)!.Id}";
        events.Clear();
        var generic = Enumerable.Min<double>(sensor);
        return result + "|" + generic.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", events);
    }

    private static double AuditReading(double value, List<string> events)
    {
        events.Add(value.ToString(CultureInfo.InvariantCulture));
        return value;
    }

    private static double? AuditNullableReading(double? value, List<string> events)
    {
        events.Add(value is null ? "missing" : (value ?? 0).ToString(CultureInfo.InvariantCulture));
        return value;
    }

    private static string TenantJoinLifecycle()
    {
        var events = new List<string>();
        var first = ValidatedBatch(events, "outer", 3, false).Join(ValidatedBatch(events, "inner", 3, false), value => value, value => value, (left, right) => left + right).First();
        var result = $"{first}:" + string.Join(",", events);
        events.Clear();
        var empty = ValidatedBatch(events, "outer", 3, false).Join(ValidatedBatch(events, "inner", 0, false), value => value, value => value, (left, right) => left + right).ToArray();
        return result + $"|{empty.Length}:" + string.Join(",", events);
    }

    private static string SeededLedger()
    {
        int state = 37;
        var amounts = new List<int>();
        for (int index = 0; index < 250; index++)
        {
            state = state * 1664525 + 1013904223;
            amounts.Add(state % 10000);
        }
        return amounts.Sum().ToString() + "|" + amounts.Average().ToString(CultureInfo.InvariantCulture)
            + $"|{amounts.Min()}:{amounts.Max()}:{amounts.Distinct().Count()}"
            + "|" + string.Join(",", amounts.Chunk(50).Select(batch => batch.Sum().ToString()));
    }

    private static string ComparisonLifecycle()
    {
        var events = new List<string>();
        var matched = ValidatedBatch(events, "left", 3, false).SequenceEqual(ValidatedBatch(events, "right", 2, false));
        var result = $"{matched}:" + string.Join(",", events);
        events.Clear();
        var mismatch = ValidatedBatch(events, "left", 3, false).SequenceEqual(ValidatedBatch(events, "right", 3, false).Select(value => value + 1));
        return result + $"|{mismatch}:" + string.Join(",", events);
    }

    private static string NullQueryCallbacks()
    {
        Func<string, int> missingKey = null!;
        Func<int, string> missingResult = null!;
        var source = new[] { "a", "b" };
        bool rejectedUnion = false, rejectedIntersect = false, rejectedAggregate = false, rejectedMin = false, rejectedMinBy = false, rejectedSum = false, rejectedRemoveAll = false;
        Predicate<int> missingPredicate = null!;
        try { var query = source.UnionBy(source, missingKey); }
        catch (Exception) { rejectedUnion = true; }
        try { var query = source.IntersectBy(new[] { 1 }, missingKey); }
        catch (Exception) { rejectedIntersect = true; }
        try { var total = source.Aggregate(0, (total, value) => total + value.Length, missingResult); }
        catch (Exception) { rejectedAggregate = true; }
        try { var total = source.Min(missingKey); }
        catch (Exception) { rejectedMin = true; }
        try { var value = source.MinBy(missingKey); }
        catch (Exception) { rejectedMinBy = true; }
        try { var total = source.Sum(missingKey); }
        catch (Exception) { rejectedSum = true; }
        try { var count = new List<int>().RemoveAll(missingPredicate); }
        catch (Exception) { rejectedRemoveAll = true; }
        return $"{rejectedUnion}:{rejectedIntersect}:{rejectedAggregate}:{rejectedMin}:{rejectedMinBy}:{rejectedSum}:{rejectedRemoveAll}";
    }

    private static string TenantSetAudit()
    {
        var events = new List<string>();
        var current = new[] { "a", "a", "b" };
        var incoming = new[] { "b", "c", "a" };
        var union = current.UnionBy(incoming, value => AuditTenantKey(value, events)).ToArray();
        var result = string.Join(",", union) + ":" + string.Join(",", events);
        events.Clear();
        var intersect = current.IntersectBy(new[] { "a", "a", "c" }, value => AuditTenantKey(value, events)).ToArray();
        result += "|" + string.Join(",", intersect) + ":" + string.Join(",", events);
        events.Clear();
        var except = current.ExceptBy(new[] { "b", "b" }, value => AuditTenantKey(value, events)).ToArray();
        var nullable = new double?[] { null, double.NaN, 0, -0.0, double.NaN, null };
        return result + "|" + string.Join(",", except) + ":" + string.Join(",", events)
            + $"|{nullable.Distinct().Count()}:{nullable.Intersect(new double?[] { null, double.NaN }).Count()}";
    }

    private static string AuditTenantKey(string value, List<string> events)
    {
        events.Add(value);
        return value;
    }

    private static string ReservationIdentity()
    {
        var accepted = new VerificationReservation("reservation-1", 3);
        var amended = new VerificationReservation("reservation-1", 4);
        var other = new VerificationReservation("reservation-2", 3);
        var pending = new List<VerificationReservation> { accepted, other };
        var equivalent = accepted == amended;
        var contains = pending.Contains(amended);
        var index = pending.IndexOf(amended);
        var removed = pending.Remove(amended);
        return $"{equivalent}:{contains}:{index}:{removed}:{pending.Count}:{accepted.Equals(amended)}";
    }

    private static string ReservationEqualityCallbacks()
    {
        var events = new List<string>();
        var reservation = new VerificationAuditedReservation("one", events);
        var alias = reservation;
        var values = new List<VerificationAuditedReservation> { reservation };
        var contains = values.Contains(reservation);
        var same = reservation == alias;
        var index = values.IndexOf(reservation);
        var removed = values.Remove(reservation);
        var holder = new VerificationReservationHolder(reservation);
        var nested = new List<VerificationReservationHolder> { holder }.Contains(new VerificationReservationHolder(reservation));
        var missing = new List<VerificationAuditedReservation?> { null }.Contains(null);
        return $"{contains}:{same}:{index}:{removed}:{nested}:{missing}:{string.Join(",", events)}";
    }
}

public sealed record VerificationShipment(string Id, double? Priority);

#pragma warning disable CS8851 // Equality is deliberately customized to model reservation identity.
public sealed record VerificationReservation(string Id, int Quantity)
{
    public bool Equals(VerificationReservation? other) => other is not null && Id == other.Id;
}

public sealed record VerificationAuditedReservation(string Id, List<string> Audit)
{
    public bool Equals(VerificationAuditedReservation? other)
    {
        Audit.Add("equals");
        return other is not null && Id == other.Id;
    }
}

public sealed record VerificationReservationHolder(VerificationAuditedReservation Reservation);
#pragma warning restore CS8851
