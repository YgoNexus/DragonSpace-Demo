﻿using DragonSpace.Structs;
using System;
using System.Collections.Generic;

namespace DragonSpace.Grids
{
    public class UGrid<T> where T : class, IUGridElt
    {
        /// <summary>
        /// the size of the grid
        /// </summary>
        readonly int _width, _height;

        /// <summary>Stores all the rows in the grid</summary>
        private T[][] rows;
        private int[] rowCounts;

        // Stores the number of columns and rows in the grid.
        readonly int _numRows, _numCols;

        /// <summary>
        /// stores the size of a cell
        /// </summary>
        readonly float _invCellWidth, _invCellHeight;

        /// <summary>
        /// The size of all elements stored in the grid.
        /// </summary>
        readonly int _eltWidth, _eltHeight;

        // Returns a new grid storing elements that have a uniform upper-bound size. Because 
        // all elements are treated uniformly-sized for the sake of search queries, each one 
        // can be stored as a single point in the grid.
        public UGrid(int eWidth, int eHeight, float cWidth, float cHeight,
            int gridWidth, int gridHeight)
        {
            _eltWidth = eWidth;
            _eltHeight = eHeight;
            _numRows = (int)( gridHeight / cHeight ) + 1;
            _numCols = (int)( gridWidth / cWidth ) + 1;
            _invCellWidth = 1 / cWidth;
            _invCellHeight = 1 / cHeight;
            _width = gridWidth;
            _height = gridHeight;

            rows = new T[_numRows][];
            rowCounts = new int[_numRows];
            for (int i = 0; i < _numRows; i++)
            {
                rows[i] = new T[_numCols];
                for (int j = 0; j < _numCols; j++)
                {
                    rows[i][j] = default(T);
                }
            }
        }



        /// <summary>
        /// Inserts an element to the grid, using the position 
        /// from the <see cref="IUGridElt"/> interface
        /// </summary>
        /// <param name="obj">The object implementing <see cref="IUGridElt"/> to insert</param>
        public void Insert(T obj)
        {
            int xIdx = GridLocalToCellCol(obj.Xf);
            int yIdx = GridLocalToCellRow(obj.Yf);
            InsertToCell(obj, xIdx, yIdx);
        }

        private void InsertToCell(T obj, int xIdx, int yIdx)
        {
            //UGridRow row = rows[yIdx];
            obj.NextElt = rows[yIdx][xIdx];
            rows[yIdx][xIdx] = obj;
            rowCounts[yIdx]++;
        }

        /// <summary>
        /// Removes an element from the grid. The <see cref="IUGridElt"/> of the object must give
        /// the same position where the element is currently in the grid
        /// </summary>
        /// <param name="obj">The object to remove</param>
        public void Remove(T obj)
        {
            int xIdx = GridLocalToCellCol(obj.Xf);
            int yIdx = GridLocalToCellRow(obj.Yf);
            RemoveFromCell(obj, xIdx, yIdx);
        }

        private void RemoveFromCell(T obj, int xIdx, int yIdx)
        {
            var row = rows[yIdx];

            T elt = row[xIdx];
            T prevElt = null;
            while (elt.ID != obj.ID)
            {
                prevElt = elt;
                elt = (T)elt.NextElt;

                if (elt == null)
                {
                    throw new Exception("Element not found");
                }
            }

            if (prevElt == null)
                row[xIdx] = (T)elt.NextElt;
            else
                prevElt.NextElt = elt.NextElt;

            rowCounts[yIdx]--;
        }

