﻿#region LICENSE
// ---------------------------------- LICENSE ---------------------------------- //
//
//    Fling OS - The educational operating system
//    Copyright (C) 2015 Edward Nutting
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//  Project owner: 
//		Email: edwardnutting@outlook.com
//		For paper mail address, please contact via email for details.
//
// ------------------------------------------------------------------------------ //
#endregion

using System;

namespace Kernel.FOS_System.Collections
{
    /// <summary>
    /// Represents a strongly typed list of UInt64s that can be accessed by 
    /// index. Provides methods to search and manipulate lists.
    /// </summary>
    public class UInt64List : FOS_System.Object
    {
        /// <summary>
        /// The underlying object array.
        /// </summary>
        /// <remarks>
        /// When describing entries in the internal, the internal array should be seen
        /// as a list which grows downwards. So the first entry (index 0) is the top of 
        /// the list. Adding an entry extends the list downwards by one. Removing an 
        /// entry shifts the remaining items up by one.
        /// </remarks>
        protected UInt64[] _array;
        /// <summary>
        /// The "currentIndex" is the index to insert the next new item.
        /// It is the index immediately after the last-set item in the array.
        /// It thus also acts as an item count.
        /// </summary>
        protected int nextIndex = 0;

        /// <summary>
        /// The number of elements in the list.
        /// </summary>
        public int Count
        {
            [Drivers.Compiler.Attributes.NoGC]
            [Drivers.Compiler.Attributes.NoDebug]
            get
            {
                return nextIndex;
            }
        }

        public int Capacity
        {
            [Drivers.Compiler.Attributes.NoGC]
            [Drivers.Compiler.Attributes.NoDebug]
            get
            {
                return _array.Length;
            }
            set
            {
                if (value > _array.Length)
                {
                    ExpandCapacity(value - _array.Length);
                }
            }
        }

        public int ExpandAmount = 5;

        /// <summary>
        /// Creates a new list with initial capacity of 5.
        /// </summary>
        [Drivers.Compiler.Attributes.NoDebug]
        public UInt64List()
            : this(5)
        {
        }
        /// <summary>
        /// Creates a new list with specified initial capacity. Use this to optimise memory usage.
        /// </summary>
        /// <param name="capacity">The initial capacity of the list.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        public UInt64List(int capacity)
            : this(capacity, 5)
        {
        }
        /// <summary>
        /// Creates a new list with specified initial capacity. Use this to optimise memory usage.
        /// </summary>
        /// <param name="capacity">The initial capacity of the list.</param>
        /// <param name="expandAmount">The amount to expand the list capacity by each time the capacity must be increased.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        public UInt64List(int capacity, int expandAmount)
        {
            //Create the internal array with specified capacity.
            _array = new UInt64[capacity];
            ExpandAmount = expandAmount;
        }

        /// <summary>
        /// Adds the specified UInt32 to the list.
        /// </summary>
        /// <param name="obj">The UInt32 to add.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        public void Add(UInt64 obj)
        {
            //If the next index to insert an item at is beyond the capacity of
            //  the array, we need to expand the array.
            if (nextIndex >= _array.Length)
            {
                ExpandCapacity(ExpandAmount);
            }
            //Insert the value at the next index in the internal array then increment
            //  next index 
            _array[nextIndex] = obj;
            nextIndex++;
        }
        /// <summary>
        /// Removes the first equal value of the specified UInt64 from the list.
        /// </summary>
        /// <param name="obj">The UInt64 to remove.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        public void Remove(UInt64 obj)
        {
            bool setObjectToNull = false;
            int origNextIndex = nextIndex;
            for (int i = 0; i < origNextIndex; i++)
            {
                if (_array[i] == obj || setObjectToNull)
                {
                    if (!setObjectToNull)
                    {
                        nextIndex--;
                    }

                    setObjectToNull = true;

                    if (i < nextIndex)
                    {
                        _array[i] = _array[i + 1];
                    }
                    else
                    {
                        _array[i] = 0;
                    }
                }
            }
        }
        /// <summary>
        /// The removes the UInt64 at the specified index from the list.
        /// </summary>
        /// <param name="index">The index of the UInt64 to remove.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        public void RemoveAt(int index)
        {
            if (index >= nextIndex)
            {
                ExceptionMethods.Throw(new Exceptions.IndexOutOfRangeException(index, nextIndex));
            }

            for (int i = index; i < nextIndex; i++)
            {
                if (i < nextIndex - 1)
                {
                    _array[i] = _array[i + 1];
                }
                else
                {
                    _array[i] = 0;
                }
            }

            nextIndex--;
        }

        /// <summary>
        /// Returns the index of the first instance of the specified object or -1 
        /// if it is not found.
        /// </summary>
        /// <param name="obj">The object to search for.</param>
        /// <returns>The index or -1 if not found.</returns>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public int IndexOf(UInt64 obj)
        {
            for (int i = 0; i < nextIndex; i++)
            {
                if (_array[i] == obj)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool ContainsItemInRange(UInt64 start, UInt64 end)
        {
            for (int i = 0; i < nextIndex; i++)
            {
                UInt64 elem = _array[i];
                if (elem >= start && elem < end)
                {
                    return true;
                }
            }
            return false;
        }

        public UInt64 Last()
        {
            if (nextIndex == 0)
            {
                return 0;
            }

            return _array[nextIndex - 1];
        }

        /// <summary>
        /// Empties the list of all objects but does not alter the list capacity.
        /// </summary>
        [Drivers.Compiler.Attributes.NoDebug]
        public void Empty()
        {
            //Reset the count to 0
            nextIndex = 0;
            //Nice and simple again - just set everything to 0 :)
            for (int i = 0; i < nextIndex; i++)
            {
                _array[i] = 0;
            }
        }

        /// <summary>
        /// Expands the capacity of the internel array that stores the objects.
        /// </summary>
        /// <param name="amount">The amount to expand the capacity by.</param>
        [Drivers.Compiler.Attributes.NoDebug]
        private void ExpandCapacity(int amount)
        {
            UInt64[] newArray = new UInt64[_array.Length + amount];
            for (int i = 0; i < _array.Length; i++)
            {
                newArray[i] = _array[i];
            }
            _array = newArray;
        }

        /// <summary>
        /// Gets the UInt64 at the specified index.
        /// </summary>
        /// <param name="index">The index of the UInt64 to get.</param>
        /// <returns>The UInt64 at the specified index.</returns>
        /// <exception cref="Kernel.FOS_System.Exceptions.IndexOutOfRangeException">
        /// Throws IndexOutOfRangeException if "index" is &lt; 0 or greater than the length of the list.
        /// </exception>
        public UInt64 this[int index]
        {
            [Drivers.Compiler.Attributes.NoDebug]
            get
            {
                if (index >= nextIndex)
                {
                    ExceptionMethods.Throw(new Exceptions.IndexOutOfRangeException(index, nextIndex));
                }
                else if (index < 0)
                {
                    ExceptionMethods.Throw(new Exceptions.IndexOutOfRangeException(index, nextIndex));
                }

                return _array[index];
            }
            [Drivers.Compiler.Attributes.NoDebug]
            set
            {
                //Throw an exception if the index to set is beyond the length of
                //  the list.
                //Note: Beyond the length of the list not the capacity!
                if (index >= nextIndex)
                {
                    ExceptionMethods.Throw(new Exceptions.IndexOutOfRangeException(index, nextIndex));
                }
                else if (index < 0)
                {
                    ExceptionMethods.Throw(new Exceptions.IndexOutOfRangeException(index, nextIndex));
                }

                _array[index] = value;
            }
        }
    }
}
