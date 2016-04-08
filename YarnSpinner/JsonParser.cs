/*
This file is a modified version of Daniel Crenna's JSON parser, which is in the
public domain and is available here: https://github.com/danielcrenna/json 
*/

using System;
using System.Collections;
using System.Collections.Generic;
#if NET40
using System.Dynamic;
#endif
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Json
{
    /// <summary>
    /// Possible JSON tokens in parsed input.
    /// </summary>
    public enum JsonToken
    {
        Unknown,
        LeftBrace,
        RightBrace,
        Colon,
        Comma,
        LeftBracket,
        RightBracket,
        String,
        Number,
        True,
        False,
        Null
    }

    /// <summary>
    /// Exception raised when <see cref="JsonParser" /> encounters an invalid token.
    /// </summary>
    public class InvalidJsonException : Exception
    {
        public InvalidJsonException(string message)
            : base(message)
        {

        }
    }

#if NET40
    public interface IJson { }

    public class JsonArray : DynamicObject, IEnumerable, IJson
    {
        private readonly List<IJson> _collection;

        public JsonArray(ICollection<object> collection)
        {
            _collection = new List<IJson>(collection.Count);
            foreach (var instance in collection.Cast<IDictionary<string, object>>())
            {
                _collection.Add(new JsonObject(instance));
            }
        }

        public IEnumerator GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
    }

    public class JsonObject : DynamicObject, IJson
    {
        private readonly IDictionary<string, object> _hash = new Dictionary<string, object>();

        public JsonObject(IDictionary<string, object> hash)
        {
            _hash = hash;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var name = Underscored(binder.Name);
            _hash[name] = value;
            return _hash[name] == value;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var name = Underscored(binder.Name);
            return YieldMember(name, out result);
        }

        private bool YieldMember(string name, out object result)
        {
            if (_hash.ContainsKey(name))
            {
                result = _hash[name];

                if (result is IDictionary<string, object>)
                {
                    result = new JsonObject((IDictionary<string, object>)result);
                    return true;
                }

                return _hash[name] == result;
            }
            result = null;
            return false;
        }

        private static string Underscored(IEnumerable<char> pascalCase)
        {
            var sb = new StringBuilder();
            var i = 0;
            foreach (var c in pascalCase)
            {
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append("_");
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString().ToLowerInvariant();
        }
    }
#endif

    /// <summary>
    /// A parser for JSON.
    /// <seealso cref="http://json.org" />
    /// </summary>
    public class JsonParser
    {
#if !NETCF
        private const NumberStyles JsonNumbers = NumberStyles.Float;
#endif
        private static readonly IDictionary<Type, PropertyInfo[]> _cache;

        private static readonly char[] _base16 = new[]
                             {
                                 '0', '1', '2', '3', 
                                 '4', '5', '6', '7', 
                                 '8', '9', 'A', 'B', 
                                 'C', 'D', 'E', 'F'
                             };

        static JsonParser()
        {
            _cache = new Dictionary<Type, PropertyInfo[]>(0);
        }

        public static string Serialize<T>(T instance)
        {
            var bag = GetBagForObject(instance);

            return ToJson(bag);
        }

        public static object Deserialize(string json, Type type)
        {
            object instance;
            var map = PrepareInstance(out instance, type);
            var bag = FromJson(json);

            DeserializeImpl(map, bag, instance);
            return instance;
        }

        public static T Deserialize<T>(string json)
        {
            T instance;
            var map = PrepareInstance(out instance);
            var bag = FromJson(json);

            DeserializeImpl(map, bag, instance);
            return instance;
        }

#if NET40
        public static dynamic Deserialize(string json)
        {
            JsonToken type;
            var inner = FromJson(json, out type);
            dynamic instance = null;

            switch (type)
            {
                case JsonToken.LeftBrace:
                    var @object = (IDictionary<string, object>)inner.Single().Value;
                    instance = new JsonObject(@object);
                    break;
                case JsonToken.LeftBracket:
                    var @array = (IList<object>)inner.Single().Value;
                    instance = new JsonArray(@array);
                    break;
            }

            return instance;
        }
#endif

        private static void DeserializeImpl(IEnumerable<PropertyInfo> map,
                                            IDictionary<string, object> bag,
                                            object instance)
        {
            DeserializeType(map, bag, instance);
        }

        private static void DeserializeImpl<T>(IEnumerable<PropertyInfo> map,
                                               IDictionary<string, object> bag,
                                               T instance)
        {
            DeserializeType(map, bag, instance);
        }

        private static void DeserializeType(IEnumerable<PropertyInfo> map, IDictionary<string, object> bag, object instance)
        {
            foreach (var info in map)
            {
                var key = info.Name;
                if (!bag.ContainsKey(key))
                {
                    key = info.Name.Replace("_", "");
                    if (!bag.ContainsKey(key))
                    {
                        key = info.Name.Replace("-", "");
                        if (!bag.ContainsKey(key))
                        {
                            continue;
                        }
                    }
                }

                var value = bag[key];
                if (info.PropertyType == typeof(DateTime))
                {
                    // Dates (Not part of spec, using lossy epoch convention)
                    var seconds = Int32.Parse(
                        value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture
                        );
                    var time = new DateTime(1970, 1, 1).ToUniversalTime();
                    value = time.AddSeconds(seconds);
                }

                if (info.PropertyType == typeof(byte[]))
                {
                    var bytes = (List<object>)value;
                    value = bytes.Select(symbol => Convert.ToByte(symbol)).ToArray();
                }

                if (info.PropertyType == typeof(double))
                {
                    value = Convert.ToDouble(value);
                }

                if (info.PropertyType == typeof(int))
                {
                    value = Convert.ToInt32(value);
                }

                if (info.PropertyType == typeof(long))
                {
                    value = Convert.ToInt64(value);
                }

                info.SetValue(instance, value, null);
            }
        }

        public static IDictionary<string, object> FromJson(string json)
        {
            JsonToken type;

            var result = FromJson(json, out type);

            switch (type)
            {
                case JsonToken.LeftBrace:
                    var @object = (IDictionary<string, object>)result.Single().Value;
                    return @object;
            }

            return result;
        }

        public static IDictionary<string, object> FromJson(string json, out JsonToken type)
        {
            var data = json.ToCharArray();
            var index = 0;

            // Rewind index for first token
            var token = NextToken(data, ref index);
            switch (token)
            {
                case JsonToken.LeftBrace:   // Start Object
                case JsonToken.LeftBracket: // Start Array
                    index--;
                    type = token;
                    break;
                default:
                    throw new InvalidJsonException("JSON must begin with an object or array");
            }

            return ParseObject(data, ref index);
        }

        public static string ToJson(IDictionary<string, object> bag)
        {
            var sb = new StringBuilder(0);

            SerializeItem(sb, bag);

            return sb.ToString();
        }

        internal static IDictionary<string, object> GetBagForObject(Type type, object instance)
        {
            CacheReflection(type);

            if (type.FullName == null)
            {
                return null;
            }

            var anonymous = type.FullName.Contains("__AnonymousType");
            var map = _cache[type];

            IDictionary<string, object> bag = InitializeBag();
            foreach (var info in map)
            {
                var readWrite = (info.CanWrite && info.CanRead);
                if (!readWrite && !anonymous)
                {
                    continue;
                }
                var value = info.GetValue(instance, null);
                bag.Add(info.Name, value);
            }

            return bag;
        }

        internal static IDictionary<string, object> GetBagForObject<T>(T instance)
        {
            return GetBagForObject(typeof(T), instance);
        }

        internal static Dictionary<string, object> InitializeBag()
        {
            return new Dictionary<string, object>(
                0, StringComparer.OrdinalIgnoreCase
                );
        }

        internal static IEnumerable<PropertyInfo> PrepareInstance(out object instance, Type type)
        {
            instance = Activator.CreateInstance(type);

            CacheReflection(type);

            return _cache[type];
        }

        internal static IEnumerable<PropertyInfo> PrepareInstance<T>(out T instance)
        {
            instance = Activator.CreateInstance<T>();
            var item = typeof(T);

            CacheReflection(item);

            return _cache[item];
        }

        internal static void CacheReflection(Type item)
        {
            if (_cache.ContainsKey(item))
            {
                return;
            }

            #if PCL
            var properties = item.GetRuntimeProperties().ToArray();
            #else
            var properties = item.GetProperties(
                BindingFlags.Public | BindingFlags.Instance
                );
            #endif

            _cache.Add(item, properties);
        }

        internal static void SerializeItem(StringBuilder sb, object item)
        {
            if (item is IDictionary<string, object>)
            {
                SerializeObject(item, sb);
                return;
            }

            if (item is IEnumerable)
            {
                SerializeArray(item, sb);
                return;
            }

            if (item is DateTime)
            {
                SerializeDateTime(sb);
                return;
            }

            if (item is bool)
            {
                sb.Append(((bool)item).ToString().ToLower());
                return;
            }

            double number;
            var input = item != null ? item.ToString() : "";
#if NETCF
            if (input.TryParse(out number))
            {
                sb.Append(number);
            }
#else
            if (double.TryParse(input, JsonNumbers, CultureInfo.InvariantCulture, out number))
            {
                sb.Append(number);
                return;
            }
#endif
            if (item == null)
            {
                sb.Append("null");
                return;
            }

            var bag = GetBagForObject(item.GetType(), item);
            SerializeItem(sb, bag);
        }

        internal static void SerializeDateTime(StringBuilder sb)
        {
            var elapsed = DateTime.UtcNow - new DateTime(1970, 1, 1).ToUniversalTime();
            var epoch = (long)elapsed.TotalSeconds;
            SerializeString(sb, epoch);
        }

        internal static void SerializeArray(object item, StringBuilder sb)
        {
            var array = (IEnumerable)item;
            sb.Append("[");
            var count = 0;

            var total = array.Cast<object>().Count();
            foreach (var element in array)
            {
                SerializeItem(sb, element);
                count++;
                if (count < total)
                {
                    sb.Append(",");
                }
            }
            sb.Append("]");
        }

        internal static void SerializeObject(object item, StringBuilder sb)
        {
            var nested = (IDictionary<string, object>)item;
            sb.Append("{");

            var count = 0;
            foreach (var key in nested.Keys)
            {
                SerializeString(sb, key.ToLower());
                sb.Append(":");

                var value = nested[key];
                if (value is string)
                {
                    SerializeString(sb, value);
                }
                else
                {
                    SerializeItem(sb, nested[key]);
                }

                if (count < nested.Keys.Count - 1)
                {
                    sb.Append(",");
                }
                count++;
            }
            sb.Append("}");
        }


        internal static void SerializeString(StringBuilder sb, object item)
        {
            sb.Append("\"");
            var symbols = item.ToString().ToCharArray();
            
            var unicodes = symbols.Select(symbol => GetUnicode(symbol));

            foreach (var unicode in unicodes)
            {
                sb.Append(unicode);
            }

            sb.Append("\"");
        }

        internal static string GetUnicode(int code)
        {
            // http://unicode.org/roadmaps/bmp/
            var basicLatin = code >= 32 && code <= 126;
            if (basicLatin)
            {
                var value = (char)code;
                return value == '"' ? @"\""" : new string(value, 1);
            }

            var unicode = BaseConvert(code, _base16, 4);
            return string.Concat("\\u", unicode);
        }

        internal static KeyValuePair<string, object> ParsePair(IList<char> data, ref int index)
        {
            var valid = true;

            var name = ParseString(data, ref index);
            if (name == null)
            {
                valid = false;
            }

            if (!ParseToken(JsonToken.Colon, data, ref index))
            {
                valid = false;
            }

            if (!valid)
            {
                throw new InvalidJsonException(string.Format(
                            "Invalid JSON found while parsing a value pair at index {0}.", index
                            ));
            }

            index++;
            var value = ParseValue(data, ref index);
            return new KeyValuePair<string, object>(name, value);
        }

        internal static bool ParseToken(JsonToken token, IList<char> data, ref int index)
        {
            var nextToken = NextToken(data, ref index);
            return token == nextToken;
        }

        internal static string ParseString(IList<char> data, ref int index)
        {
            var symbol = data[index];
            IgnoreWhitespace(data, ref index, symbol);
            symbol = data[++index]; // Skip first quotation

            var sb = new StringBuilder();
            while (true)
            {
                if (index >= data.Count - 1)
                {
                    return null;
                }
                switch (symbol)
                {
                    case '"':  // End String
                        index++;
                        return sb.ToString();
                    case '\\': // Control Character
                        symbol = data[++index];
                        switch (symbol)
                        {
                            case '/':
                                sb.Append(symbol);
                                break;
							case 't':
								sb.Append ("\t");
								break;
							case 'n':
								sb.Append ("\n");
								break;
							case '\\':
								sb.Append ("\\");
								break;
							case '\"':
								sb.Append ("\"");
								break;
							case 'r':
								
                            case 'b':
                            case 'f':
                            
                                break;
                            case 'u': // Unicode literals
                                if (index < data.Count - 5)
                                {
                                    var array = data.ToArray();
                                    var buffer = new char[4];
                                    Array.Copy(array, index + 1, buffer, 0, 4);

                                    // http://msdn.microsoft.com/en-us/library/aa664669%28VS.71%29.aspx
                                    // http://www.yoda.arachsys.com/csharp/unicode.html
                                    // http://en.wikipedia.org/wiki/UTF-32/UCS-4
                                    var hex = new string(buffer);
                                    var unicode = (char)Convert.ToInt32(hex, 16);
                                    sb.Append(unicode);
                                    index += 4;
                                }
                                else
                                {
                                    break;
                                }
                                break;
                        }
                        break;
                    default:
                        sb.Append(symbol);
                        break;
                }
                symbol = data[++index];
            }
        }

        internal static object ParseValue(IList<char> data, ref int index)
        {
            var token = NextToken(data, ref index);
            switch (token)
            {
                // End Tokens
                case JsonToken.RightBracket:    // Bad Data
                case JsonToken.RightBrace:
                case JsonToken.Unknown:
                case JsonToken.Colon:
                case JsonToken.Comma:
                    throw new InvalidJsonException(string.Format(
                            "Invalid JSON found while parsing a value at index {0}.", index
                            ));
                // Value Tokens
                case JsonToken.LeftBrace:
                    return ParseObject(data, ref index);
                case JsonToken.LeftBracket:
                    return ParseArray(data, ref index);
                case JsonToken.String:
                    return ParseString(data, ref index);
                case JsonToken.Number:
                    return ParseNumber(data, ref index);
                case JsonToken.True:
                    return true;
                case JsonToken.False:
                    return false;
                case JsonToken.Null:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static IDictionary<string, object> ParseObject(IList<char> data, ref int index)
        {
            var result = InitializeBag();

            index++; // Skip first token

            while (index < data.Count - 1)
            {
                var token = NextToken(data, ref index);
                switch (token)
                {
                    // End Tokens
                    case JsonToken.Unknown:             // Bad Data
                    case JsonToken.True:
                    case JsonToken.False:
                    case JsonToken.Null:
                    case JsonToken.Colon:
                    case JsonToken.RightBracket:
                    case JsonToken.Number:
                        throw new InvalidJsonException(string.Format(
                            "Invalid JSON found while parsing an object at index {0}.", index
                            ));
                    case JsonToken.RightBrace:          // End Object
                        index++;
                        return result;
                    // Skip Tokens
                    case JsonToken.Comma:
                        index++;
                        break;
                    // Start Tokens
                    case JsonToken.LeftBrace:           // Start Object
                        var @object = ParseObject(data, ref index);
                        if (@object != null)
                        {
                            result.Add(string.Concat("object", result.Count), @object);
                        }
                        index++;
                        break;
                    case JsonToken.LeftBracket:         // Start Array
                        var @array = ParseArray(data, ref index);
                        if (@array != null)
                        {
                            result.Add(string.Concat("array", result.Count), @array);
                        }
                        index++;
                        break;
                    case JsonToken.String:
                        var pair = ParsePair(data, ref index);
                        result.Add(pair.Key, pair.Value);
                        break;
                    default:
                        throw new NotSupportedException("Invalid token expected.");
                }
            }

            return result;
        }

        internal static IEnumerable<object> ParseArray(IList<char> data, ref int index)
        {
            var result = new List<object>();

            index++; // Skip first bracket
            while (index < data.Count - 1)
            {
                var token = NextToken(data, ref index);
                switch (token)
                {
                    // End Tokens
                    case JsonToken.Unknown:             // Bad Data
                        throw new InvalidJsonException(string.Format(
                            "Invalid JSON found while parsing an array at index {0}.", index
                            ));
                    case JsonToken.RightBracket:        // End Array
                        index++;
                        return result;
                    // Skip Tokens
                    case JsonToken.Comma:               // Separator
                    case JsonToken.RightBrace:          // End Object
                    case JsonToken.Colon:               // Separator
                        index++;
                        break;
                    // Value Tokens
                    case JsonToken.LeftBrace:           // Start Object
                        var nested = ParseObject(data, ref index);
                        result.Add(nested);
                        break;
                    case JsonToken.LeftBracket:         // Start Array
                    case JsonToken.String:
                    case JsonToken.Number:
                    case JsonToken.True:
                    case JsonToken.False:
                    case JsonToken.Null:
                        var value = ParseValue(data, ref index);
                        result.Add(value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return result;
        }

        internal static object ParseNumber(IList<char> data, ref int index)
        {
            var symbol = data[index];
            IgnoreWhitespace(data, ref index, symbol);

            var start = index;
            var length = 0;
            while (ParseToken(JsonToken.Number, data, ref index))
            {
                length++;
                index++;
            }

            var number = new char[length];
            Array.Copy(data.ToArray(), start, number, 0, length);

            double result;
            var buffer = new string(number);
#if NETCF
            if (!buffer.TryParse(out result))
            {
                throw new InvalidJsonException(
                    string.Format("Value '{0}' was not a valid JSON number", buffer)
                    );
            }
#else
            if (!double.TryParse(buffer, JsonNumbers, CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidJsonException(
                    string.Format("Value '{0}' was not a valid JSON number", buffer)
                    );
            }
#endif

            return result;
        }

        internal static JsonToken NextToken(IList<char> data, ref int index)
        {
            var symbol = data[index];
            var token = GetTokenFromSymbol(symbol);
            token = IgnoreWhitespace(data, ref index, ref token, symbol);

            GetKeyword("true", JsonToken.True, data, ref index, ref token);
            GetKeyword("false", JsonToken.False, data, ref index, ref token);
            GetKeyword("null", JsonToken.Null, data, ref index, ref token);

            return token;
        }

        internal static JsonToken GetTokenFromSymbol(char symbol)
        {
            return GetTokenFromSymbol(symbol, JsonToken.Unknown);
        }

        internal static JsonToken GetTokenFromSymbol(char symbol, JsonToken token)
        {
            switch (symbol)
            {
                case '{':
                    token = JsonToken.LeftBrace;
                    break;
                case '}':
                    token = JsonToken.RightBrace;
                    break;
                case ':':
                    token = JsonToken.Colon;
                    break;
                case ',':
                    token = JsonToken.Comma;
                    break;
                case '[':
                    token = JsonToken.LeftBracket;
                    break;
                case ']':
                    token = JsonToken.RightBracket;
                    break;
                case '"':
                    token = JsonToken.String;
                    break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '.':
                case 'e':
                case 'E':
                case '+':
                case '-':
                    token = JsonToken.Number;
                    break;
            }
            return token;
        }

        internal static void IgnoreWhitespace(IList<char> data, ref int index, char symbol)
        {
            var token = JsonToken.Unknown;
            IgnoreWhitespace(data, ref index, ref token, symbol);
            return;
        }

        internal static JsonToken IgnoreWhitespace(IList<char> data, ref int index, ref JsonToken token, char symbol)
        {
            switch (symbol)
            {
                case ' ':
                case '\\':
                case '/':
                case '\b':
                case '\f':
                case '\n':
                case '\r':
                case '\t':
                    index++;
                    token = NextToken(data, ref index);
                    break;
            }
            return token;
        }

        internal static void GetKeyword(string word,
                                       JsonToken target,
                                       IList<char> data,
                                       ref int index,
                                       ref JsonToken result)
        {
            var buffer = data.Count - index;
            if (buffer < word.Length)
            {
                return;
            }

            for (var i = 0; i < word.Length; i++)
            {
                if (data[index + i] != word[i])
                {
                    return;
                }
            }

            result = target;
            index += word.Length;
        }

        internal static string BaseConvert(int input, char[] charSet, int minLength)
        {
            var sb = new StringBuilder();
            var @base = charSet.Length;

            while (input > 0)
            {
                var index = input % @base;
                sb.Insert(0, new[] { charSet[index] });
                input = input / @base;
            }

            while (sb.Length < minLength)
            {
                sb.Insert(0, "0");
            }

            return sb.ToString();
        }
    }

#if NETCF
    public static class CompactExtensions
    {
        private const NumberStyles JsonNumbers = NumberStyles.Float;

        public static bool TryParse(this string input, out double result)
        {
            try
            {
                result = double.Parse(input, JsonNumbers, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                result = 0;
                return false;
            }
        }
    }
#endif
}
