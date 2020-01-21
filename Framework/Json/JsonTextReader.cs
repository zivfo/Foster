﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Foster.Framework.Json
{
    /// <summary>
    /// Reads JSON from a Stream or Path
    /// </summary>
    public class JsonTextReader : JsonReader, IDisposable
    {

        private readonly TextReader reader;
        private readonly StringBuilder builder = new StringBuilder();

        // in the case where the value of a previous key is completely empty, we want to
        // return null, and then store the current value for the next Read call
        // this only matters for non-strict JSON
        private bool storedNext;
        private string? storedString;
        private JsonToken storedToken;

        public JsonTextReader(string path) : this(File.OpenRead(path))
        {

        }

        public JsonTextReader(Stream stream) : this(new StreamReader(stream, Encoding.UTF8, true, 4096))
        {

        }

        public JsonTextReader(TextReader reader)
        {
            this.reader = reader;
        }

        public override bool Read()
        {
            Value = null;
            var lastToken = Token;

            if (storedNext)
            {
                Value = storedString;
                Token = storedToken;
                storedNext = false;
                return true;
            }

            while (Step(out var next))
            {
                // skip whitespace and characters we don't care about
                if (char.IsWhiteSpace(next) || next == ':' || next == ',')
                    continue;

                var isEncapsulated = false;

                switch (next)
                {
                    // object
                    case '{':
                        Token = JsonToken.ObjectStart;
                        return true;

                    case '}':

                        // if we found an object-end after a key
                        // set the value of that last key to null, and store this value for next time
                        if (lastToken == JsonToken.ObjectKey)
                        {
                            storedNext = true;
                            storedToken = JsonToken.ObjectEnd;
                            storedString = null;

                            Value = null;
                            Token = JsonToken.Null;
                            return true;
                        }

                        Token = JsonToken.ObjectEnd;
                        return true;

                    // array
                    case '[':
                        Token = JsonToken.ArrayStart;
                        return true;

                    case ']':

                        // if we found an array-end after a key
                        // set the value of that last key to null, and store this value for next time
                        if (lastToken == JsonToken.ObjectKey)
                        {
                            storedNext = true;
                            storedToken = JsonToken.ArrayEnd;
                            storedString = null;

                            Value = null;
                            Token = JsonToken.Null;
                            return true;
                        }

                        Token = JsonToken.ArrayEnd;
                        return true;

                    // an encapsulated string
                    case '"':
                        {
                            builder.Clear();

                            char last = next;
                            while (Step(out next) && (next != '"' || last == '\\'))
                                builder.Append(last = next);

                            isEncapsulated = true;
                            break;
                        }

                    // other value
                    default:
                        {
                            builder.Clear();
                            builder.Append(next);

                            while (Peek(out next) && !("\r\n,:{}[]#").Contains(next))
                            {
                                builder.Append(next);
                                Skip();
                            }

                            break;
                        }
                }

                // check if this entry is a KEY
                bool isKey = false;
                {
                    if (char.IsWhiteSpace(next))
                    {
                        while (Peek(out next) && char.IsWhiteSpace(next))
                            Skip();
                    }

                    if (Peek(out next) && next == ':')
                        isKey = true;
                }

                // is a key
                if (isKey)
                {
                    // if we found an key after a key
                    // set the value of that last key to null, and store this value for next time
                    if (lastToken == JsonToken.ObjectKey)
                    {
                        storedNext = true;
                        storedToken = JsonToken.ObjectKey;
                        storedString = builder.ToString();

                        Value = null;
                        Token = JsonToken.Null;
                        return true;
                    }

                    Token = JsonToken.ObjectKey;
                    Value = builder.ToString();
                    return true;
                }
                // is an ecnapsulated string
                else if (isEncapsulated)
                {
                    Token = JsonToken.String;
                    Value = builder.ToString();
                    return true;
                }
                else
                {
                    var str = builder.ToString();

                    // null value
                    if (str.Length <= 0 || str.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        Token = JsonToken.Null;
                        return true;
                    }
                    // true value
                    else if (str.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        Token = JsonToken.Boolean;
                        Value = true;
                        return true;
                    }
                    // false value
                    else if (str.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        Token = JsonToken.Boolean;
                        Value = false;
                        return true;
                    }
                    // could be a number value ...
                    // this is kinda ugly ... but we just try to fit it into the smallest number type it can be
                    else if ((str[0] >= '0' && str[0] <= '9') || str[0] == '-' || str[0] == '+' || str[0] == '.')
                    {
                        Token = JsonToken.Number;

                        // decimal, float, double
                        if (str.Contains('.'))
                        {
                            if (float.TryParse(str, out float floatValue))
                            {
                                Value = floatValue;
                                return true;
                            }
                            else if (double.TryParse(str, out double doubleValue))
                            {
                                Value = doubleValue;
                                return true;
                            }
                        }
                        else if (int.TryParse(str, out int intValue))
                        {
                            Value = intValue;
                            return true;
                        }
                        else if (long.TryParse(str, out long longValue))
                        {
                            Value = longValue;
                            return true;
                        }
                        else if (ulong.TryParse(str, out ulong ulongValue))
                        {
                            Value = ulongValue;
                            return true;
                        }
                    }

                    // fallback to string
                    Token = JsonToken.String;
                    Value = str;
                    return true;
                }

            }

            return false;

            bool Skip()
            {
                return Step(out _);
            }

            bool Step(out char next)
            {
                int read = reader.Read();
                next = (char)read;
                return read >= 0;
            }

            bool Peek(out char next)
            {
                int read = reader.Peek();
                next = (char)read;
                return read >= 0;
            }
        }

        public void Dispose()
        {
            reader.Dispose();
        }
    }
}