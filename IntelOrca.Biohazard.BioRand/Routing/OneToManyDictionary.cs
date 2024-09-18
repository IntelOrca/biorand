using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class OneToManyDictionary<TOne, TMany>
        where TOne : notnull
        where TMany : notnull
    {
        private Dictionary<TOne, TMany> _keyToValue = new Dictionary<TOne, TMany>();
        private Dictionary<TMany, HashSet<TOne>> _valueToKeys = new Dictionary<TMany, HashSet<TOne>>();

        public TMany this[TOne key] => _keyToValue[key];

        public bool TryGetValue(TOne key, out TMany value) => _keyToValue.TryGetValue(key, out value);

        public void Add(TOne key, TMany value)
        {
            _keyToValue.Add(key, value);
            var set = GetManyToOneList(value);
            set.Add(key);
        }

        public ISet<TOne> GetKeysContainingValue(TMany value)
        {
            if (_valueToKeys.TryGetValue(value, out var keys))
                return keys;
            return ImmutableHashSet<TOne>.Empty;
        }

        private HashSet<TOne> GetManyToOneList(TMany value)
        {
            if (!_valueToKeys.TryGetValue(value, out var set))
            {
                set = new HashSet<TOne>();
                _valueToKeys.Add(value, set);
            }
            return set;
        }

        public ImmutableOneToManyDictionary<TOne, TMany> ToImmutable()
        {
            return new ImmutableOneToManyDictionary<TOne, TMany>(
                _keyToValue.ToImmutableDictionary(),
                _valueToKeys.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableHashSet()));
        }
    }

    public sealed class ImmutableOneToManyDictionary<TOne, TMany>
        where TOne : notnull
        where TMany : notnull
    {
        public static ImmutableOneToManyDictionary<Node, Node> Empty { get; } = new ImmutableOneToManyDictionary<Node, Node>();

        private readonly ImmutableDictionary<TOne, TMany> _keyToValue;
        private readonly ImmutableDictionary<TMany, ImmutableHashSet<TOne>> _valueToKeys;

        private ImmutableOneToManyDictionary()
        {
            _keyToValue = ImmutableDictionary<TOne, TMany>.Empty;
            _valueToKeys = ImmutableDictionary<TMany, ImmutableHashSet<TOne>>.Empty;
        }

        internal ImmutableOneToManyDictionary(
            ImmutableDictionary<TOne, TMany> keyToValue,
            ImmutableDictionary<TMany, ImmutableHashSet<TOne>> valueToKeys)
        {
            _keyToValue = keyToValue;
            _valueToKeys = valueToKeys;
        }

        public TMany this[TOne key] => _keyToValue[key];

        public ImmutableHashSet<TOne> GetKeysContainingValue(TMany value)
        {
            if (_valueToKeys.TryGetValue(value, out var keys))
                return keys;
            return ImmutableHashSet<TOne>.Empty;
        }

        public bool TryGetValue(TOne key, [MaybeNullWhen(false)] out TMany value) => _keyToValue.TryGetValue(key, out value);

        public ImmutableOneToManyDictionary<TOne, TMany> Add(TOne key, TMany value)
        {
            var newKeyToValue = _keyToValue.Add(key, value);
            var newValueToKeys = _valueToKeys;
            if (_valueToKeys.TryGetValue(value, out var set))
            {
                newValueToKeys = newValueToKeys.SetItem(value, set.Add(key));
            }
            else
            {
                newValueToKeys = _valueToKeys.Add(value, ImmutableHashSet<TOne>.Empty);
            }
            return new ImmutableOneToManyDictionary<TOne, TMany>(newKeyToValue, newValueToKeys);
        }
    }
}
