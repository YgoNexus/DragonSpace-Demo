namespace DragonSpace.Grids
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DragonSpace.Structs;
    using DragonSpace.Grids;
    //public class LooseDoubleGrid
    public class LooseTightGrid
    {
        private readonly LooseCell[] grid;

        /// <summary>
        /// the size of the grid
        /// </summary>
        private readonly int boundWidth, boundHeight;

        // Stores the number of columns and rows in the grid.
        private readonly int _numRows, _numCols;

        /// <summary>
        /// stores the size of a cell
        /// </summary>
        private readonly float _invCellWidth, _invCellHeight;

        //public LooseDoubleGrid(float cellWidth, float cellHeight, float coarseWidth, float coarseHeight, int boundWidth, int boundHeight)
        public LooseTightGrid(float cellWidth, float cellHeight, int boundWidth, int boundHeight)
        {
            this.boundWidth = boundWidth;
            this.boundHeight = boundHeight;
            _numRows = (int)( boundHeight / cellHeight ) + 1;
            _numCols = (int)( boundWidth / cellWidth ) + 1;
            _invCellWidth = 1 / cellWidth;
            _invCellHeight = 1 / cellHeight;

            //init rows
            grid = new LooseCell[_numRows * _numCols];
            //init columns
            for (int i = 0; i < _numRows; i++)
            {
                for (int j = 0; j < _numCols; j++)
                {
                    grid[i * _numCols + j] = new LooseCell();
                }
            }
        }

        #region Public methods
        /// <summary>
        /// Inserts an object into the grid, expanding the cell to fit the element
        /// </summary>
        /// <param name="obj">The object to insert</param>
        public void Insert(IGridElt obj)
        {
            int xIdx = GridLocalToCellCol(obj.LeftX);
            int yIdx = GridLocalToCellRow(obj.BottomY);
            InsertToCell(obj, xIdx, yIdx);
        }

        /// <summary>
        /// Inserts an object into a grid cell at the given index
        /// </summary>
        /// <param name="elt">The object to insert</param>
        /// <param name="xIdx">The column index of the cell</param>
        /// <param name="yIdx">The row index of the cell</param>
        private void InsertToCell(IGridElt elt, int xIdx, int yIdx)
        {
            //if the cell is empty, initialize the bounds to match the element
            if (grid[yIdx * _numCols + xIdx].FirstElt == null)
            {
                grid[yIdx * _numCols + xIdx].Push(elt);
                grid[yIdx * _numCols + xIdx].lft = (int)elt.LeftX;
                grid[yIdx * _numCols + xIdx].btm = (int)elt.BottomY;
                grid[yIdx * _numCols + xIdx].rgt = (int)elt.LeftX + elt.Width;
                grid[yIdx * _numCols + xIdx].top = (int)elt.BottomY + elt.Height;
            }
            else    //otherwise, see if the bounds need to change to fit the element
            {
                grid[yIdx * _numCols + xIdx].Push(elt);
                ExpandCell(yIdx, xIdx, elt);
            }
        }
        /// <summary>
        /// Removes an element from the grid. The <see cref="IUGridElt"/> of the object must give
        /// the same position where the element is currently in the grid
        /// </summary>
        /// <param name="obj">The object to remove</param>
        public void Remove(IGridElt obj)
        {
            int xIdx = GridLocalToCellCol(obj.LeftX);
            int yIdx = GridLocalToCellRow(obj.BottomY);
            RemoveFromCell(obj, xIdx, yIdx);
        }
        private void RemoveFromCell(IGridElt obj, int xIdx, int yIdx)
        {
            IGridElt elt = grid[yIdx * _numCols + xIdx].FirstElt;
            IGridElt prevElt = null;

            while (elt != null && elt.ID != obj.ID)
            {
                prevElt = elt;
                elt = elt.NextElt;
            }

            if (prevElt == null)
                grid[yIdx * _numCols + xIdx].Pop();
            else
                prevElt.NextElt = elt.NextElt;
        }
        /// <summary>
        /// Moves an element in the grid from the former position to the new one.
        /// </summary>
        /// <param name="obj">The object to move</param>
        /// <param name="fromX">The current position of the object in the grid</param>
        /// <param name="fromY">The current position of the object in the grid</param>
        /// <param name="toX">The new position of the object in the grid</param>
        /// <param name="toY">The new position of the object in the grid</param>
        public void Move(IGridElt obj, float fromX, float fromY, float toX, float toY)
        {
            int oldCol = GridLocalToCellCol(fromX);
            int oldRow = GridLocalToCellRow(fromY);
            int newCol = GridLocalToCellCol(toX);
            int newRow = GridLocalToCellRow(toY);

            //ref LooseGridRow row = ref looseGridRowArr[oldRow];

            if (oldCol != newCol || oldRow != newRow)
            {
                RemoveFromCell(obj, oldCol, oldRow);
                InsertToCell(obj, newCol, newRow);
            }
            else
            {
                //just expand the cell if necessary, we can contract it later
                ExpandCell(oldRow, oldCol, obj);
            }
        }

        private readonly List<IGridElt> _queryResults = new List<IGridElt>(16);
        /// <summary>
        /// Returns all the elements that intersect the specified rectangle excluding 
        /// elements with the specified ID to omit.
        /// </summary>
        /// <param name="omitEltID">The ID of the element from the <see cref="IGridElt"/> interface to omit from results</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="IGridElt"/>s</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<IGridElt> Query(float lft, float btm, float rgt, float top, int omitEltID = int.MinValue)
        {
            // Find the coarse cells that intersect the search query.
            int minX = GridLocalToCellCol(lft);
            int minY = GridLocalToCellRow(btm);
            int maxX = GridLocalToCellCol(rgt);
            int maxY = GridLocalToCellRow(top);

            AABB query = new AABB(lft, top, rgt, btm);

            _queryResults.Clear();

            for (int y = minY; y <= maxY; ++y)
            {
                for (int x = minX; x <= maxX; ++x)
                {
                    var elt = grid[y * _numCols + x].FirstElt;
                    while (elt != null)
                    {
                        if (RectOverlap(in query, in elt) && elt.ID != omitEltID)
                            _queryResults.Add(elt);
                        elt = elt.NextElt;
                    }
                }
            }
            return _queryResults;
        }

        /// <summary>
        /// Contracts all the loose cell boundaries and removes them from coarse cells.
        /// Run at the end of every frame or update
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TightenUp()
        {
            for (int i = _numRows - 1; i >= 0; i--)
            {
                for (int j = _numCols - 1; j >= 0; j--)
                {
                    ContractCell(i, j);
                }
            }
        }

        /// <summary>
        /// Calls <see cref="ILooseGridVisitor.CoarseGrid(float, float, float, float)"/> with the grid data
        /// Then traverses the whole grid and calls <see cref="IUniformGridVisitor.Cell(int, int)"/>
        /// on any non-empty cells.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Traverse(ILooseTightGridVisitor visitor)
        {
            visitor.LooseGrid(boundWidth, boundHeight, 1f / _invCellWidth, 1f / _invCellHeight);

            for (int i = _numRows - 1; i >= 0; i--)
            {
                for (int j = _numCols - 1; j >= 0; j--)
                {
                    LooseCell cell = grid[i * _numCols + j];
                    visitor.LooseCell(cell.FirstElt, new AABB(cell.lft, cell.top, cell.rgt, cell.btm));
                }
            }
        }
        #endregion

        #region Private methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandCell(int row, int col, IGridElt elt)
        {
            int eLft = (int)elt.LeftX;
            int eBtm = (int)elt.BottomY;

            grid[row * _numCols + col].lft = Math.Min(grid[row * _numCols + col].lft, eLft);
            grid[row * _numCols + col].btm = Math.Min(grid[row * _numCols + col].btm, eBtm);
            grid[row * _numCols + col].rgt = Math.Max(grid[row * _numCols + col].rgt, eLft + elt.Width);
            grid[row * _numCols + col].top = Math.Max(grid[row * _numCols + col].top, eBtm + elt.Height);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ContractCell(int row, int col)
        {
            grid[row * _numCols + col].lft = grid[row * _numCols + col].btm = int.MaxValue;
            grid[row * _numCols + col].rgt = grid[row * _numCols + col].top = int.MinValue;
            IGridElt elt = grid[row * _numCols + col].FirstElt;

            while (elt != null)
            {
                int eLft = (int)elt.LeftX;
                int eBtm = (int)elt.BottomY;

                grid[row * _numCols + col].lft = Math.Min(grid[row * _numCols + col].lft, eLft);
                grid[row * _numCols + col].btm = Math.Min(grid[row * _numCols + col].btm, eBtm);
                grid[row * _numCols + col].rgt = Math.Max(grid[row * _numCols + col].rgt, eLft + elt.Width);
                grid[row * _numCols + col].top = Math.Max(grid[row * _numCols + col].top, eBtm + elt.Height);

                elt = elt.NextElt;
            }
        }
        // Returns the grid cell Y index for the specified position.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCellRow(float y)
        {
            return y <= 0 ? 0 : Math.Min((int)( y * _invCellHeight ), _numRows - 1);
        }

        // Returns the grid cell X index for the specified position.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCellCol(float x)
        {
            return x <= 0 ? 0 : Math.Min((int)( x * _invCellWidth ), _numCols - 1);
        }

        //TODO: move somewhere more useful
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(in AABB a, in IGridElt b)
        {
            int bLft = (int)b.LeftX;
            int bBtm = (int)b.BottomY;
            return RectOverlap(a.lft, a.top, a.rgt, a.btm, bLft, bBtm + b.Height, bLft + b.Width, bBtm);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(in AABB a, in LooseCell b)
        {
            return RectOverlap(a.lft, a.top, a.rgt, a.btm, b.lft, b.top, b.rgt, b.btm);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(int l1, int t1, int r1, int b1, int l2, int t2, int r2, int b2)
        {
            return l2 <= r1 && r2 >= l1 && t2 >= b1 && b2 <= t1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(float l1, float t1, float r1, float b1, float l2, float t2, float r2, float b2)
        {
            return l2 <= r1 && r2 >= l1 && t2 >= b1 && b2 <= t1;
        }
        #endregion

        //=============================================
        #region child classes

        private struct LooseCell
        {
            /// <summary>
            /// The first element in the linked list for this cell, 
            /// the rest are accessed by <see cref="IGridElt.NextElt"/>
            /// </summary>
            public IGridElt FirstElt;

            /// <summary>X coordinate of the cell's bottom-left corner</summary>
            public int lft;
            /// <summary>Y coordinate of the cell's bottom-left corner</summary>
            public int btm;
            /// <summary>Width of the cell's bounding box</summary>
            public int rgt;
            /// <summary>Height of the cell's bounding box</summary>
            public int top;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(IGridElt elt)
            {
                elt.NextElt = FirstElt;
                FirstElt = elt;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Pop()
            {
                FirstElt = FirstElt.NextElt;
            }
        }
    }
    #endregion
}