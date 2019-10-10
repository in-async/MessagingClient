using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting {

    /// <summary>
    /// テスト検証のヘルパークラス。
    /// </summary>
    public static class AssertExtensions {

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings {
            ContractResolver = new ComparingContractResolver(),
            Formatting = Formatting.None,
        };

        private class ComparingContractResolver : DefaultContractResolver {
            private readonly Type _comparingType;

            public ComparingContractResolver(Type comparingType = null) {
                _comparingType = comparingType;
            }

            public override JsonContract ResolveContract(Type type) {
                if (_comparingType != null && _comparingType.IsAssignableFrom(type)) {
                    return base.ResolveContract(_comparingType);
                }

                return base.ResolveContract(type);
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
                var property = base.CreateProperty(member, memberSerialization);
                property.ShouldSerialize = instance => true;

                return property;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
                return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToArray();
            }
        }

        public static void DeepIs(this object actual, object expected, string message = null) => DeepIs(actual, expected, message, _jsonSettings);

        public static void DeepIs<T>(this object actual, object expected, string message = null) => DeepIs(actual, expected, message, new JsonSerializerSettings {
            ContractResolver = new ComparingContractResolver(typeof(T)),
            Formatting = Formatting.None,
        });

        private static void DeepIs(object actual, object expected, string message, JsonSerializerSettings jsonSettings) {
            // HACK: JSON で代用しているが、そのうちリフレクションで堅実に比較検証する。
            var actualJson = JsonConvert.SerializeObject(actual, jsonSettings);
            var expectedJson = JsonConvert.SerializeObject(expected, jsonSettings);
            try {
                Console.WriteLine("## Actual:");
                Console.WriteLine(actualJson);
                Assert.AreEqual(expectedJson, actualJson, message);
            }
            catch (AssertFailedException) {
                Console.WriteLine("## Expected:");
                Console.WriteLine(expectedJson);
                throw;
            }
        }

        public static void Is<T>(this T actual, T expected, string message = null) {
            Is(typeof(T), actual, expected, message);
        }

        public static void Is<T>(this IEnumerable<T> actual, IEnumerable<T> expected, string message = null) {
            Is(typeof(IEnumerable<T>), actual, expected, message);
        }

        private static void Is(Type type, object actual, object expected, string message) {
            Debug.Assert(type != null);

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string)) {
                CollectionAssert.AreEqual(((IEnumerable)expected).ToCollection(), ((IEnumerable)actual).ToCollection(), message);
                return;
            }

            if (type.FullName.StartsWith("System.ValueTuple`")) {
                foreach (var field in type.GetFields()) {
                    Is(field.FieldType, field.GetValue(actual), field.GetValue(expected), message + ":" + field.Name);
                }
                return;
            }

            Assert.AreEqual(expected, actual, message);
        }

        private static ICollection ToCollection(this IEnumerable source) {
            if (source == null) { return null; }
            if (source is ICollection casted) { return casted; }

            var list = new List<object>();
            foreach (var item in source) {
                list.Add(item);
            }
            return list;
        }
    }
}
