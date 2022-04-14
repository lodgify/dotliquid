using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace DotLiquid
{
    /// <summary>
    /// Context keeps the variable stack and resolves variables, as well as keywords
    /// </summary>
    public class Context
    {
        private static readonly HashSet<char> SpecialCharsSet = new HashSet<char>() { '\'', '"', '(', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '-' };
        private static readonly Regex SingleQuotedRegex = R.C(R.Q(@"^'(.*)'$"));
        private static readonly Regex DoubleQuotedRegex = R.C(R.Q(@"^""(.*)""$"));
        private static readonly Regex IntegerRegex = R.C(R.Q(@"^([+-]?\d+)$"));
        private static readonly Regex RangeRegex = R.C(R.Q(@"^\((\S+)\.\.(\S+)\)$"));
        private static readonly Regex NumericRegex = R.C(R.Q(@"^([+-]?\d[\d\.|\,]+)$"));
        private static readonly Regex VariableParserRegex = R.C(Liquid.VariableParser);

        private readonly ErrorsOutputMode _errorsOutputMode;

        /// <summary>
        /// Liquid syntax flag used for backward compatibility
        /// </summary>
        public SyntaxCompatibility SyntaxCompatibilityLevel { get; set; }

        /// <summary>
        /// Ruby Date Format flag, switches Date filter syntax between Ruby and CSharp formats.
        /// </summary>
        public bool UseRubyDateFormat { get; set; }

        /// <summary>
        /// Returns the CurrentCulture specified for this Context.
        /// </summary>
        /// <remarks>
        /// **WARNING**: If the context was created with an IFormatProvider that is not also
        /// a CultureInfo it is replaced with the current threads CurrentCulture.
        /// </remarks>
        public CultureInfo CurrentCulture
        {
            get
            {
                return this.FormatProvider is CultureInfo cultureInfo
                    ? cultureInfo
                    : CultureInfo.CurrentCulture;
            }
            set
            {
                this.FormatProvider = value;
            }
        }

        private readonly int _maxIterations;

        public int MaxIterations
        {
            get { return _maxIterations; }
        }

        private Strainer _strainer;

        /// <summary>
        /// Environments
        /// </summary>
        public List<Hash> Environments { get; private set; }

        /// <summary>
        /// Scopes
        /// </summary>
        public List<Hash> Scopes { get; private set; }

        /// <summary>
        /// Hash of user-defined, internally-available variables
        /// </summary>
        public Hash Registers { get; private set; }

        /// <summary>
        /// Exceptions that have been raised during rendering
        /// </summary>
        public List<Exception> Errors { get; private set; }

        /// <summary>
        /// Creates a new rendering context
        /// </summary>
        /// <param name="environments"></param>
        /// <param name="outerScope"></param>
        /// <param name="registers"></param>
        /// <param name="errorsOutputMode"></param>
        /// <param name="maxIterations"></param>
        /// <param name="timeout"></param>
        /// <param name="formatProvider">A CultureInfo instance that will be used to parse filter input and format filter output</param>
        [Obsolete("The method with timeout argument is deprecated. Please use the one with CancellationToken.")]
        public Context
            (List<Hash> environments
             , Hash outerScope
             , Hash registers
             , ErrorsOutputMode errorsOutputMode
             , int maxIterations
             , int timeout
             , IFormatProvider formatProvider)
            : this(environments, outerScope, registers, errorsOutputMode, maxIterations, formatProvider, CancellationToken.None)
        {
            _timeout = timeout;
            RestartTimeout();
        }

        /// <summary>
        /// Creates a new rendering context
        /// </summary>
        /// <param name="environments"></param>
        /// <param name="outerScope"></param>
        /// <param name="registers"></param>
        /// <param name="errorsOutputMode"></param>
        /// <param name="maxIterations"></param>
        /// <param name="formatProvider">A CultureInfo instance that will be used to parse filter input and format filter output</param>
        /// <param name="cancellationToken"></param>
        public Context
            (List<Hash> environments
             , Hash outerScope
             , Hash registers
             , ErrorsOutputMode errorsOutputMode
             , int maxIterations
             , IFormatProvider formatProvider
             , CancellationToken cancellationToken)
        {
            Environments = environments;

            Scopes = new List<Hash>();
            if (outerScope != null)
                Scopes.Add(outerScope);

            Registers = registers;

            Errors = new List<Exception>();
            _errorsOutputMode = errorsOutputMode;
            _maxIterations = maxIterations;
            _cancellationToken = cancellationToken;
            FormatProvider = formatProvider;
            SyntaxCompatibilityLevel = Template.DefaultSyntaxCompatibilityLevel;
            UseRubyDateFormat = Liquid.UseRubyDateFormat;

            SquashInstanceAssignsWithEnvironments();
        }

        /// <summary>
        /// Creates a new rendering context
        /// </summary>
        /// <param name="formatProvider">A CultureInfo instance that will be used to parse filter input and format filter output</param>
        public Context(IFormatProvider formatProvider)
            : this(new List<Hash>(), new Hash(), new Hash(), ErrorsOutputMode.Display, 0, 0, formatProvider)
        {
        }

        /// <summary>
        /// Strainer for the current context
        /// </summary>
        public Strainer Strainer
        {
            get { return (_strainer = _strainer ?? Strainer.Create(this)); }
        }

        /// <summary>
        /// Adds a filter from a function
        /// </summary>
        /// <typeparam name="TIn">Type of the parameter</typeparam>
        /// <typeparam name="TOut">Type of the returned value</typeparam>
        /// <param name="filterName">Filter name</param>
        /// <param name="func">Filter function</param>
        public void AddFilter<TIn, TOut>(string filterName, Func<TIn, TOut> func)
        {
            Strainer.AddFunction(filterName, func);
        }

        /// <summary>
        /// Adds a filter from a function
        /// </summary>
        /// <typeparam name="TIn">Type of the first parameter</typeparam>
        /// <typeparam name="TIn2">Type of the second parameter</typeparam>
        /// <typeparam name="TOut">Type of the returned value</typeparam>
        /// <param name="filterName">Filter name</param>
        /// <param name="func">Filter function</param>
        public void AddFilter<TIn, TIn2, TOut>(string filterName, Func<TIn, TIn2, TOut> func)
        {
            Strainer.AddFunction(filterName, func);
        }

        /// <summary>
        /// Adds filters to this context.
        /// this does not register the filters with the main Template object. see <tt>Template.register_filter</tt>
        /// for that
        /// </summary>
        /// <param name="filters"></param>
        public void AddFilters(IEnumerable<Type> filters)
        {
            foreach (Type f in filters)
                Strainer.Extend(f);
        }

        /// <summary>
        /// Add filters from a list of types
        /// </summary>
        /// <param name="filters"></param>
        public void AddFilters(params Type[] filters)
        {
            if (filters != null)
                AddFilters(filters.AsEnumerable());
        }

        /// <summary>
        /// Handles error during rendering
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public string HandleError(Exception ex)
        {
            if (ex is InterruptException || ex is TimeoutException || ex is RenderException || ex is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            Errors.Add(ex);

            if (_errorsOutputMode == ErrorsOutputMode.Suppress)
                return string.Empty;

            if (_errorsOutputMode == ErrorsOutputMode.Rethrow)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            if (ex is SyntaxException)
            {
                return string.Format(Liquid.ResourceManager.GetString("ContextLiquidSyntaxError"), ex.Message);
            }

            return string.Format(Liquid.ResourceManager.GetString("ContextLiquidError"), ex.Message);
        }

        /// <summary>
        /// Invokes a strainer method
        /// </summary>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task<object> InvokeAsync(string method, List<object> args)
        {
            if (Strainer.RespondTo(method))
            {
                return await Strainer.InvokeAsync(method, args);
            }

            if (SyntaxCompatibilityLevel >= SyntaxCompatibility.DotLiquid22)
                throw new FilterNotFoundException(method); // this will be caught and rethrown in caller with correct message

            return args.First();
        }

        /// <summary>
        /// Push new local scope on the stack. use <tt>Context#stack</tt> instead
        /// </summary>
        /// <param name="newScope"></param>
        public void Push(Hash newScope)
        {
            if (Scopes.Count > 80)
                throw new StackLevelException(Liquid.ResourceManager.GetString("ContextStackException"));

            Scopes.Insert(0, newScope);
        }

        /// <summary>
        /// Merge a hash of variables in the current local scope
        /// </summary>
        /// <param name="newScopes"></param>
        public void Merge(Hash newScopes)
        {
            Scopes[0].Merge(newScopes);
        }

        /// <summary>
        /// Pop from the stack. use <tt>Context#stack</tt> instead
        /// </summary>
        public Hash Pop()
        {
            if (Scopes.Count == 1)
                throw new ContextException();
            Hash result = Scopes[0];
            Scopes.RemoveAt(0);
            return result;
        }

        /// <summary>
        /// Pushes a new local scope on the stack, pops it at the end of the block
        ///
        /// Example:
        ///
        /// context.stack do
        /// context['var'] = 'hi'
        /// end
        /// context['var] #=> nil
        /// </summary>
        /// <param name="newScope"></param>
        /// <param name="callbackAsync"></param>
        /// <returns></returns>
        public async Task StackAsync(Hash newScope, Func<Task> callbackAsync)
        {
            Push(newScope);
            try
            {
                await callbackAsync();
            }
            finally
            {
                Pop();
            }
        }

        /// <summary>
        /// Pushes a new hash on the stack, pops it at the end of the block
        /// </summary>
        /// <param name="callbackAsync"></param>
        public Task StackAsync(Func<Task> callbackAsync)
        {
            return StackAsync(new Hash(), callbackAsync);
        }

        /// <summary>
        /// Clear the current instance assigns
        /// </summary>
        public void ClearInstanceAssigns()
        {
            Scopes[0].Clear();
        }

        /// <summary>
        /// Only allow String, Numeric, Hash, Array, Proc, Boolean or <tt>Liquid::Drop</tt>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="notifyNotFound">True to notify if variable is not found; Default true.</param>
        /// <returns></returns>
        public Task<object> GetAsync(string key, bool notifyNotFound = true)
        {
            return ResolveAsync(key, notifyNotFound);
        }

        public void Set(string key, object value)
        {
            Scopes[0][key] = value;
        }


        /// <summary>
        /// Checks if a variable key exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<bool> HasKeyAsync(string key)
        {
            return (await ResolveAsync(key, false) != null);
        }

        /// <summary>
        /// Look up variable, either resolve directly after considering the name. We can directly handle
        /// Strings, digits, floats and booleans (true,false). If no match is made we lookup the variable in the current scope and
        /// later move up to the parent blocks to see if we can resolve the variable somewhere up the tree.
        /// Some special keywords return symbols. Those symbols are to be called on the rhs object in expressions
        ///
        /// Example:
        ///
        /// products == empty #=> products.empty?
        /// </summary>
        /// <param name="key"></param>
        /// <param name="notifyNotFound">True to notify if variable is not found; Default true.</param>
        /// <returns></returns>
        private async Task<object> ResolveAsync(string key, bool notifyNotFound = true)
        {
            switch (key)
            {
                case null:
                case "nil":
                case "null":
                case "":
                    return null;
                case "true":
                    return true;
                case "false":
                    return false;
                case "blank":
                case "empty":
                    return new Symbol(o => (o is IEnumerable enumerableO) && !enumerableO.Cast<object>().Any());
            }

            var firstChar = key[0];
            if (SpecialCharsSet.Contains(firstChar))
            {
                switch (firstChar)
                {
                    case '\'':
                        // Single quoted strings.
                        Match match = SingleQuotedRegex.Match(key);
                        if (match.Success)
                            return match.Groups[1].Value;
                        break;
                    case '"':
                        // Double quoted strings.
                        match = DoubleQuotedRegex.Match(key);
                        if (match.Success)
                            return match.Groups[1].Value;
                        break;
                    case '(':
                        // Ranges.
                        match = RangeRegex.Match(key);
                        if (match.Success)
                            return DotLiquid.Util.Range.Inclusive(Convert.ToInt32(await ResolveAsync(match.Groups[1].Value)),
                                Convert.ToInt32(await ResolveAsync(match.Groups[2].Value)));
                        break;
                    default:
                        // Integer.
                        match = IntegerRegex.Match(key);
                        if (match.Success)
                        {
                            try
                            {
                                return Convert.ToInt32(match.Groups[1].Value);
                            }
                            catch (OverflowException)
                            {
                                return Convert.ToInt64(match.Groups[1].Value);
                            }
                        }

                        // Floating point numbers.
                        match = NumericRegex.Match(key);
                        if (match.Success)
                        {
                            // For cultures with "," as the decimal separator, allow
                            // both "," and "." to be used as the separator.
                            // First try to parse using current culture.
                            // If that fails, try to parse using invariant culture.
                            // Also, first try higher precision decimal.
                            // If that fails, try to parse as double (precision float).
                            // Double is less precise but has a larger range.
                            if (decimal.TryParse(match.Groups[1].Value, NumberStyles.Number | NumberStyles.Float, FormatProvider, out decimal parsedDecimalCurrentCulture))
                                return parsedDecimalCurrentCulture;
                            if (decimal.TryParse(match.Groups[1].Value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedDecimalInvariantCulture))
                                return parsedDecimalInvariantCulture;
                            if (double.TryParse(match.Groups[1].Value, NumberStyles.Number | NumberStyles.Float, FormatProvider, out double parsedDouble))
                                return parsedDouble;
                            return double.Parse(match.Groups[1].Value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture);
                        }
                        break;
                }
            }
            return await VariableAsync(key, notifyNotFound);
        }

        public IFormatProvider FormatProvider { get; private set; }

        /// <summary>
        /// Fetches an object starting at the local scope and then moving up
        /// the hierarchy
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private async Task<(bool, object)> TryFindVariableAsync(string key)
        {
            bool foundVariable = false;
            object foundValue = null;
            object variable = null;
            Hash scope = Scopes.FirstOrDefault(s => s.ContainsKey(key));
            if (scope == null)
            {
                foreach (Hash environment in Environments)
                {
                    (foundVariable, foundValue) = await TryEvaluateHashOrArrayLikeObjectAsync(environment, key);
                    if (foundVariable)
                    {
                        scope = environment;
                        break;
                    }
                }

                if (scope == null)
                {
                    scope = Environments.LastOrDefault() ?? Scopes.Last();
                    (foundVariable, foundValue) = await TryEvaluateHashOrArrayLikeObjectAsync(scope, key);
                }
            }
            else
            {
                (foundVariable, foundValue) = await TryEvaluateHashOrArrayLikeObjectAsync(scope, key);
            }

            variable = Liquidize(foundValue);
            if (variable is IContextAware contextAwareVariable)
            {
                contextAwareVariable.Context = this;
            }
            return (foundVariable, variable);
        }

        /// <summary>
        /// Resolves namespaced queries gracefully.
        ///
        /// Example
        ///
        /// @context['hash'] = {"name" => 'tobi'}
        /// assert_equal 'tobi', @context['hash.name']
        /// assert_equal 'tobi', @context['hash["name"]']
        /// </summary>
        /// <param name="markup"></param>
        /// <param name="notifyNotFound"></param>
        /// <returns></returns>
        private async Task<object> VariableAsync(string markup, bool notifyNotFound)
        {
            using (var partsEnumerator = SyntaxCompatibilityLevel >= SyntaxCompatibility.DotLiquid22 ? Tokenizer.GetVariableEnumerator(markup) : R.Scan(markup, VariableParserRegex).GetEnumerator())
            {
                var (success, variable) = await TryGetVariableAsync(partsEnumerator);
                if (success)
                    return variable;
                if (notifyNotFound)
                    Errors.Add(new VariableNotFoundException(string.Format(Liquid.ResourceManager.GetString("VariableNotFoundException"), markup)));
                return null;
            }
        }

        private async Task<(bool, object)> TryGetVariableAsync(IEnumerator<string> partsEnumerator)
        {
            object variable = null;

            // first item in list, if any
            string firstPart = partsEnumerator.MoveNext() ? partsEnumerator.Current : null;
            if (firstPart != null && firstPart[0] == '[')
                firstPart = (await ResolveAsync(firstPart.Substring(1, firstPart.Length - 2))).ToString();

            object @object;
            bool variableFound = false;

            if (firstPart == null)
            {
                variable = null;
                return (false, variable);
            }
            else
            {
                (variableFound, @object) = await TryFindVariableAsync(firstPart);
                if (!variableFound)
                {
                    variable = null;
                    return (false, variable);
                }
            }

            // try to resolve the rest of the parts (starting from the second item in the list)
            while (partsEnumerator.MoveNext())
            {
                string forEachPart = partsEnumerator.Current;
                bool partResolved = forEachPart[0] == '[';

                object part = forEachPart;
                if (partResolved)
                    part = await ResolveAsync(forEachPart.Substring(1, forEachPart.Length - 2));              

                // If object is a KeyValuePair and the required part is either 0 or 'Key', return the Key.
                var isKeyValuePair = IsKeyValuePair(@object);
                if (isKeyValuePair && (part.SafeTypeInsensitiveEqual(0L) || part.Equals("Key")))
                {
                    @object = Liquidize(@object.GetPropertyValue("Key"));
                }
                // If object is a KeyValuePair and the required part is either 1 or 'Value' or part matches the key, return the Value.
                else if (isKeyValuePair && (part.SafeTypeInsensitiveEqual(1L)
                                        || part.Equals("Value")
                                        || part.Equals(@object.GetPropertyValue("Key"))))
                {
                    @object = Liquidize(@object.GetPropertyValue("Value"));
                }
                else
                {
                    // If object is a hash- or array-like object we look for the
                    // presence of the key and if its available we return it
                    var (tryEvaluateHashOrArraySuccess, hashObj) = await TryEvaluateHashOrArrayLikeObjectAsync(@object, part);
                    if (tryEvaluateHashOrArraySuccess)
                    {
                        // If its a proc we will replace the entry with the proc
                        @object = Liquidize(hashObj);
                    }
                    // Some special cases. If the part wasn't in square brackets and
                    // no key with the same name was found we interpret first/last/size
                    // as commands and call them on the current object
                    else if (!partResolved && (@object is IEnumerable enumerable) && (Template.NamingConvention.OperatorEquals(part as string, "size") || Template.NamingConvention.OperatorEquals(part as string, "first") || Template.NamingConvention.OperatorEquals(part as string, "last")))
                    {
                        var castCollection = enumerable.Cast<object>();
                        if (Template.NamingConvention.OperatorEquals(part as string, "size"))
                        {
                            @object = castCollection.Count();
                        }
                        else if (Template.NamingConvention.OperatorEquals(part as string, "first"))
                        {
                            @object = Liquidize(castCollection.FirstOrDefault());
                        }
                        else
                        {
                            @object = Liquidize(castCollection.LastOrDefault());
                        }
                    }
                    // No key was present with the desired value and it wasn't one of the directly supported
                    // keywords either. The only thing we got left is to return nil
                    else
                    {
                        variable = null;
                        return (false, variable);
                    }
                }

                // If we are dealing with a drop here we have to
                if (@object is IContextAware contextAwareObject)
                {
                    contextAwareObject.Context = this;
                }
            }
            variable = @object;
            return (true, variable);
        }

        private async Task<(bool, object)> TryEvaluateHashOrArrayLikeObjectAsync(object obj, object key)
        {
            object value = null;

            if (obj == null)
                return (false, value);

            if ((obj is IDictionary dictionaryObj && dictionaryObj.Contains(key)))
                value = dictionaryObj[key];

            // Resolve #350/#417, add support for rendering of a nested  ExpandoObject
            else if (obj is IDictionary<string, object> dictionaryObject && dictionaryObject.ContainsKey(key.ToString()))
                value = dictionaryObject[key.ToString()];

            else if ((obj is IList listObj) && (key is int || key is uint || key is long || key is ulong || key is short || key is ushort || key is byte || key is sbyte
                || (key is decimal dec && Math.Truncate(dec) == dec) || (key is double dbl && Math.Truncate(dbl) == dbl) || (key is float flt && Math.Truncate(flt) == flt)))
                value = listObj[Convert.ToInt32(key)];

            else if (TypeUtility.IsAnonymousType(obj.GetType()) && obj.GetType().GetRuntimeProperty((string)key) != null)
                value = obj.GetType().GetRuntimeProperty((string)key).GetValue(obj, null);

            else if ((obj is IIndexable indexableObj) && indexableObj.ContainsKey(key))
                value = await indexableObj.GetAsync(key);

            else
                return (false, value);

            if (value is Proc procValue)
            {
                object newValue = procValue.Invoke(this);
                if (obj is IDictionary dicObj)
                {
                    dicObj[key] = newValue;
                }
                else if (obj is IList listObj)
                {
                    listObj[Convert.ToInt32(key)] = newValue;
                }
                else if (TypeUtility.IsAnonymousType(obj.GetType()))
                {
                    obj.GetType().GetRuntimeProperty((string)key).SetValue(obj, newValue, null);
                }
                else
                {
                    throw new NotSupportedException();
                }
                value = newValue;
            }

            return (true, value);
        }

        private static object Liquidize(object obj)
        {
            if (obj == null)
            {
                return obj;
            }
            if (obj is ILiquidizable liquidizableObj)
            {
                return liquidizableObj.ToLiquid();
            }
            if (obj is string || obj is IEnumerable || obj is decimal || obj is DateTime || obj is DateTimeOffset || obj is TimeSpan || obj is Guid || obj is Enum)
            {
                return obj;
            }

            var valueType = obj.GetType();
            if (valueType.GetTypeInfo().IsPrimitive)
            {
                return obj;
            }

            if (TypeUtility.IsAnonymousType(valueType))
            {
                return obj;
            }

            var safeTypeTransformer = Template.GetSafeTypeTransformer(valueType);
            if (safeTypeTransformer != null)
            {
                return safeTypeTransformer(obj);
            }

            var attr = (LiquidTypeAttribute)valueType.GetTypeInfo().GetCustomAttributes(typeof(LiquidTypeAttribute), false).FirstOrDefault();
            if (attr != null)
            {
                return new DropProxy(obj, attr.AllowedMembers);
            }

            if (IsKeyValuePair(obj))
            {
                return obj;
            }

            throw new SyntaxException(Liquid.ResourceManager.GetString("ContextObjectInvalidException"), obj.ToString());
        }

        private static bool IsKeyValuePair(object obj)
        {
            if (obj != null)
            {
                Type valueType = obj.GetType();
                if (valueType.GetTypeInfo().IsGenericType)
                {
                    Type baseType = valueType.GetGenericTypeDefinition();
                    if (baseType == typeof(KeyValuePair<,>))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void SquashInstanceAssignsWithEnvironments()
        {
            Dictionary<string, object> tempAssigns = new Dictionary<string, object>(Template.NamingConvention.StringComparer);

            Hash lastScope = Scopes.Last();
            foreach (string k in lastScope.Keys)
                foreach (Hash env in Environments)
                    if (env.ContainsKey(k))
                    {
                        tempAssigns[k] = env[k];
                        break;
                    }

            foreach (string k in tempAssigns.Keys)
                lastScope[k] = tempAssigns[k];
        }

        private readonly int _timeout;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly CancellationToken _cancellationToken = CancellationToken.None;

        public void RestartTimeout()
        {
            _stopwatch.Restart();
            _cancellationToken.ThrowIfCancellationRequested();
        }

        public void CheckTimeout()
        {
            if (_timeout > 0 && _stopwatch.ElapsedMilliseconds > _timeout)
            {
                throw new TimeoutException();
            }

            _cancellationToken.ThrowIfCancellationRequested();
        }
    }
}