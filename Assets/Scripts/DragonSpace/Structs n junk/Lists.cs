using System;
using System.Collections.Generic;

namespace DragonSpace.Lists
{
    /// <summary>
    /// Creates a pool of generic lists of type T to avoid GC allocations. Lists must be returned
    /// by reference. A memory leak will result if the reference is lost (using new List()) or not returned.
    /// </summary>
    /// <typeparam name="T">The type to use for the lists</typeparam>
    public class ListPool<T>
    {
        private readonly Stack<List<T>> _pool;
        private int Capacity { get; set; }

        public ListPool()
        {
            _pool = new Stack<List<T>>(4);  //create the pool
            _pool.Push(new List<T>());      //start with just one list
        }

        /// <param name="capacity">How many lists to start with</param>
        public ListPool(int capacity) : this()
        {
            Init(capacity, 8);
        }

        /// <param name="capacity">How many lists to start with</param>
        /// <param name="listLength">How long each list should start</param>
        public ListPool(int capacity, int listLength) : this()
        {
            Init(capacity, listLength);
        }

        private void Init(int cap, int listLength)
        {
            ExpandPool(cap, listLength);  // TODO: this seems redundant huh
        }

        private void ExpandPool(int newSize)
        {
            ExpandPool(newSize, 8);
        }

        private void ExpandPool(int newSize, int listLength)
        {
            if (newSize < _pool.Count)
            { return; }
            Capacity = newSize;
            while (_pool.Count < newSize)
            {
                _pool.Push(new List<T>(listLength));
            }
        }

        /// <summary>
        /// Returns a <see cref="List{T}"/> from a pool. Be a good citizen and
        /// return with <see cref="ReturnList(List{T})"/>
        /// </summary>
        /// <returns>An empty <see cref="List{T}"/></returns>
        public List<T> RentList()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }
            else
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// Returns the list to the pool and clears its data
        /// </summary>
        /// <param name="li">The list rented with <see cref="RentList"/></param>
        public void ReturnList(List<T> li)
        {
            if (_pool.Count < Capacity)
            {
                li.Clear();
                _pool.Push(li);
            }
            //just let it get garbage collected if we have more than we need
        }
    }

    /// <summary>
    /// An indexed free list, will expand as needed
    /// </summary>
    /// <typeparam name="T">The struct or class type to list</typeparam>
    public class FreeList<T>
    {
        private FreeElement[] _data;
        private int _freeElement;    // Index of the first free slot or -1 if there are
                                     // no free slots before the end of the list

        public int Count { get; private set; } = 0;
        public int Capacity
        {
            get { return _data.Length; }
            private set
            {
                if (value > _data.Length)
                {
                    ExpandCapacity(value);
                }
            }  //TODO: handle reducing capacity?
        }

        /// <summary>
        /// Creates a new list of elements
        /// </summary>
        /// <param name="capacity">initial capacity of the list</param>
        public FreeList(int capacity = 4)
        {
            _data = new FreeElement[capacity];
            Count = 0;
            _freeElement = -1;
        }

        private void ExpandCapacity(int cap)
        {
            FreeElement[] new_array = new FreeElement[cap];
            Array.Copy(_data, 0, new_array, 0, Capacity);
            _data = new_array;
        }

        //TODO: ReduceCapacity()

        #region List Interface
        public ref T this[int index]
        {
            get { return ref _data[index].element; }
        }

        /// <summary>
        /// Clears the list
        /// </summary>
        public void Clear()
        {
            //note that the data is still in the underlying array
            //so memory is still reserved and the GC won't collect it. 
            //If it needs to be cleared, the list can just be resized with new
            Count = 0;
            _freeElement = -1;
        }
        #endregion

        /// <summary>
        /// Inserts an element to the back of the list and returns an index to it.
        /// Expands the array if necessary.
        /// </summary>
        /// <returns>The index where the new element was inserted</returns>
        private int PushBack(T elt)
        {
            // Check if the array is full
            if (Count == Capacity)
            {
                // Use double the size for the new capacity.
                ExpandCapacity(Count * 2);
            }
            _data[Count].element = elt;
            return Count++;
        }

        #region Free List Interface
        /// <summary>
        /// Inserts an element to a vacant position in the list and returns an index to it.
        /// </summary>
        /// <returns>index of the slot where the element was inserted</returns>
        public int Insert(T elt)
        {
            //if there's an open slot in the free list, pop that and use it
            if (_freeElement != -1)
            {
                int index = _freeElement;

                //set the free index to the next open index in the free list
                _freeElement = _data[index].next;

                //actually insert the element
                _data[index].element = elt;
                //return the index where the element was inserted
                return index;
            }
            // Otherwise insert to the back of the array.
            return PushBack(elt);
        }

        public void RemoveAt(int n)
        {
            // Add the slot to the free list
            _data[n].element = default;
            _data[n].next = _freeElement;  //Set the value of the slot to the next free slot
            _freeElement = n;              //Make this slot the first free slot
        }
        #endregion

        // Doesn't work with generic types :(
        ///// <summary>
        ///// C++ union style struct. Both fields overlap so only one will have valid data.
        ///// If an element is removed, write over the data and treat it as an int index for the array.
        ///// </summary>
        //[StructLayout(LayoutKind.Explicit)]
        //struct FreeElement
        //{
        //    [FieldOffset(0)] internal int next;
        //    [FieldOffset(0)] internal T element;
        //}

        /// <summary>
        /// Either the element or a reference to the next free slot
        /// </summary>
        private struct FreeElement
        {
            internal int next;
            internal T element;
        }
    }

}