        /// <summary>
        /// Moves an element in the grid from the former position to the new one.
        /// </summary>
        /// <param name="obj">The object to move</param>
        /// <param name="fromX">The current position of the object in the grid</param>
        /// <param name="fromY">The current position of the object in the grid</param>
        /// <param name="toX">The new position of the object in the grid</param>
        /// <param name="toY">The new position of the object in the grid</param>
        public void Move(T obj, float fromX, float fromY, float toX, float toY)
        {
            int oldCol = GridLocalToCellCol(fromX);
            int oldRow = GridLocalToCellRow(fromY);
            int newCol = GridLocalToCellCol(toX);
            int newRow = GridLocalToCellRow(toY);

            if (oldCol != newCol || oldRow != newRow)
            {
                RemoveFromCell(obj, oldCol, oldRow);
                InsertToCell(obj, newCol, newRow);
            }
        }

        private readonly List<T> _queryResults = new List<T>(16);
        // Returns all the element IDs that intersect the specified rectangle excluding 
        // elements with the specified ID to omit.
        public List<T> Query(float lft, float btm, float rgt, float top, int omitEltID = int.MinValue)
        {
            // expand the query by the size of the elements
            // since we use the bottom left point to represent elements, we expand
            // the bottom left of the query to catch any elements with an origin 
            // outside the query but with a bounding box inside it
            lft -= _eltWidth;
            btm -= _eltHeight;

            // Find the cells that intersect the search query.
            int minX = GridLocalToCellCol(lft);
            int minY = GridLocalToCellRow(btm);
            int maxX = GridLocalToCellCol(rgt);
            int maxY = GridLocalToCellRow(top);

            AABB query = new AABB(lft, top, rgt, btm);

            _queryResults.Clear();
            for (int y = minY; y <= maxY; ++y)
            {
                var row = rows[y];
                if (rowCounts[y] == 0)
                    continue;

                for (int x = minX; x <= maxX; ++x)
                {
                    T elt = row[x];
                    while (elt != null)
                    {
                        if (PointInRect(elt, in query) && elt.ID != omitEltID)
                            _queryResults.Add(elt);
                        elt = (T)elt.NextElt;
                    }
                }
            }
            return _queryResults;
        }

        /// <summary>
        /// Calls <see cref="IUniformGridVisitor.Grid(float, float, float, float)"/> with the grid data
        /// Then traverses the whole grid and calls <see cref="IUniformGridVisitor.Cell(IUGridElt, int, int, float, float)"/>
        /// on any non-empty cells.
        /// </summary>
        public void Traverse(IUniformGridVisitor visitor)
        {
            visitor.Grid(_width, _height, 1f / _invCellWidth, 1f / _invCellHeight);

            for (int y = 0; y < _numRows; ++y)
            {
                var row = rows[y];
                if (rowCounts[y] == 0)
                    continue;

                for (int x = 0; x < _numCols; ++x)
                {
                    if (row[x] != null)
                    {
                        visitor.Cell(row[x], x, y, 1f / _invCellWidth, 1f / _invCellHeight);
                    }
                }
            }
        }

        #region Private methods
        /// <summary>
        /// Returns the grid cell Y index for the specified position
        /// </summary>
        private int GridLocalToCellRow(float y)
        {
            if (y <= 0)
            { return 0; }
            return Math.Min((int)( y * _invCellHeight ), _numRows - 1);
        }

        /// <summary>
        /// Returns the grid cell X index for the specified position
        /// </summary>
        private int GridLocalToCellCol(float x)
        {
            if (x <= 0)
            { return 0; }
            return Math.Min((int)( x * _invCellWidth ), _numCols - 1);
        }

        private static bool PointInRect(in IUGridElt elt, in AABB rect)
        {
            return elt.Xf <= rect.rgt && elt.Xf >= rect.lft && elt.Yf <= rect.top && elt.Yf >= rect.btm;
        }
        #endregion

        /// <summary>
        /// Just an array of type T and a cached count of the elements in the row
        /// </summary>
        //class UGridRow
        //{
        //    // Stores all the cells in the row. 
        //    // Each cell stores the first element in that cell, 
        //    // which points to the next in the elts list.
        //    public T[] cells;

        //    // Stores the number of elements in the row.
        //    public int eltCount;
        //}
    }
}