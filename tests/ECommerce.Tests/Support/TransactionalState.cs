using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace ECommerce.Tests.Support;

// Test repository state is copied on transaction entry and published only on commit.
// This models rollback without copying the production SQL/locking implementation.
public abstract class TransactionalState<T> where T : new()
{
    public T Data { get; private set; } = new();
    protected T Current => _work is null ? Data : _work;
    private T? _work;
    protected Lease Start()
    {
        if (_work is not null) throw new InvalidOperationException("Unexpected nested transaction.");
        _work = Graph.Copy(Data);
        return new Lease(() => Data = Graph.Copy(_work), () => _work = default);
    }
    protected sealed class Lease(Action commit, Action dispose)
    {
        public Task Commit(CancellationToken ct) { ct.ThrowIfCancellationRequested(); commit(); return Task.CompletedTask; }
        public ValueTask Dispose() { dispose(); return ValueTask.CompletedTask; }
    }
}
internal static class Graph
{
    public static T Copy<T>(T value) => (T)Clone(value!, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));
    private static object Clone(object value, Dictionary<object, object> seen)
    {
        var type = value.GetType();
        if (type.IsValueType || value is string) return value;
        if (seen.TryGetValue(value, out var previous)) return previous;
        if (value is IDictionary dictionary)
        {
            var copy = (IDictionary)Activator.CreateInstance(type)!; seen[value] = copy;
            foreach (DictionaryEntry entry in dictionary) copy.Add(Clone(entry.Key, seen), entry.Value is null ? null : Clone(entry.Value, seen));
            return copy;
        }
        if (value is IList list)
        {
            var copy = (IList)Activator.CreateInstance(type)!; seen[value] = copy;
            foreach (var entry in list) copy.Add(entry is null ? null : Clone(entry, seen));
            return copy;
        }
        var result = RuntimeHelpers.GetUninitializedObject(type); seen[value] = result;
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var member = field.GetValue(value);
                field.SetValue(result, member is null ? null : Clone(member, seen));
            }
        return result;
    }
}
