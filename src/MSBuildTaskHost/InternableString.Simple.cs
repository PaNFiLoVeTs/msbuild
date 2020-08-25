﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace System
{
    /// <summary>
    /// A bare minimum and inefficient version of MemoryExtensions as provided in System.Memory on .NET 4.5.
    /// </summary>
    internal static class MemoryExtensions
    {
        public static string AsSpan<T>(this T[] array, int start, int length)
        {
            if (array is char[] charArray)
            {
                return new string(charArray, start, length);
            }
            throw new ArgumentException(nameof(array));
        }
    }
}

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Represents a string that can be converted to System.String with interning, i.e. by returning an existing string if it has been seen before
    /// and is still tracked in the intern table.
    /// </summary>
    /// <remarks>
    /// This is a simple and inefficient implementation compatible with .NET Framework 3.5.
    /// </remarks>
    internal ref struct InternableString
    {
        /// <summary>
        /// Enumerator for the top-level struct. Enumerates characters of the string.
        /// </summary>
        public ref struct Enumerator
        {
            /// <summary>
            /// The InternableString being enumerated.
            /// </summary>
            private readonly InternableString _string;

            /// <summary>
            /// Index of the current character, -1 if MoveNext has not been called yet.
            /// </summary>
            private int _charIndex;

            public Enumerator(InternableString spanBuilder)
            {
                _string = spanBuilder;
                _charIndex = -1;
            }

            /// <summary>
            /// Returns the current character.
            /// </summary>
            public char Current => _string[_charIndex];

            /// <summary>
            /// Moves to the next character.
            /// </summary>
            /// <returns>True if there is another character, false if the enumerator reached the end.</returns>
            public bool MoveNext()
            {
                int newIndex = _charIndex + 1;
                if (newIndex < _string.Length)
                {
                    _charIndex = newIndex;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// If this instance is used in a StringBuilder-like manner, it uses this backing field.
        /// </summary>
        private StringBuilder _builder;

        /// <summary>
        /// If this instance represents one contiguous string, it may be held in this field.
        /// </summary>
        private string _firstString;

        /// <summary>
        /// Constructs a new InternableString wrapping the given string. The instance is still mutable and can be used as a StringBuilder,
        /// although that may require an allocation.
        /// </summary>
        /// <param name="str">The string to wrap, must be non-null.</param>
        public InternableString(string str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }
            _builder = null;
            _firstString = str;
        }

        /// <summary>
        /// Constructs a new empty InternableString with the given expected number of spans. Such an InternableString is used similarly
        /// to a StringBuilder. This constructor allocates GC memory.
        /// </summary>
        internal InternableString(int capacity = 4)
        {
            _builder = new StringBuilder(capacity * 128);
            _firstString = null;
        }

        /// <summary>
        /// Gets the length of the string.
        /// </summary>
        public int Length => (_builder == null ? _firstString.Length : _builder.Length);

        /// <summary>
        /// Creates a new enumerator for enumerating characters in this string. Does not allocate.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Returns the character at the given index.
        /// </summary>
        /// <param name="index">The index to return the character at.</param>
        /// <returns>The character.</returns>
        public char this[int index] => (_builder == null ? _firstString[index] : _builder[index]);

        /// <summary>
        /// Returns true if the string starts with another string.
        /// </summary>
        /// <param name="other">Another string.</param>
        /// <returns>True if this string starts with <paramref name="other"/>.</returns>
        public bool StartsWithStringByOrdinalComparison(string other)
        {
            if (_firstString != null)
            {
                return _firstString.StartsWith(other);
            }

            if (Length < other.Length)
            {
                return false;
            }
            for (int i = 0; i < other.Length; i++)
            {
                if (other[i] != this[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns a System.String representing this string. Allocates memory unless this InternableString was created by wrapping a
        /// System.String in which case the original string is returned.
        /// </summary>
        /// <returns>The string.</returns>
        public string ExpensiveConvertToString()
        {
            // Special case: if we hold just one string, we can directly return it.
            if (_firstString != null)
            {
                return _firstString;
            }
            return _builder.ToString();
        }

        /// <summary>
        /// Returns true if this InternableString wraps a System.String and the same System.String is passed as the argument.
        /// </summary>
        /// <param name="str">The string to compare to.</param>
        /// <returns>True is this instance wraps the given string.</returns>
        public bool ReferenceEquals(string str)
        {
            return Object.ReferenceEquals(str, _firstString);
        }

        /// <summary>
        /// Converts this instance to a System.String while first searching for a match in the intern table.
        /// </summary>
        /// <remarks>
        /// May allocate depending on whether the string has already been interned.
        /// </remarks>
        public override unsafe string ToString()
        {
            return OpportunisticIntern.InternableToString(this);
        }

        /// <summary>
        /// Appends a string.
        /// </summary>
        /// <param name="value">The string to append.</param>
        internal void Append(string value)
        {
            _builder ??= new StringBuilder(_firstString);
            _firstString = null;
            _builder.Append(value);
        }

        /// <summary>
        /// Appends a substring.
        /// </summary>
        /// <param name="value">The string to append.</param>
        /// <param name="startIndex">The start index of the substring within <paramref name="value"/> to append.</param>
        /// <param name="count">The length of the substring to append.</param>
        internal void Append(string value, int startIndex, int count)
        {
            _builder ??= new StringBuilder(_firstString);
            _firstString = null;
            _builder.Append(value, startIndex, count);
        }

        /// <summary>
        /// Clears this instance making it represent an empty string.
        /// </summary>
        public void Clear()
        {
            _builder.Length = 0;
            _firstString = null;
        }
    }
}
